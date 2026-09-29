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

    private readonly List<Ir.Data> _temps = [];

    private readonly HashSet<string> _tempNames = new(StringComparer.Ordinal);

    private readonly HashSet<string> _used = new(StringComparer.Ordinal);

    private int _labels;

    private Legalizer(Ir.Module module) => _module = module;

    /// <summary>Zamienia operacje nieobsługiwane przez CPU na dozwolone.</summary>
    /// <param name="module">Moduł po przebiegach IR.</param>
    /// <returns>Moduł, w którym selektor bajtowy nie zobaczy operacji poza Mov/Add/Sub/And/Or/Xor/Neg/Cpl/Shl/Shr o stałą/Load/Store/BrCmp/Call/Ret.</returns>
    public static Ir.Module Run(Ir.Module module)
    {
        ArgumentNullException.ThrowIfNull(module);
        return new Legalizer(module).Apply();
    }

    private static Ir.Module CompileRuntime()
    {
        CheckedProgram program = TypeChecker.Check(Parser.Parse(StdLib.Portable("rt.c"), StdLib.HeaderReader));
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
        AddRuntime(functions, data);
        data.AddRange(_temps);
        return _module with { Functions = functions, Data = data };
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
            if (!seen.Add(name) || !library.TryGetValue(name, out Ir.Function? function))
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
            case Ir.Bin { Kind: Ir.BinOp.Mul } bin:
                CallBinary("__cc_mul", bin, bin.A, bin.B, output);
                break;
            case Ir.Bin { Kind: Ir.BinOp.Div } bin:
                CallBinary("__cc_divu", bin, bin.A, bin.B, output);
                break;
            case Ir.Bin { Kind: Ir.BinOp.Mod } bin:
                CallBinary("__cc_modu", bin, bin.A, bin.B, output);
                break;
            case Ir.Bin { Kind: Ir.BinOp.DivS } bin:
                CallBinary("__cc_divs", bin, SignExtend(bin.A, 0, output), SignExtend(bin.B, 1, output), output);
                break;
            case Ir.Bin { Kind: Ir.BinOp.ModS } bin:
                CallBinary("__cc_mods", bin, SignExtend(bin.A, 0, output), SignExtend(bin.B, 1, output), output);
                break;
            case Ir.Bin { Kind: Ir.BinOp.Sar } bin:
                CallShift("__cc_sar", bin, SignExtend(bin.A, 0, output), output);
                break;
            case Ir.Bin { Kind: Ir.BinOp.Shl or Ir.BinOp.Shr } bin when bin.B is not Ir.Imm:
                CallShift(bin.Kind == Ir.BinOp.Shl ? "__cc_shl" : "__cc_shr", bin, bin.A, output);
                break;
            case Ir.CopyBlock copy:
                RewriteCopy(copy, output);
                break;
            case Ir.Fill fill:
                RewriteFill(fill, output);
                break;
            default:
                output.Add(ins);
                break;
        }
    }

    private void CallBinary(string name, Ir.Bin bin, Ir.Op a, Ir.Op b, List<Ir.Ins> output)
    {
        _used.Add(name);
        Ir.Cell result = bin.Dst.W == 2 ? bin.Dst : Temp(2, 2);
        output.Add(new Ir.Call(name, null, [a, b], [2, 2], result));
        if (result != bin.Dst)
        {
            output.Add(new Ir.Mov(bin.Dst, result));
        }
    }

    private void CallShift(string name, Ir.Bin bin, Ir.Op value, List<Ir.Ins> output)
    {
        _used.Add(name);
        Ir.Cell result = bin.Dst.W == 2 ? bin.Dst : Temp(2, 2);
        output.Add(new Ir.Call(name, null, [value, bin.B], [2, 1], result));
        if (result != bin.Dst)
        {
            output.Add(new Ir.Mov(bin.Dst, result));
        }
    }

    /// <summary>Operand jako 16-bitowa liczba ze znakiem wg szerokości źródła (bajt: rozszerzenie znakiem).</summary>
    private Ir.Op SignExtend(Ir.Op op, int slot, List<Ir.Ins> output)
    {
        if (WidthOf(op) == 2)
        {
            return op;
        }

        if (op is Ir.Imm imm)
        {
            int value = imm.Value & 0xFF;
            return new Ir.Imm(value >= 0x80 ? value | 0xFF00 : value, 2);
        }

        Ir.Cell wide = Temp(2, slot);
        string done = NewLabel();
        output.Add(new Ir.Mov(wide, op));
        output.Add(new Ir.BrCmp(Ir.Cond.Ltu, op, new Ir.Imm(0x80, 1), done));
        output.Add(new Ir.Bin(Ir.BinOp.Or, wide, wide, new Ir.Imm(0xFF00, 2)));
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
