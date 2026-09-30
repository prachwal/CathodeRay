namespace CathodeRay.C;

/// <summary>Przebiegi optymalizujące na rejestrach wirtualnych (niezależne od celu): lokalne CSE wyrażeń czystych,
/// zwijanie kopii i usuwanie martwych rejestrów przez dokładną żywość (<see cref="VRegLiveness"/>).
/// Rejestry są prywatne funkcji (pamięć widzą tylko load/store), więc nie trzeba reguły lokalności jak w
/// <see cref="IrPasses"/>; <c>Cmp</c> nie podlega CSE, bo <see cref="VRegToCell"/> wymaga definicji porównania.</summary>
internal static class VRegPasses
{
    /// <summary>Uruchamia wszystkie przebiegi na funkcji.</summary>
    /// <param name="function">Funkcja.</param>
    /// <param name="volatiles">Symbole <c>volatile</c>: odczyty z nich zostają.</param>
    /// <returns>Funkcja po optymalizacji.</returns>
    public static VReg.Function Run(VReg.Function function, IReadOnlySet<string>? volatiles = null)
    {
        ArgumentNullException.ThrowIfNull(function);
        var blocks = new List<VReg.Block>(function.Blocks.Count);
        foreach (VReg.Block block in function.Blocks)
        {
            blocks.Add(block with { Code = Coalesce(Number(block)) });
        }

        return RemoveDead(function with { Blocks = blocks }, volatiles);
    }

    private static string OpKey(VReg.Op op, Dictionary<int, int> numbers) => op switch
    {
        VReg.Reg reg => "v" + (numbers.TryGetValue(reg.Id, out int n) ? n : -reg.Id - 1) + ":" + reg.W,
        VReg.Imm imm => $"c{imm.Value},{imm.W},{imm.High}",
        VReg.Addr addr => $"a{addr.Sym}+{addr.Off}",
        VReg.Pinned pinned => $"p{pinned.Sym},{pinned.W}",
        _ => "?",
    };

    /// <summary>Lokalne CSE: powtórzone czyste wyrażenie w bloku staje się kopią pierwszego wyniku.</summary>
    private static List<VReg.Ins> Number(VReg.Block block)
    {
        var numbers = new Dictionary<int, int>();
        var exprs = new Dictionary<string, (int Reg, int W, int[] Deps)>(StringComparer.Ordinal);
        var widths = new Dictionary<int, int>();
        int fresh = 0;
        var code = new List<VReg.Ins>(block.Code.Count);

        int Num(int id)
        {
            if (!numbers.TryGetValue(id, out int n))
            {
                n = fresh++;
                numbers[id] = n;
            }

            return n;
        }

        void Kill(int id)
        {
            int old = Num(id);
            foreach (string key in exprs.Where(e => e.Value.Reg == id || e.Value.Deps.Contains(old)).Select(static e => e.Key).ToList())
            {
                exprs.Remove(key);
            }

            numbers[id] = fresh++;
        }

        foreach (VReg.Ins ins in block.Code)
        {
            VReg.Ins current = ins;
            string? key = ins switch
            {
                VReg.Mov mov when mov.Src is VReg.Reg => "mov(" + OpKey(mov.Src, numbers) + ")",
                VReg.Bin bin => $"bin{(int)bin.Kind}({OpKey(bin.A, numbers)},{OpKey(bin.B, numbers)})",
                VReg.Un un => $"un{(int)un.Kind}({OpKey(un.A, numbers)})",
                _ => null,
            };
            if (key is not null && VRegFacts.Def(ins) is { } dst && exprs.TryGetValue(key, out (int Reg, int W, int[] Deps) found) && found.Reg != dst.Id && found.W == dst.W)
            {
                widths.TryGetValue(found.Reg, out int w);
                current = new VReg.Mov(dst, new VReg.Reg(found.Reg, w == 0 ? dst.W : w));
            }

            if (VRegFacts.Def(current) is { } defined)
            {
                Kill(defined.Id);
                widths[defined.Id] = defined.W;
                if (key is not null && current == ins)
                {
                    int[] deps = [.. VRegFacts.Uses(ins).OfType<VReg.Reg>().Select(r => Num(r.Id))];
                    exprs[key] = (defined.Id, defined.W, deps);
                }
            }

            code.Add(current);
        }

        return code;
    }

    private static VReg.Op SubOp(VReg.Op op, int dst, VReg.Reg src) =>
        op is VReg.Reg reg && reg.Id == dst ? src : op;

    private static VReg.Ins SubUses(VReg.Ins ins, int dst, VReg.Reg src) => ins switch
    {
        VReg.Mov mov => mov with { Src = SubOp(mov.Src, dst, src) },
        VReg.Bin bin => bin with { A = SubOp(bin.A, dst, src), B = SubOp(bin.B, dst, src) },
        VReg.Un un => un with { A = SubOp(un.A, dst, src) },
        VReg.Load load => load with { Ptr = SubOp(load.Ptr, dst, src) },
        VReg.Store store => store with { Ptr = SubOp(store.Ptr, dst, src), Value = SubOp(store.Value, dst, src) },
        VReg.CopyBlock copy => copy with { Dst = SubOp(copy.Dst, dst, src), Src = SubOp(copy.Src, dst, src) },
        VReg.Fill fill => fill with { Dst = SubOp(fill.Dst, dst, src) },
        VReg.Cmp cmp => cmp with { A = SubOp(cmp.A, dst, src), B = SubOp(cmp.B, dst, src) },
        VReg.Br br when br.C.Id == dst => br with { C = src },
        VReg.Call call => call with
        {
            Indirect = call.Indirect is not null && call.Indirect.Id == dst ? src : call.Indirect,
            Args = [.. call.Args.Select(a => SubOp(a, dst, src))],
        },
        VReg.Ret ret when ret.Value is not null => ret with { Value = SubOp(ret.Value, dst, src) },
        _ => ins,
    };

    /// <summary>Zwijanie kopii: <c>d = s</c> podstawia <c>s</c> za użycia <c>d</c>, dopóki żaden z nich nie jest zapisany.</summary>
    private static List<VReg.Ins> Coalesce(List<VReg.Ins> code)
    {
        var result = new List<VReg.Ins>(code.Count);
        for (int i = 0; i < code.Count; i++)
        {
            result.Add(code[i]);
            if (code[i] is VReg.Mov { Src: VReg.Reg src } mov && mov.Dst.W == src.W)
            {
                for (int j = i + 1; j < code.Count; j++)
                {
                    if (VRegFacts.Def(code[j]) is { } def && (def.Id == mov.Dst.Id || def.Id == src.Id))
                    {
                        break;
                    }

                    code[j] = SubUses(code[j], mov.Dst.Id, src);
                }
            }
        }

        return result;
    }

    private static bool KeepLoad(VReg.Load load, IReadOnlySet<string>? volatiles)
    {
        if (load.Volatile)
        {
            return true;
        }

        string? sym = load.Ptr switch
        {
            VReg.Pinned pinned => VRegFacts.BaseSymbol(pinned.Sym),
            VReg.Addr addr => addr.Sym,
            _ => null,
        };
        return sym is not null && volatiles?.Contains(sym) == true;
    }

    /// <summary>Usuwa definicje rejestrów, których nikt nie czyta (do braku zmian); wołania, zapisy i skoki zostają.</summary>
    private static VReg.Function RemoveDead(VReg.Function function, IReadOnlySet<string>? volatiles)
    {
        while (true)
        {
            VRegLiveness live = VRegLiveness.Of(function);
            (List<VReg.Ins> flat, _) = VRegLiveness.Flatten(function);
            var dead = new HashSet<int>();
            for (int i = 0; i < flat.Count; i++)
            {
                if (VRegFacts.Def(flat[i]) is { } def && !live.LiveOut(i).Contains("r" + def.Id))
                {
                    bool keep = flat[i] is VReg.Call || (flat[i] is VReg.Load load && KeepLoad(load, volatiles));
                    if (!keep)
                    {
                        dead.Add(i);
                    }
                }
            }

            if (dead.Count == 0)
            {
                return function;
            }

            var blocks = new List<VReg.Block>(function.Blocks.Count);
            int index = 0;
            foreach (VReg.Block block in function.Blocks)
            {
                var code = new List<VReg.Ins>(block.Code.Count);
                foreach (VReg.Ins ins in block.Code)
                {
                    if (!dead.Contains(index))
                    {
                        code.Add(ins);
                    }

                    index++;
                }

                blocks.Add(block with { Code = code });
            }

            function = function with { Blocks = blocks };
        }
    }
}
