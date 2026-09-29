using System.Text;

namespace CathodeRay.C;

/// <summary>Dobór instrukcji stuba dla kodu pośredniego: każda operacja IR to samodzielny ciąg instrukcji
/// pamięć-pamięć (akumulator A, indeks X=0 dla trybu <c>,X</c>, kod samomodyfikujący do dostępu przez wskaźnik).
/// Komórka 2-bajtowa to <c>sym</c> (młodszy) i <c>sym+1</c> (starszy).</summary>
internal sealed partial class StubSelector
{
    private readonly Ir.Module _module;

    private readonly StringBuilder _code = new();

    private readonly HashSet<string> _helperSymbols = new(StringComparer.Ordinal);

    private readonly Dictionary<string, string> _addressCells = new(StringComparer.Ordinal);

    private readonly List<(string Label, string Sym, int Off)> _addressList = [];

    private int _labels;

    public StubSelector(Ir.Module module) => _module = module;

    /// <summary>Składa asembler modułu.</summary>
    /// <param name="optimize">Uruchom <see cref="Peephole"/> na kodzie.</param>
    /// <returns>Tekst dla asemblera stuba.</returns>
    public string Emit(bool optimize)
    {
        foreach (Ir.Function function in _module.Functions)
        {
            EmitFunction(function);
        }

        string code = optimize ? Peephole.Optimize(_code.ToString()) : _code.ToString();
        return Header() + code + RuntimeText() + PrintInit() + PrintData() + PrintBss();
    }

    private static string CellHi(string sym) => $"{sym}+1";

    private static string At(string sym, int offset) =>
        offset == 0 ? sym : $"{sym}{(offset > 0 ? "+" : "-")}{Math.Abs(offset)}";

    private static (string Lo, string? Hi) ArgCells(IReadOnlyList<int> widths, int index)
    {
        bool wide = widths[index] == 2;
        if (index == 1 && widths[0] == 1 && !wide)
        {
            return ("cc_arg1_h", null);
        }

        string lo = $"cc_arg{index + 1}";
        return (lo, wide ? $"{lo}_h" : null);
    }

    private static IEnumerable<string> SavedBytes(Ir.Owned owned)
    {
        if (owned.Aggregate)
        {
            for (int i = 0; i < owned.Size; i++)
            {
                yield return At(owned.Sym, i);
            }

            yield break;
        }

        yield return owned.Sym;
        for (int i = 1; i < owned.Size; i++)
        {
            yield return At(owned.Sym, i);
        }
    }

    private static int WidthOf(Ir.Op op) => op switch
    {
        Ir.Cell cell => cell.W,
        Ir.Imm imm => imm.W,
        _ => 2,
    };

    private static void AppendData(StringBuilder text, Ir.Data data)
    {
        if (data.Exported)
        {
            text.AppendLine($".global {data.Sym}");
        }

        string label = data.Sym.Length == 0 ? string.Empty : $"{data.Sym}: ";
        foreach (Ir.Piece piece in data.Init!)
        {
            switch (piece)
            {
                case Ir.Bytes bytes:
                    text.AppendLine($"{label}.byte {string.Join(", ", bytes.Value)}");
                    break;
                case Ir.SymWord word:
                    text.AppendLine($"{label}.word {At(word.Sym, word.Off)}");
                    break;
            }

            label = string.Empty;
        }
    }

    private void Line(string text) => _code.AppendLine(text);

    private string Label(string hint) => $"S{++_labels}_{hint}";

    /// <summary>Komórka z adresem symbolu (<c>.word</c> w danych) — jedyny sposób na wczytanie adresu w stubie.</summary>
    private string AddressCell(Ir.AddrOf address)
    {
        string key = $"{address.Sym}|{address.Off}";
        if (!_addressCells.TryGetValue(key, out string? label))
        {
            label = $"__a@{_addressList.Count}";
            _addressCells[key] = label;
            _addressList.Add((label, address.Sym, address.Off));
        }

        return label;
    }

    private Octet ByteOf(Ir.Op op, int index) => op switch
    {
        Ir.Cell cell => index < cell.W ? new Octet(false, index == 0 ? cell.Sym : CellHi(cell.Sym)) : new Octet(true, "0"),
        Ir.Imm imm => new Octet(true, (index < imm.W ? (imm.Value >> (8 * index)) & 0xFF : 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
        Ir.AddrOf address => index < 2 ? new Octet(false, index == 0 ? AddressCell(address) : CellHi(AddressCell(address))) : new Octet(true, "0"),
        _ => throw new InvalidOperationException($"unsupported operand {op.GetType().Name}."),
    };

    private void LoadA(Octet value) => Line(value.IsImmediate ? $"LDI {value.Text}" : $"LDA {value.Text}");

    private void LoadA(Ir.Op op, int index) => LoadA(ByteOf(op, index));

    /// <summary>Działanie A = A op b dla ADD/ADC/AND/ORA/EOR (komórka wymaga X=0).</summary>
    private void Alu(string op, Octet value) => Line(value.IsImmediate ? $"{op} {value.Text}" : $"{op} {value.Text},X");

    private bool NeedsIndex(params Octet[] values) => values.Any(static v => !v.IsImmediate);

    /// <summary>A = wynik <paramref name="op"/> (SUB albo CPA) z operandem b; komórkę b łata w kodzie, bo stub
    /// nie ma odejmowania z pamięci. <paramref name="loadA"/> ładuje odjemną tuż przed instrukcją.</summary>
    private void SubOrCompare(string op, Octet value, Action loadA)
    {
        if (value.IsImmediate)
        {
            loadA();
            Line($"{op} {value.Text}");
            return;
        }

        string site = Label("patch");
        Line($"LDA {value.Text}");
        Line($"STA {site}+1");
        loadA();
        Line($"{site}: {op} 0");
    }

    private void EmitFunction(Ir.Function function)
    {
        if (function.Body.Count > 0 && function.Body[0] is Ir.Src leading)
        {
            EmitSource(leading);
        }

        Line($".proc {function.Name}");
        if (!function.IsStatic)
        {
            Line($".global {function.Name}");
        }

        Line("STA cc_arg1");
        Line("TXA");
        Line("STA cc_arg1_h");
        foreach (Ir.Owned owned in function.Saved)
        {
            foreach (string b in SavedBytes(owned))
            {
                Line($"LDA {b}");
                Line("PUSH");
            }
        }

        var widths = function.Params.Select(static p => p.W).ToList();
        for (int i = 0; i < function.Params.Count; i++)
        {
            (string lo, string? hi) = ArgCells(widths, i);
            Line($"LDA {lo}");
            Line($"STA {function.Params[i].Sym}");
            if (hi is not null)
            {
                Line($"LDA {hi}");
                Line($"STA {CellHi(function.Params[i].Sym)}");
            }
        }

        for (int i = function.Body.Count > 0 && function.Body[0] is Ir.Src ? 1 : 0; i < function.Body.Count; i++)
        {
            EmitIns(function, function.Body[i]);
        }

        Line($"{function.Name}__ret:");
        Line("STA cc_ret");
        Line("TXA");
        Line("STA cc_ret_h");
        foreach (Ir.Owned owned in function.Saved.Reverse())
        {
            foreach (string b in SavedBytes(owned).Reverse())
            {
                Line("POP");
                Line($"STA {b}");
            }
        }

        Line("LDA cc_ret");
        Line("LDA cc_ret_h");
        Line("TAX");
        Line("LDA cc_ret");
        Line("RET");
        Line(".endproc");
    }

    private void EmitSource(Ir.Src source) =>
        Line(source.File is null ? $";c:{source.Line}" : $";c:{source.File}:{source.Line}");

    private void EmitIns(Ir.Function function, Ir.Ins ins)
    {
        switch (ins)
        {
            case Ir.Src source:
                EmitSource(source);
                break;
            case Ir.Label label:
                Line($"{label.Name}:");
                break;
            case Ir.Jmp jump:
                Line($"JMP {jump.Target}");
                break;
            case Ir.Mov mov:
                EmitMov(mov);
                break;
            case Ir.Bin bin:
                EmitBin(bin);
                break;
            case Ir.Un un:
                EmitUn(un);
                break;
            case Ir.Load load:
                EmitLoad(load);
                break;
            case Ir.Store store:
                EmitStore(store);
                break;
            case Ir.CopyBlock copy:
                EmitCopy(copy);
                break;
            case Ir.Fill fill:
                EmitFill(fill);
                break;
            case Ir.BrCmp branch:
                EmitBranch(branch);
                break;
            case Ir.Call call:
                EmitCall(call);
                break;
            case Ir.Ret ret:
                EmitRet(function, ret);
                break;
            default:
                throw new InvalidOperationException($"StubSelector cannot select {ins.GetType().Name}.");
        }
    }

    private string Header()
    {
        var text = new StringBuilder();
        text.AppendLine(".segment \"CODE\"");
        if (!_module.ObjectMode)
        {
            return text.ToString();
        }

        foreach (string function in _module.ExternFunctions)
        {
            text.AppendLine($".extern {function}");
        }

        foreach (string external in _module.ExternCells)
        {
            text.AppendLine($".extern {external}");
        }

        text.AppendLine(".extern cc_arg1");
        text.AppendLine(".extern cc_arg1_h");
        for (int arg = 2; arg <= TypeChecker.MaxArgs; arg++)
        {
            text.AppendLine($".extern cc_arg{arg}");
            text.AppendLine($".extern cc_arg{arg}_h");
        }

        text.AppendLine(".extern cc_ret");
        text.AppendLine(".extern cc_ret_h");
        foreach (string symbol in _helperSymbols.Order(StringComparer.Ordinal))
        {
            text.AppendLine($".extern {symbol}");
        }

        return text.ToString();
    }

    /// <summary>Podprogramy mnożenia i dzielenia to moduły asemblerowe biblioteki (<c>rt_*.s</c>). W trybie obiektowym linkuje je
    /// <c>cc</c> na żądanie; gdy moduł jest całym programem, dołączamy tu te, których użyto.</summary>
    private string RuntimeText()
    {
        if (_module.ObjectMode || _helperSymbols.Count == 0)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        foreach (StdModule module in StdLib.Modules.Where(static m => m.IsAssembly && m.Name.StartsWith("rt", StringComparison.Ordinal)))
        {
            if (module.Defines.Any(_helperSymbols.Contains))
            {
                text.AppendLine(module.Source);
            }
        }

        return text.ToString();
    }

    /// <summary>Zapisuje, że kod woła symbol modułu wykonawczego (procedurę albo komórkę argumentów).</summary>
    private void UseHelper(params string[] symbols)
    {
        foreach (string symbol in symbols)
        {
            _helperSymbols.Add(symbol);
        }
    }

    private string PrintInit()
    {
        var text = new StringBuilder();
        Ir.Data[] table = [.. _module.Data.Where(static d => d.Segment == "INIT")];
        if (table.Length == 0 && _module.ObjectMode)
        {
            return string.Empty;
        }

        text.AppendLine(".segment \"INIT\"");
        if (!_module.ObjectMode)
        {
            text.AppendLine("__init_start:");
        }

        foreach (Ir.Data data in table)
        {
            AppendData(text, data);
        }

        if (!_module.ObjectMode)
        {
            text.AppendLine("__init_end:");
        }

        return text.ToString();
    }

    private string PrintData()
    {
        var text = new StringBuilder();
        text.AppendLine(".segment \"DATA\"");
        foreach (Ir.Data data in _module.Data.Where(static d => d.Segment == "DATA"))
        {
            AppendData(text, data);
        }

        foreach ((string label, string sym, int off) in _addressList)
        {
            text.AppendLine($"{label}: .word {At(sym, off)}");
        }

        return text.ToString();
    }

    private string PrintBss()
    {
        var text = new StringBuilder();
        text.AppendLine(".segment \"BSS\"");
        foreach (Ir.Data data in _module.Data.Where(static d => d.Segment == "BSS"))
        {
            if (data.Exported)
            {
                text.AppendLine($".global {data.Sym}");
            }

            text.AppendLine($"{data.Sym}: .res {data.Size}");
        }

        foreach (string scratch in new[] { "__x@0", "__x@1", "__p@0", "__p@1", "__p@2", "__p@3", "__c@0" })
        {
            text.AppendLine($"{scratch}: .res {(scratch[2] == 'x' ? 1 : 2)}");
        }

        if (!_module.ObjectMode)
        {
            text.AppendLine("__bss_end:");
        }

        return text.ToString();
    }

    /// <summary>Bajt operandu: stała (natychmiastowy) albo komórka pamięci.</summary>
    private readonly record struct Octet(bool IsImmediate, string Text);
}
