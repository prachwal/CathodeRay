namespace CathodeRay.C;

/// <summary>Podnoszenie Cell IR do rejestrów wirtualnych: każda skalarna komórka funkcji dostaje własny rejestr
/// (klucz: pełny symbol, więc połówki <c>x+2</c> to osobne rejestry), agregaty i symbole nielokalne przechodzą jako
/// <see cref="VReg.Pinned"/>, adresy jako <see cref="VReg.Addr"/>. <c>BrCmp</c> rozpada się na parę
/// <c>Cmp + Br</c> (spadek dostaje syntezowaną etykietę, gdy nie ma jawnej), etykiety dzielą ciało na bloki.
/// Odwrotność: <see cref="VRegToCell"/>.</summary>
internal static class VRegLift
{
    /// <summary>Podnosi moduł Cell IR do rejestrów wirtualnych (1:1, bez optymalizacji).</summary>
    /// <param name="module">Moduł po <see cref="Codegen.Lower"/>.</param>
    /// <returns>Moduł VReg.</returns>
    public static VReg.Module Run(Ir.Module module)
    {
        ArgumentNullException.ThrowIfNull(module);
        return new VReg.Module(
            [.. module.Functions.Select(LiftFunction)],
            module.Data,
            module.ExternFunctions,
            module.ExternCells,
            module.ObjectMode,
            module.Volatile);
    }

    private static VReg.Function LiftFunction(Ir.Function function)
    {
        var ctx = new Ctx();
        foreach (Ir.Owned owned in function.Saved.Where(static o => o.Aggregate))
        {
            ctx.AddPinned(BaseSymbol(owned.Sym));
        }

        var paramRegs = new List<VReg.Reg>(function.Params.Count);
        foreach (Ir.Cell param in function.Params)
        {
            paramRegs.Add(new VReg.Reg(ctx.RegOf(param), param.W));
        }

        // Formowanie bloków: cięcie na etykietach oraz po skoku/powrocie/rozgałęzieniu,
        // gdy następna instrukcja nie ma etykiety (spadek dostaje syntezowaną).
        var bounds = new List<(string Label, bool Explicit, List<Ir.Ins> Code)> { ("__entry", false, []) };
        foreach (Ir.Ins ins in function.Body)
        {
            if (ins is Ir.Label l)
            {
                bounds.Add((l.Name, true, []));
                continue;
            }

            bounds[^1].Code.Add(ins);
            if (ins is Ir.Jmp or Ir.Ret or Ir.BrCmp)
            {
                bounds.Add(($"__b{bounds.Count}", false, []));
            }
        }

        if (bounds[^1].Code.Count == 0 && !bounds[^1].Explicit)
        {
            bounds.RemoveAt(bounds.Count - 1);
        }

        var blocks = new List<VReg.Block>(bounds.Count);
        for (int b = 0; b < bounds.Count; b++)
        {
            string fallthrough = b + 1 < bounds.Count ? bounds[b + 1].Label : "__end";
            var code = new List<VReg.Ins>(bounds[b].Code.Count + 1);
            foreach (Ir.Ins ins in bounds[b].Code)
            {
                LiftIns(ins, ctx, code, fallthrough);
            }

            blocks.Add(new VReg.Block(bounds[b].Label, bounds[b].Explicit, code));
        }

        List<Ir.Owned> aggregates = function.Saved.Where(static o => o.Aggregate).ToList();
        return new VReg.Function(function.Name, function.IsStatic, paramRegs, ctx.Sym, aggregates, function.Saved, function.RetW, blocks);
    }

    private static void LiftIns(Ir.Ins ins, Ctx ctx, List<VReg.Ins> code, string fallthrough)
    {
        switch (ins)
        {
            case Ir.Mov mov:
                code.Add(new VReg.Mov(new VReg.Reg(ctx.RegOf(mov.Dst), mov.Dst.W), ctx.LiftOp(mov.Src)));
                break;
            case Ir.Bin bin:
                code.Add(new VReg.Bin(bin.Kind, new VReg.Reg(ctx.RegOf(bin.Dst), bin.Dst.W), ctx.LiftOp(bin.A), ctx.LiftOp(bin.B)));
                break;
            case Ir.Un un:
                code.Add(new VReg.Un(un.Kind, new VReg.Reg(ctx.RegOf(un.Dst), un.Dst.W), ctx.LiftOp(un.A)));
                break;
            case Ir.Load load:
                code.Add(new VReg.Load(new VReg.Reg(ctx.RegOf(load.Dst), load.Dst.W), ctx.LiftOp(load.Ptr), load.Off, load.Bytes, load.Volatile));
                break;
            case Ir.Store store:
                code.Add(new VReg.Store(ctx.LiftOp(store.Ptr), store.Off, ctx.LiftOp(store.Value), store.Bytes, store.Volatile));
                break;
            case Ir.CopyBlock copy:
                code.Add(new VReg.CopyBlock(ctx.LiftOp(copy.Dst), ctx.LiftOp(copy.Src), copy.Size));
                break;
            case Ir.Fill fill:
                code.Add(new VReg.Fill(ctx.LiftOp(fill.Dst), fill.Value, fill.Size));
                break;
            case Ir.BrCmp branch:
                int cmp = ctx.Fresh();
                code.Add(new VReg.Cmp(branch.C, new VReg.Reg(cmp, 1), ctx.LiftOp(branch.A), ctx.LiftOp(branch.B)));
                code.Add(new VReg.Br(new VReg.Reg(cmp, 1), branch.Target, fallthrough));
                break;
            case Ir.Jmp jump:
                code.Add(new VReg.Jmp(jump.Target));
                break;
            case Ir.Call call:
                code.Add(new VReg.Call(
                    call.Direct,
                    call.Indirect is null ? null : new VReg.Reg(ctx.RegOf(call.Indirect), call.Indirect.W),
                    [.. call.Args.Select(ctx.LiftOp)],
                    call.ParamWidths,
                    call.Result is null ? null : new VReg.Reg(ctx.RegOf(call.Result), call.Result.W)));
                break;
            case Ir.Ret ret:
                code.Add(new VReg.Ret(ret.Value is null ? null : ctx.LiftOp(ret.Value), ret.W));
                break;
            case Ir.Src src:
                code.Add(new VReg.Src(src.File, src.Line));
                break;
            default:
                throw new CCodegenException($"vreg: nieobsługiwana instrukcja '{ins.GetType().Name}'.");
        }
    }

    private static string BaseSymbol(string symbol)
    {
        int plus = symbol.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? symbol : symbol[..plus];
    }

    /// <summary>Stan numeracji rejestrów jednej funkcji.</summary>
    private sealed class Ctx
    {
        private readonly Dictionary<string, int> _ids = new(StringComparer.Ordinal);

        private readonly Dictionary<int, string> _sym = new();

        private readonly HashSet<string> _pinned = new(StringComparer.Ordinal);

        private int _next;

        /// <summary>Mapa rejestr → symbol.</summary>
        public Dictionary<int, string> Sym => _sym;

        /// <summary>Dokłada bazę agregatu (pamięć, nie rejestr).</summary>
        /// <param name="base">Symbol bazowy.</param>
        public void AddPinned(string @base) => _pinned.Add(@base);

        /// <summary>Świeży numer rejestru syntezowanego (bez symbolu).</summary>
        /// <returns>Numer.</returns>
        public int Fresh() => _next++;

        /// <summary>Numer rejestru komórki (nadaje przy pierwszym użyciu).</summary>
        /// <param name="cell">Komórka.</param>
        /// <returns>Numer rejestru.</returns>
        public int RegOf(Ir.Cell cell)
        {
            if (_pinned.Contains(BaseSymbol(cell.Sym)))
            {
                throw new CCodegenException($"vreg: zapis do agregatu '{cell.Sym}' nie jest rejestrem.");
            }

            if (!_ids.TryGetValue(cell.Sym, out int id))
            {
                id = _next++;
                _ids[cell.Sym] = id;
                _sym[id] = cell.Sym;
            }

            return id;
        }

        /// <summary>Podnosi operand.</summary>
        /// <param name="op">Operand Cell IR.</param>
        /// <returns>Operand VReg.</returns>
        public VReg.Op LiftOp(Ir.Op op) => op switch
        {
            Ir.Cell cell when _pinned.Contains(BaseSymbol(cell.Sym)) => new VReg.Pinned(cell.Sym, cell.W),
            Ir.Cell cell => new VReg.Reg(RegOf(cell), cell.W),
            Ir.Imm imm => new VReg.Imm(imm.Value, imm.W, imm.High),
            Ir.AddrOf addr => new VReg.Addr(addr.Sym, addr.Off),
            _ => throw new CCodegenException($"vreg: nieobsługiwany operand '{op.GetType().Name}'."),
        };
    }
}
