namespace CathodeRay.C;

/// <summary>Przebieg IR → IR dla celów akumulatorowych bez mnożenia, dzielenia i przesunięć o zmienną liczbę pozycji:
/// zamienia te operacje (oraz kopiowanie i wypełnianie bloków) na małe ciągi IR albo wołania procedur z
/// <c>stdlib/portable/rt.c</c> (mini-C kompilowane tym samym frontendem, dołączane do modułu jako funkcje lokalne).</summary>
internal sealed class Legalizer
{
    /// <summary>Największy blok kopiowany/wypełniany bez pętli.</summary>
    private const int InlineBlock = 6;

    private static readonly Lazy<Ir.Module> Runtime = new(CompileRuntime);

    private readonly Ir.Module _module;

    private readonly bool _wide;

    private readonly List<Ir.Data> _temps = [];

    private readonly HashSet<string> _tempNames = new(StringComparer.Ordinal);

    private readonly HashSet<string> _used = new(StringComparer.Ordinal);

    private int _labels;

    private Legalizer(Ir.Module module, bool wide)
    {
        _module = module;
        _wide = wide;
    }

    /// <summary>Zamienia operacje nieobsługiwane przez CPU na dozwolone.</summary>
    /// <param name="module">Moduł po przebiegach IR.</param>
    /// <param name="wide"><see langword="true"/>: tylko operacje 32-bitowe (przed <see cref="WideLegalizer"/>); <see langword="false"/>:
    /// operacje 8- i 16-bitowe oraz bloki (po nim).</param>
    /// <returns>Moduł, w którym selektor bajtowy nie zobaczy operacji poza Mov/Add/Sub/And/Or/Xor/Neg/Cpl/Shl/Shr o stałą/Load/Store/BrCmp/Call/Ret.</returns>
    public static Ir.Module Run(Ir.Module module, bool wide = false)
    {
        ArgumentNullException.ThrowIfNull(module);
        return new Legalizer(module, wide).Apply();
    }

    private static Ir.Module CompileRuntime()
    {
        CheckedProgram program = TypeChecker.Check(Parser.Parse(StdLib.RuntimeSource, StdLib.HeaderReader));
        return Codegen.Lower(program, "rt.c", objectMode: false, stackLimit: null);
    }

    private static int WidthOf(Ir.Op op) => op switch
    {
        Ir.Cell cell => cell.W,
        Ir.Imm imm => imm.W,
        _ => 2,
    };

    private static IEnumerable<string> References(Ir.Function function)
    {
        foreach (Ir.Cell p in function.Params)
        {
            yield return p.Sym;
        }

        foreach (Ir.Owned owned in function.Saved)
        {
            yield return owned.Sym;
        }

        foreach (Ir.Ins ins in function.Body)
        {
            foreach (Ir.Op op in Operands(ins))
            {
                switch (op)
                {
                    case Ir.Cell cell:
                        yield return cell.Sym;
                        break;
                    case Ir.AddrOf address:
                        yield return address.Sym;
                        break;
                }
            }

            if (ins is Ir.Call { Direct: { } name })
            {
                yield return name;
            }
        }
    }

    private static IEnumerable<Ir.Op> Operands(Ir.Ins ins)
    {
        switch (ins)
        {
            case Ir.Mov mov:
                return [mov.Dst, mov.Src];
            case Ir.Bin bin:
                return [bin.Dst, bin.A, bin.B];
            case Ir.Un un:
                return [un.Dst, un.A];
            case Ir.Load load:
                return [load.Dst, load.Ptr];
            case Ir.Store store:
                return [store.Ptr, store.Value];
            case Ir.CopyBlock copy:
                return [copy.Dst, copy.Src];
            case Ir.Fill fill:
                return [fill.Dst];
            case Ir.BrCmp branch:
                return [branch.A, branch.B];
            case Ir.Call call:
                return [.. call.Args, .. call.Indirect is null ? [] : new Ir.Op[] { call.Indirect }, .. call.Result is null ? [] : new Ir.Op[] { call.Result }];
            case Ir.Ret { Value: { } value }:
                return [value];
            default:
                return [];
        }
    }

    private Ir.Module Apply()
    {
        var functions = new List<Ir.Function>();
        foreach (Ir.Function function in _module.Functions)
        {
            var body = new List<Ir.Ins>();
            foreach (Ir.Ins ins in function.Body)
            {
                Rewrite(ins, body);
            }

            functions.Add(function with { Body = body });
        }

        List<Ir.Data> data = [.. _module.Data];
        IReadOnlyList<string> externs = _module.ExternFunctions;
        if (_module.ObjectMode)
        {
            // moduł obiektowy woła procedury z osobnego modułu rt.c (linkowanego raz na żądanie), zamiast nosić ich kopie
            string[] missing = [.. _used.Where(name => functions.All(f => f.Name != name) && !externs.Contains(name)).Order(StringComparer.Ordinal)];
            externs = [.. externs, .. missing];
        }
        else
        {
            AddRuntime(functions, data);
        }

        data.AddRange(_temps);
        return _module with { Functions = functions, Data = data, ExternFunctions = externs };
    }

    private void AddRuntime(List<Ir.Function> functions, List<Ir.Data> data)
    {
        if (_used.Count == 0)
        {
            return;
        }

        Dictionary<string, Ir.Function> library = Runtime.Value.Functions.ToDictionary(static f => f.Name, StringComparer.Ordinal);
        var included = new List<Ir.Function>();
        var pending = new Queue<string>(_used);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            string name = pending.Dequeue();
            if (!seen.Add(name) || !library.TryGetValue(name, out Ir.Function? function) || functions.Any(f => f.Name == name))
            {
                continue;
            }

            included.Add(function with { IsStatic = true });
            foreach (string reference in References(function))
            {
                symbols.Add(reference);
                pending.Enqueue(reference);
            }
        }

        functions.AddRange(included);
        data.AddRange(Runtime.Value.Data.Where(d => symbols.Contains(d.Sym)).Select(static d => d with { Exported = false }));
    }

    private Ir.Cell Temp(int width, int slot)
    {
        string name = $"__lg{slot}_{width}";
        if (_tempNames.Add(name))
        {
            _temps.Add(new Ir.Data(name, "BSS", width, null, false));
        }

        return new Ir.Cell(name, width);
    }

    private string NewLabel() => $"__lg{++_labels}";

    private void Rewrite(Ir.Ins ins, List<Ir.Ins> output)
    {
        switch (ins)
        {
            case Ir.Bin { Kind: Ir.BinOp.Mul } bin when Handles(bin.Dst):
                CallBinary(_wide ? "__cc_mul32" : "__cc_mul", bin, bin.A, bin.B, output);
                break;
            case Ir.Bin { Kind: Ir.BinOp.Div } bin when Handles(bin.Dst):
                CallBinary(_wide ? "__cc_divu32" : "__cc_divu", bin, bin.A, bin.B, output);
                break;
            case Ir.Bin { Kind: Ir.BinOp.Mod } bin when Handles(bin.Dst):
                CallBinary(_wide ? "__cc_modu32" : "__cc_modu", bin, bin.A, bin.B, output);
                break;
            case Ir.Bin { Kind: Ir.BinOp.DivS } bin when Handles(bin.Dst):
                CallBinary(_wide ? "__cc_divs32" : "__cc_divs", bin, SignExtend(bin.A, 0, output), SignExtend(bin.B, 1, output), output);
                break;
            case Ir.Bin { Kind: Ir.BinOp.ModS } bin when Handles(bin.Dst):
                CallBinary(_wide ? "__cc_mods32" : "__cc_mods", bin, SignExtend(bin.A, 0, output), SignExtend(bin.B, 1, output), output);
                break;
            case Ir.Bin { Kind: Ir.BinOp.Sar } bin when Handles(bin.Dst):
                if (_wide && bin.B is Ir.Imm)
                {
                    output.Add(bin with { A = SignExtend(bin.A, 0, output) });
                }
                else
                {
                    CallShift(_wide ? "__cc_sar32" : "__cc_sar", bin, SignExtend(bin.A, 0, output), output);
                }

                break;
            case Ir.Bin { Kind: Ir.BinOp.Shl or Ir.BinOp.Shr } bin when Handles(bin.Dst) && bin.B is not Ir.Imm:
                CallShift(bin.Kind == Ir.BinOp.Shl ? (_wide ? "__cc_shl32" : "__cc_shl") : (_wide ? "__cc_shr32" : "__cc_shr"), bin, bin.A, output);
                break;
            case Ir.CopyBlock copy when !_wide:
                RewriteCopy(copy, output);
                break;
            case Ir.Fill fill when !_wide:
                RewriteFill(fill, output);
                break;
            default:
                output.Add(ins);
                break;
        }
    }

    /// <summary>Czy ta faza zajmuje się operacją o takim wyniku (32-bitowe w fazie szerokiej, pozostałe w wąskiej).</summary>
    private bool Handles(Ir.Cell dst) => _wide == (dst.W == 4);

    private void CallBinary(string name, Ir.Bin bin, Ir.Op a, Ir.Op b, List<Ir.Ins> output)
    {
        _used.Add(name);
        int width = _wide ? 4 : 2;
        Ir.Cell result = bin.Dst.W == width ? bin.Dst : Temp(width, 2);
        output.Add(new Ir.Call(name, null, [a, b], [width, width], result));
        if (result != bin.Dst)
        {
            output.Add(new Ir.Mov(bin.Dst, result));
        }
    }

    private void CallShift(string name, Ir.Bin bin, Ir.Op value, List<Ir.Ins> output)
    {
        _used.Add(name);
        int width = _wide ? 4 : 2;
        Ir.Cell result = bin.Dst.W == width ? bin.Dst : Temp(width, 2);
        output.Add(new Ir.Call(name, null, [value, bin.B], [width, 1], result));
        if (result != bin.Dst)
        {
            output.Add(new Ir.Mov(bin.Dst, result));
        }
    }

    /// <summary>Operand jako liczba ze znakiem w szerokości fazy (16 albo 32 bity) wg szerokości źródła (bajt, słowo).</summary>
    private Ir.Op SignExtend(Ir.Op op, int slot, List<Ir.Ins> output)
    {
        int target = _wide ? 4 : 2;
        int source = WidthOf(op);
        if (source == target)
        {
            return op;
        }

        if (op is Ir.Imm imm)
        {
            int value = source == 1 ? (sbyte)imm.Value : (short)imm.Value;
            return new Ir.Imm(target == 2 ? value & 0xFFFF : value, target);
        }

        Ir.Cell wide = Temp(target, slot);
        string done = NewLabel();
        output.Add(new Ir.Mov(wide, op));
        output.Add(new Ir.BrCmp(Ir.Cond.Ltu, op, new Ir.Imm(source == 1 ? 0x80 : 0x8000, source), done));
        output.Add(new Ir.Bin(Ir.BinOp.Or, wide, wide, new Ir.Imm(unchecked((int)(source == 1 ? (target == 2 ? 0xFF00u : 0xFFFFFF00u) : 0xFFFF0000u)), target)));
        output.Add(new Ir.Label(done));
        return wide;
    }

    private void RewriteCopy(Ir.CopyBlock copy, List<Ir.Ins> output)
    {
        if (copy.Size > InlineBlock)
        {
            _used.Add("__cc_copy");
            output.Add(new Ir.Call("__cc_copy", null, [copy.Dst, copy.Src, new Ir.Imm(copy.Size, 2)], [2, 2, 2], null));
            return;
        }

        Ir.Cell temp = Temp(1, 0);
        for (int i = 0; i < copy.Size; i++)
        {
            output.Add(new Ir.Load(temp, copy.Src, i, 1));
            output.Add(new Ir.Store(copy.Dst, i, temp, 1));
        }
    }

    private void RewriteFill(Ir.Fill fill, List<Ir.Ins> output)
    {
        if (fill.Size > InlineBlock)
        {
            _used.Add("__cc_fill");
            output.Add(new Ir.Call("__cc_fill", null, [fill.Dst, new Ir.Imm(fill.Value & 0xFF, 1), new Ir.Imm(fill.Size, 2)], [2, 1, 2], null));
            return;
        }

        for (int i = 0; i < fill.Size; i++)
        {
            output.Add(new Ir.Store(fill.Dst, i, new Ir.Imm(fill.Value & 0xFF, 1), 1));
        }
    }
}
