namespace CathodeRay.C;

/// <summary>Opadanie rejestrów wirtualnych do Cell IR: rejestry wracają do pierwotnych komórek
/// (mapa <see cref="VReg.Function.Sym"/>), pary <c>Cmp + Br</c> zlewają się w <c>BrCmp</c>
/// (z negacją warunku albo dodatkowym skokiem, gdy żaden cel nie jest spadkiem).
/// Dalej moduł idzie istniejącym potokiem celu (legalizacja, ABI, selektor).</summary>
internal static class VRegToCell
{
    /// <summary>Przepisuje moduł VReg na Cell IR.</summary>
    /// <param name="module">Moduł w rejestrach wirtualnych.</param>
    /// <returns>Moduł Cell IR.</returns>
    public static Ir.Module Run(VReg.Module module)
    {
        ArgumentNullException.ThrowIfNull(module);
        return new Ir.Module(
            [.. module.Functions.Select(LowerFunction)],
            module.Data,
            module.ExternFunctions,
            module.ExternCells,
            module.ObjectMode,
            module.Volatile);
    }

    private static Ir.Function LowerFunction(VReg.Function function)
    {
        var body = new List<Ir.Ins>();
        for (int b = 0; b < function.Blocks.Count; b++)
        {
            VReg.Block block = function.Blocks[b];
            if (block.Explicit)
            {
                body.Add(new Ir.Label(block.Label));
            }

            string next = b + 1 < function.Blocks.Count ? function.Blocks[b + 1].Label : "__end";
            LowerBlock(function, block, next, body);
        }

        var parameters = new List<Ir.Cell>(function.Params.Count);
        foreach (VReg.Reg param in function.Params)
        {
            parameters.Add(CellOf(function, param));
        }

        return new Ir.Function(function.Name, function.IsStatic, parameters, function.RetW, ShrinkSaved(function), body);
    }

    /// <summary>Ramka po przebiegach VReg: z pierwotnego <c>Saved</c> zostają wpisy, których baza żyje przez
    /// któreś wołanie (reguła <c>LiveAcrossCalls</c>), więc martwe po DCE komórki nie idą na stos.</summary>
    private static IReadOnlyList<Ir.Owned> ShrinkSaved(VReg.Function function)
    {
        VRegLiveness live = VRegLiveness.Of(function);
        var across = new HashSet<string>(function.Params.Select(p => BaseSymbol(function.Sym[p.Id])), StringComparer.Ordinal);
        for (int i = 0; i < live.Count; i++)
        {
            if (live.At(i) is VReg.Call call)
            {
                string? result = call.Result is null ? null : "r" + call.Result.Id;
                foreach (string key in live.LiveOut(i))
                {
                    if (key != result && MapKey(function, key) is { } sym)
                    {
                        across.Add(sym);
                    }
                }
            }

            foreach (VReg.Op op in VRegFacts.Uses(live.At(i)))
            {
                if (op is VReg.Addr addr)
                {
                    across.Add(addr.Sym);
                }
                else if (op is VReg.Pinned pinned)
                {
                    across.Add(BaseSymbol(pinned.Sym));
                }
            }
        }

        return [.. function.Saved.Where(o => across.Contains(BaseSymbol(o.Sym)))];
    }

    private static string? MapKey(VReg.Function function, string key)
    {
        if (key.StartsWith('r'))
        {
            if (int.TryParse(key[1..], out int id) && function.Sym.TryGetValue(id, out string? sym))
            {
                return BaseSymbol(sym);
            }

            throw new CCodegenException($"vreg: rejestr syntezowany r{key[1..]} żyje przez wołanie w funkcji '{function.Name}'.");
        }

        return key.StartsWith('m') ? key[1..] : null;
    }

    private static string BaseSymbol(string symbol)
    {
        int plus = symbol.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? symbol : symbol[..plus];
    }

    private static void LowerBlock(VReg.Function function, VReg.Block block, string next, List<Ir.Ins> body)
    {
        var cmps = new Dictionary<int, VReg.Cmp>();
        foreach (VReg.Ins ins in block.Code)
        {
            if (ins is VReg.Cmp cmp)
            {
                cmps[cmp.Dst.Id] = cmp;
            }
        }

        foreach (VReg.Ins ins in block.Code)
        {
            switch (ins)
            {
                case VReg.Mov mov:
                    body.Add(new Ir.Mov(CellOf(function, mov.Dst), OpOf(function, mov.Src)));
                    break;
                case VReg.Bin bin:
                    body.Add(new Ir.Bin(bin.Kind, CellOf(function, bin.Dst), OpOf(function, bin.A), OpOf(function, bin.B)));
                    break;
                case VReg.Un un:
                    body.Add(new Ir.Un(un.Kind, CellOf(function, un.Dst), OpOf(function, un.A)));
                    break;
                case VReg.Load load:
                    body.Add(new Ir.Load(CellOf(function, load.Dst), OpOf(function, load.Ptr), load.Off, load.Bytes, load.Volatile));
                    break;
                case VReg.Store store:
                    body.Add(new Ir.Store(OpOf(function, store.Ptr), store.Off, OpOf(function, store.Value), store.Bytes, store.Volatile));
                    break;
                case VReg.CopyBlock copy:
                    body.Add(new Ir.CopyBlock(OpOf(function, copy.Dst), OpOf(function, copy.Src), copy.Size));
                    break;
                case VReg.Fill fill:
                    body.Add(new Ir.Fill(OpOf(function, fill.Dst), fill.Value, fill.Size));
                    break;
                case VReg.Cmp:
                    break;
                case VReg.Br br:
                    LowerBranch(function, block, br, cmps, next, body);
                    break;
                case VReg.Jmp jump:
                    body.Add(new Ir.Jmp(jump.Target));
                    break;
                case VReg.Call call:
                    body.Add(new Ir.Call(
                        call.Direct,
                        call.Indirect is null ? null : CellOf(function, call.Indirect),
                        [.. call.Args.Select(a => OpOf(function, a))],
                        call.ParamWidths,
                        call.Result is null ? null : CellOf(function, call.Result)));
                    break;
                case VReg.Ret ret:
                    body.Add(new Ir.Ret(ret.Value is null ? null : OpOf(function, ret.Value), ret.W));
                    break;
                case VReg.Src src:
                    body.Add(new Ir.Src(src.File, src.Line));
                    break;
                default:
                    throw new CCodegenException($"vreg: nieobsługiwana instrukcja '{ins.GetType().Name}'.");
            }
        }
    }

    private static void LowerBranch(VReg.Function function, VReg.Block block, VReg.Br br, Dictionary<int, VReg.Cmp> cmps, string next, List<Ir.Ins> body)
    {
        if (!cmps.TryGetValue(br.C.Id, out VReg.Cmp? cmp))
        {
            throw new CCodegenException($"vreg: rozgałęzienie na r{br.C.Id} bez porównania w bloku '{block.Label}'.");
        }

        Ir.Op a = OpOf(function, cmp.A);
        Ir.Op b = OpOf(function, cmp.B);
        if (br.Then == next)
        {
            body.Add(new Ir.BrCmp(Negate(cmp.C), a, b, br.Else));
        }
        else if (br.Else == next)
        {
            body.Add(new Ir.BrCmp(cmp.C, a, b, br.Then));
        }
        else
        {
            body.Add(new Ir.BrCmp(cmp.C, a, b, br.Then));
            body.Add(new Ir.Jmp(br.Else));
        }
    }

    private static Ir.Cond Negate(Ir.Cond cond) => cond switch
    {
        Ir.Cond.Eq => Ir.Cond.Ne,
        Ir.Cond.Ne => Ir.Cond.Eq,
        Ir.Cond.Lt => Ir.Cond.Ge,
        Ir.Cond.Le => Ir.Cond.Gt,
        Ir.Cond.Gt => Ir.Cond.Le,
        Ir.Cond.Ge => Ir.Cond.Lt,
        Ir.Cond.Ltu => Ir.Cond.Geu,
        Ir.Cond.Leu => Ir.Cond.Gtu,
        Ir.Cond.Gtu => Ir.Cond.Leu,
        _ => Ir.Cond.Ltu,
    };

    private static Ir.Cell CellOf(VReg.Function function, VReg.Reg reg) =>
        function.Sym.TryGetValue(reg.Id, out string? sym)
            ? new Ir.Cell(sym, reg.W)
            : throw new CCodegenException($"vreg: rejestr r{reg.Id} bez symbolu w funkcji '{function.Name}'.");

    private static Ir.Op OpOf(VReg.Function function, VReg.Op op) => op switch
    {
        VReg.Reg reg => CellOf(function, reg),
        VReg.Imm imm => new Ir.Imm(imm.Value, imm.W, imm.High),
        VReg.Addr addr => new Ir.AddrOf(addr.Sym, addr.Off),
        VReg.Pinned pinned => new Ir.Cell(pinned.Sym, pinned.W),
        _ => throw new CCodegenException($"vreg: nieobsługiwany operand '{op.GetType().Name}'."),
    };
}
