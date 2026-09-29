using System.Text;

namespace CathodeRay.C;

/// <summary>Generator kodu stub z programu po kontroli typów: tekst asemblera
/// (segment CODE z funkcjami w <c>.proc</c>, segment DATA z komórkami).
/// Konwencja wg <c>docs/stub-calling-conv.md</c>: A = wartość/arg1/wynik
/// (int: A=lo, X=hi), X roboczy w wyrażeniach, zmienne jako komórki absolutne.
/// Dwa inty nie mieszczą się w (A,X): arg2 idzie przez umówione komórki
/// <c>cc_arg2</c>/<c>cc_arg2_h</c> (caller kopiuje tuż przed CALL, callee
/// odczytuje w prologu — bezpieczne przy zagnieżdżeniu).
/// Ramki (plan 20): callee-saves — prolog chowa wejście do cc_arg1(_h),
/// PUSHuje własne komórki (parametry, lokale, tempy), potem storuje parametry;
/// epilog chowa wynik do cc_ret(_h), POPuje, odtwarza A/X, RET. Globale
/// współdzielone. Rekurencja działa (limit: strona stosu 01xxh).
/// Argumenty: 1. w A(+X dla int), 2. bajtowy przy 1. bajtowym w X, reszta w
/// <c>cc_arg2</c>..<c>cc_arg6</c> (max <see cref="TypeChecker.MaxArgs"/>).
/// int = 16 bit ze znakiem (porównania, <c>/</c>, <c>%</c>, <c>&gt;&gt;</c>); <c>*</c>,<c>/</c>,<c>%</c>
/// przez <c>cc_mul16</c>/<c>cc_div16</c>. Literały: znak = uchar, napis = <c>uchar*</c> (DATA).</summary>
public sealed partial class Codegen
{
    /// <summary>Największa lokalna tablica/struktura funkcji rekurencyjnej (zapisywana bajt po bajcie na stosie).</summary>
    private const int MaxSavedAggregate = 64;

    private readonly List<(string Name, CType Type, byte[]? Init)> _data = [];

    private readonly Dictionary<string, Cell> _cells = new(StringComparer.Ordinal);

    private readonly Dictionary<string, CheckedFunction> _functions = new(StringComparer.Ordinal);

    private readonly List<(string Label, string Symbol)> _words = [];

    private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal);

    private readonly Stack<(string Break, string Continue)> _loopLabels = new();

    private readonly HashSet<string> _wordGlobals = new(StringComparer.Ordinal);

    private readonly Dictionary<string, CType> _globalsByName = new(StringComparer.Ordinal);

    private readonly List<string> _extraCells = [];
    private readonly List<TypedSymbol> _runtimeInits = [];
    private readonly HashSet<string> _localSymbols = new(StringComparer.Ordinal);
    private readonly List<string> _externCells = [];
    private readonly Dictionary<string, Dictionary<int, string>> _dataSymbols = new(StringComparer.Ordinal);

    private readonly Dictionary<string, int> _frames = new(StringComparer.Ordinal);

    private readonly Dictionary<string, HashSet<string>> _calls = new(StringComparer.Ordinal);

    private int _switches;
    private int _assignOps;
    private HashSet<string> _recursive = [];
    private IReadOnlyDictionary<Ast.Expr, int> _constants = new Dictionary<Ast.Expr, int>();

    private IReadOnlyDictionary<Ast.Node, int> _lines = new Dictionary<Ast.Node, int>();

    private string? _file;

    private int _addrs;

    private StringBuilder _code = new();

    private IReadOnlyList<TypedSymbol> _globals = [];

    private int _labels;

    private int _maxTemp = -1;

    private bool _needMul;

    private bool _needDiv;

    private bool _needMul16;

    private bool _needDiv16;

    private bool _wideCells;

    private string _prefix = string.Empty;

    private Dictionary<Ast.Expr, CType> _types = new(ReferenceEqualityComparer.Instance);

    private Codegen()
    {
    }

    /// <summary>Generuje tekst asemblera.</summary>
    /// <param name="program">Program po kontroli typów.</param>
    /// <param name="fileName">Nazwa pliku C do adnotacji <c>;c:</c> (null = sama linia).</param>
    /// <param name="objectMode">Tryb obiektowy (linker): emituje <c>.extern</c> dla prototypów.</param>
    /// <returns>Źródło dla <c>cathode asm --cpu stub</c> (bez wpisu: start zapewnia
    /// crt0 z <see cref="Crt0"/>, linkowany zawsze pierwszy).</returns>
    public static string Emit(CheckedProgram program, string? fileName = null, bool objectMode = false)
    {
        ArgumentNullException.ThrowIfNull(program);
        var gen = new Codegen();
        return gen.Run(program, fileName, objectMode);
    }

    private static string Swap(string op) => op switch
    {
        "<" => ">",
        "<=" => ">=",
        ">" => "<",
        ">=" => "<=",
        _ => op,
    };

    private static bool TryNumber(string text, out int value) =>
        text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value)
            : int.TryParse(text, out value);

    private static bool TryConst(Ast.Expr expr, out int value)
    {
        if (expr is Ast.Number number && TryNumber(number.Text, out value))
        {
            value = (short)value;
            return true;
        }

        value = 0;
        return false;
    }

    private static bool IsWide(CType type) => type.Kind is "int" or "ptr";

    private static int ElemSize(CType type) => type.Kind == "uchar" ? 1 : 2;

    /// <summary>Spłaszcza inicjalizator tablicy/struktury do zapisów skalarów (przesunięcie, typ, wartość).</summary>
    private static void CollectInit(CType type, Ast.Expr init, int offset, List<(int Offset, CType Type, Ast.Expr Value)> entries)
    {
        switch (type.Kind)
        {
            case "array" when init is Ast.Str str:
                string text = str.Value + '\0';
                for (int i = 0; i < text.Length; i++)
                {
                    entries.Add((offset + i, CType.UChar, new Ast.Number(((int)text[i]).ToString(System.Globalization.CultureInfo.InvariantCulture))));
                }

                break;
            case "array":
                var items = ((Ast.InitList)init).Items;
                for (int i = 0; i < items.Count; i++)
                {
                    CollectInit(type.Base!, items[i], offset + (i * type.Base!.Size), entries);
                }

                break;
            case "struct":
                var values = ((Ast.InitList)init).Items;
                for (int i = 0; i < values.Count; i++)
                {
                    StructField field = type.Info!.Fields[i];
                    CollectInit(field.Type, values[i], offset + field.Offset, entries);
                }

                break;
            default:
                entries.Add((offset, type, init));
                break;
        }
    }

    private static string WithOffset(string symbol, int offset) =>
        offset == 0 ? symbol : $"{symbol}{(offset > 0 ? "+" : "-")}{Math.Abs(offset)}";

    /// <summary>Dane z adresami w środku: <c>.byte</c> dla bajtów i <c>.word symbol</c> dla wskaźników.</summary>
    private static void AppendPieces(StringBuilder data, string name, byte[] init, Dictionary<int, string> symbols)
    {
        string label = $"{name}: ";
        var run = new List<byte>();
        void FlushBytes()
        {
            if (run.Count > 0)
            {
                data.AppendLine($"{label}.byte {string.Join(", ", run)}");
                label = string.Empty;
                run.Clear();
            }
        }

        for (int i = 0; i < init.Length; i++)
        {
            if (symbols.TryGetValue(i, out string? symbol))
            {
                FlushBytes();
                data.AppendLine($"{label}.word {symbol}");
                label = string.Empty;
                i++;
                continue;
            }

            run.Add(init[i]);
        }

        FlushBytes();
    }

    /// <summary>Komórki wejściowe parametru <paramref name="i"/>: 1. w <c>cc_arg1</c>(+<c>_h</c>),
    /// 2. bajtowy przy 1. bajtowym w <c>cc_arg1_h</c> (rejestr X), reszta w <c>cc_arg{i+1}</c>.</summary>
    private static (string Lo, string? Hi) ArgCells(IReadOnlyList<TypedSymbol> ps, int i)
    {
        bool wide = IsWide(ps[i].Type);
        if (i == 1 && !IsWide(ps[0].Type) && !wide)
        {
            return ("cc_arg1_h", null);
        }

        string lo = $"cc_arg{i + 1}";
        return (lo, wide ? $"{lo}_h" : null);
    }

    /// <summary>Wartość stałego wyrażenia: literał albo węzeł, który checker policzył jako stałą.</summary>
    private bool TryConstValue(Ast.Expr? expr, out int value)
    {
        value = 0;
        if (expr is Ast.Number number && TryNumber(number.Text, out int parsed))
        {
            value = parsed & 0xFFFF;
            return true;
        }

        return expr is not null && _constants.TryGetValue(expr, out value);
    }

    private byte[]? InitBytes(string label, TypedSymbol symbol)
    {
        if (symbol.Init is null)
        {
            return null;
        }

        if (symbol.Type.Kind is "array" or "struct")
        {
            return AggregateBytes(label, symbol);
        }

        if (TryConstValue(symbol.Init, out int value))
        {
            return symbol.Type.Size == 1 ? [(byte)(value & 0xFF)] : [(byte)(value & 0xFF), (byte)((value >> 8) & 0xFF)];
        }

        throw new CCodegenException($"initializer of '{symbol.Name}' must be a constant.");
    }

    private byte[] AggregateBytes(string label, TypedSymbol symbol)
    {
        var bytes = new byte[symbol.Type.Size];
        var entries = new List<(int Offset, CType Type, Ast.Expr Value)>();
        CollectInit(symbol.Type, symbol.Init!, 0, entries);
        Dictionary<int, string>? symbols = null;
        foreach ((int offset, CType type, Ast.Expr expr) in entries)
        {
            if (!TryConstValue(expr, out int value))
            {
                if (type.Kind == "ptr" && SymbolInit(expr) is { } address)
                {
                    (symbols ??= [])[offset] = address;
                    continue;
                }

                throw new CCodegenException($"initializer of '{symbol.Name}' must be constant.");
            }

            bytes[offset] = (byte)(value & 0xFF);
            if (type.Size == 2)
            {
                bytes[offset + 1] = (byte)((value >> 8) & 0xFF);
            }
        }

        if (symbols is not null)
        {
            _dataSymbols[label] = symbols;
        }

        return bytes;
    }

    /// <summary>Adres jako inicjalizator globalnego wskaźnika: napis, <c>&amp;g</c>, <c>&amp;g.f</c>, <c>&amp;a[2]</c>,
    /// nazwa tablicy lub pole-tablica, ewentualnie z przesunięciem stałą (<c>tab + 2</c>, skalowaną rozmiarem elementu).</summary>
    private string? SymbolInit(Ast.Expr? init)
    {
        switch (init)
        {
            case Ast.Str str:
                return StringLabel(str.Value);
            case Ast.AddressOf address:
                return $"cc_g_{address.Name}";
            case Ast.AddressOfExpr addressOf when GlobalLvalue(addressOf.Target) is var (symbol, offset):
                return WithOffset(symbol, offset);
            case Ast.Var variable when _globalsByName.TryGetValue(variable.Name, out CType? type) && type.Kind == "array":
                return $"cc_g_{variable.Name}";
            case Ast.Member member when _types.TryGetValue(member.Base, out _) && FieldOf(member).Type.Kind == "array" && GlobalLvalue(member) is var (fieldSymbol, fieldOffset):
                return WithOffset(fieldSymbol, fieldOffset);
            case Ast.Binary { Op: "+" or "-" } binary when SymbolInit(binary.Left) is { } baseSymbol && TryConstValue(binary.Right, out int offset):
                int delta = (short)offset * PointeeSize(binary.Left) * (binary.Op == "-" ? -1 : 1);
                return delta == 0 ? baseSymbol : $"{baseSymbol}{(delta > 0 ? "+" : "-")}{Math.Abs(delta)}";
            default:
                return null;
        }
    }

    /// <summary>Symbol i przesunięcie lwartości opartej o globalną zmienną i stałe indeksy/pola.</summary>
    private (string Symbol, int Offset)? GlobalLvalue(Ast.Expr expr)
    {
        switch (expr)
        {
            case Ast.Var variable when _globalsByName.ContainsKey(variable.Name):
                return ($"cc_g_{variable.Name}", 0);
            case Ast.Index index when GlobalLvalue(index.Base) is var (symbol, offset) && TryConstValue(index.Offset, out int position) && _types.TryGetValue(index, out CType? elem):
                return (symbol, offset + ((short)position * elem.Size));
            case Ast.Member { Arrow: false } member when GlobalLvalue(member.Base) is var (baseSymbol, baseOffset) && _types.ContainsKey(member.Base):
                return (baseSymbol, baseOffset + FieldOf(member).Offset);
            default:
                return null;
        }
    }

    private int PointeeSize(Ast.Expr pointer) =>
        _types.TryGetValue(pointer, out CType? type) && type.Base is not null ? type.Base.Size : 1;

    /// <summary>Kod startowy globali z inicjalizatorem niestałym: procedura <c>__cc_init</c> (lokalna w module),
    /// wpisana do tablicy w segmencie INIT; crt0 woła wszystkie po wyzerowaniu BSS.</summary>
    private void EmitGlobalInit(CheckedProgram program)
    {
        _prefix = "__cc_init";
        _types = new Dictionary<Ast.Expr, CType>(program.GlobalTypes ?? new Dictionary<Ast.Expr, CType>(), ReferenceEqualityComparer.Instance);
        _cells.Clear();
        foreach (TypedSymbol global in _globals)
        {
            _cells[global.Name] = new Cell($"cc_g_{global.Name}", global.Type);
        }

        _maxTemp = -1;
        _addrs = -1;
        _extraCells.Clear();
        StringBuilder outer = _code;
        var body = new StringBuilder();
        _code = body;
        try
        {
            foreach (TypedSymbol global in _runtimeInits)
            {
                Store(global.Name, global.Init!, 0);
            }
        }
        finally
        {
            _code = outer;
        }

        for (int temp = 0; temp <= _maxTemp; temp++)
        {
            DataCell($"{_prefix}__t{temp}", CType.UChar);
            DataCell($"{_prefix}__t{temp}_h", CType.UChar);
        }

        _code.AppendLine(".proc __cc_init");
        _code.Append(body.ToString());
        _code.AppendLine("RET");
        _code.AppendLine(".endproc");
    }

    /// <summary>Miejsce na zmienną globalną lub <c>static</c>: bajty (DATA/BSS), adres jako <c>.word</c> albo
    /// (tylko globale) kod startowy dla inicjalizatora niestałego.</summary>
    private void AddStorage(string label, TypedSymbol symbol, bool global)
    {
        bool aggregate = symbol.Type.Kind is "array" or "struct";
        bool tableInit = aggregate && symbol.Init is Ast.InitList or Ast.Str;
        if (symbol.Init is not null && !tableInit && !TryConstValue(symbol.Init, out _))
        {
            if (symbol.Type.Kind == "ptr" && SymbolInit(symbol.Init) is { } address)
            {
                _words.Add((label, address));
                _wordGlobals.Add(label);
            }
            else if (global)
            {
                DataCell(label, symbol.Type);
                _runtimeInits.Add(symbol);
            }
            else
            {
                throw new CCodegenException($"static '{symbol.Name}' needs a constant initializer.");
            }

            return;
        }

        DataCell(label, symbol.Type, InitBytes(label, symbol));
    }

    private string Hi(string lo) => _wordGlobals.Contains(lo) || lo.StartsWith("cc_g_", StringComparison.Ordinal) ? $"{lo}+1" : $"{lo}_h";

    private string Run(CheckedProgram program, string? fileName, bool objectMode)
    {
        _globals = program.Globals;
        foreach (TypedSymbol g in program.Globals)
        {
            _globalsByName[g.Name] = g.Type;
        }

        _constants = program.Constants ?? _constants;
        _recursive = RecursiveFunctions(program);
        _types = new Dictionary<Ast.Expr, CType>(program.GlobalTypes ?? new Dictionary<Ast.Expr, CType>(), ReferenceEqualityComparer.Instance);
        _lines = program.Lines;
        _file = fileName;
        foreach (CheckedFunction function in program.Functions)
        {
            _functions[function.Def.Name] = function;
        }

        foreach (TypedSymbol global in program.Globals)
        {
            string label = $"cc_g_{global.Name}";
            if (global.Flags.HasFlag(DeclFlags.Extern))
            {
                _externCells.Add(label);
                continue;
            }

            if (global.Flags.HasFlag(DeclFlags.Static))
            {
                _localSymbols.Add(label);
            }

            AddStorage(label, global, global: true);
        }

        _code.AppendLine(".segment \"CODE\"");
        if (objectMode)
        {
            foreach (CheckedFunction function in program.Functions)
            {
                if (function.Def.IsExtern)
                {
                    _code.AppendLine($".extern {function.Def.Name}");
                }
            }

            foreach (string external in _externCells)
            {
                _code.AppendLine($".extern {external}");
            }

            _code.AppendLine(".extern cc_arg1");
            _code.AppendLine(".extern cc_arg1_h");
            for (int arg = 2; arg <= TypeChecker.MaxArgs; arg++)
            {
                _code.AppendLine($".extern cc_arg{arg}");
                _code.AppendLine($".extern cc_arg{arg}_h");
            }

            _code.AppendLine(".extern cc_ret");
            _code.AppendLine(".extern cc_ret_h");
        }

        foreach (CheckedFunction function in program.Functions)
        {
            if (!function.Def.IsExtern)
            {
                EmitFunction(function);
            }
        }

        if (_runtimeInits.Count > 0)
        {
            EmitGlobalInit(program);
        }

        if (_needMul)
        {
            EmitMul();
        }

        if (_needDiv)
        {
            EmitDiv();
        }

        if (_needMul16)
        {
            EmitMul16();
        }

        if (_needDiv16)
        {
            EmitDiv16();
            EmitSDiv16();
        }

        var initSegment = new StringBuilder();
        if (_runtimeInits.Count > 0 || !objectMode)
        {
            initSegment.AppendLine(".segment \"INIT\"");
            if (!objectMode)
            {
                initSegment.AppendLine("__init_start:");
            }

            if (_runtimeInits.Count > 0)
            {
                initSegment.AppendLine(".word __cc_init");
            }

            if (!objectMode)
            {
                initSegment.AppendLine("__init_end:");
            }
        }

        var data = new StringBuilder();
        data.AppendLine(".segment \"DATA\"");
        foreach ((string name, CType type, byte[]? init) in _data)
        {
            if (init is null)
            {
                continue;
            }

            if (name.StartsWith("cc_g_", StringComparison.Ordinal) && !_localSymbols.Contains(name))
            {
                data.AppendLine($".global {name}");
            }

            if (_dataSymbols.TryGetValue(name, out Dictionary<int, string>? symbols))
            {
                AppendPieces(data, name, init, symbols);
                continue;
            }

            int size = type.Size;
            if (IsWide(type) && init.Length == 2)
            {
                data.AppendLine($"{name}: .byte {init[0]}");
                data.AppendLine($"{name}_h: .byte {init[1]}");
            }
            else
            {
                data.Append(name).Append(": .byte ");
                data.AppendLine(string.Join(", ", init.Select(static b => b.ToString())));
            }
        }

        foreach ((string text, string strLabel) in _strings)
        {
            byte[] bytes = [.. text.Select(static ch => ch <= byte.MaxValue ? (byte)ch : throw new CCodegenException("string char above 255.")), 0];
            data.AppendLine($"{strLabel}: .byte {string.Join(", ", bytes)}");
        }

        foreach ((string label, string symbol) in _words)
        {
            if (label.StartsWith("cc_g_", StringComparison.Ordinal) && !_localSymbols.Contains(label))
            {
                data.AppendLine($".global {label}");
            }

            data.AppendLine($"{label}: .word {symbol}");
        }

        var bss = new StringBuilder();
        bss.AppendLine(".segment \"BSS\"");
        foreach ((string name, CType type, byte[]? init) in _data)
        {
            if (init is not null)
            {
                continue;
            }

            int size = type.Size;
            if (name.StartsWith("cc_g_", StringComparison.Ordinal) && !_localSymbols.Contains(name))
            {
                bss.AppendLine($".global {name}");
            }

            if (type.Kind is "array" or "struct")
            {
                for (int i = 0; i < size; i++)
                {
                    bss.AppendLine(i == 0 ? $"{name}: .byte 0" : ".byte 0");
                }
            }
            else if (size == 1)
            {
                bss.AppendLine($"{name}: .byte 0");
            }
            else
            {
                bss.AppendLine($"{name}: .byte 0");
                bss.AppendLine($"{name}_h: .byte 0");
            }
        }

        if (!objectMode)
        {
            bss.AppendLine("__bss_end:");
        }

        CheckStack(program.Warnings);
        return _code.ToString() + initSegment.ToString() + data.ToString() + bss.ToString();
    }

    private string Label(string hint) => $"L{++_labels}_{hint}";

    private void Comment(Ast.Node node)
    {
        if (_lines.TryGetValue(node, out int line))
        {
            _code.AppendLine(_file is null ? $";c:{line}" : $";c:{_file}:{line}");
        }
    }

    private void DataCell(string name, CType type, byte[]? init = null) =>
        _data.Add((name, type, init));

    private string Temp(int depth, bool hi)
    {
        _maxTemp = Math.Max(_maxTemp, depth);
        return hi ? $"{_prefix}__t{depth}_h" : $"{_prefix}__t{depth}";
    }

    private void EmitFunction(CheckedFunction function)
    {
        _prefix = function.Def.Name;
        _types = new Dictionary<Ast.Expr, CType>(function.Types, ReferenceEqualityComparer.Instance);
        _cells.Clear();
        foreach (TypedSymbol global in _globals)
        {
            _cells[global.Name] = new Cell($"cc_g_{global.Name}", global.Type);
        }

        var owned = new List<string>();
        foreach (TypedSymbol param in function.Params)
        {
            var cell = new Cell($"{_prefix}__{param.Name}", param.Type);
            _cells[param.Name] = cell;
            DataCell(cell.Lo, param.Type);
            owned.Add(cell.Lo);
            if (IsWide(param.Type))
            {
                owned.Add($"{cell.Lo}_h");
            }
        }

        foreach (TypedSymbol local in function.Locals)
        {
            var cell = new Cell($"{_prefix}__{local.Name}", local.Type);
            _cells[local.Name] = cell;
            if (local.Flags.HasFlag(DeclFlags.Static))
            {
                _localSymbols.Add(cell.Lo);
                AddStorage(cell.Lo, local, global: false);
                continue;
            }

            DataCell(cell.Lo, local.Type);
            if (local.Type.Kind is "array" or "struct")
            {
                if (_recursive.Contains(function.Def.Name))
                {
                    if (local.Type.Size > MaxSavedAggregate)
                    {
                        throw new CCodegenException($"recursive function '{function.Def.Name}' has local '{local.Name}' of {local.Type.Size} B (max {MaxSavedAggregate}).");
                    }

                    for (int i = 0; i < local.Type.Size; i++)
                    {
                        owned.Add(i == 0 ? cell.Lo : $"{cell.Lo}+{i}");
                    }
                }

                continue;
            }

            owned.Add(cell.Lo);
            if (IsWide(local.Type))
            {
                owned.Add($"{cell.Lo}_h");
            }
        }

        _maxTemp = -1;
        _addrs = -1;
        _extraCells.Clear();
        Comment(function.Def);
        _code.AppendLine($".proc {function.Def.Name}");
        if (!function.Def.IsStatic)
        {
            _code.AppendLine($".global {function.Def.Name}");
        }

        _code.AppendLine("STA cc_arg1");
        _code.AppendLine("TXA");
        _code.AppendLine("STA cc_arg1_h");

        StringBuilder outer = _code;
        var body = new StringBuilder();
        _code = body;
        try
        {
            EmitParamStores(function);
            foreach (Ast.Stmt item in function.Def.Body.Items)
            {
                EmitStmt(item);
            }
        }
        finally
        {
            _code = outer;
        }

        owned.AddRange(_extraCells);
        for (int temp = 0; temp <= _maxTemp; temp++)
        {
            DataCell($"{_prefix}__t{temp}", CType.UChar);
            DataCell($"{_prefix}__t{temp}_h", CType.UChar);
            owned.Add($"{_prefix}__t{temp}");
            owned.Add($"{_prefix}__t{temp}_h");
        }

        _frames[function.Def.Name] = owned.Count + 2;
        foreach (string cell in owned)
        {
            _code.AppendLine($"LDA {cell}");
            _code.AppendLine("PUSH");
        }

        _code.Append(body.ToString());
        _code.AppendLine($"{_prefix}__ret:");
        _code.AppendLine("STA cc_ret");
        _code.AppendLine("TXA");
        _code.AppendLine("STA cc_ret_h");
        for (int i = owned.Count - 1; i >= 0; i--)
        {
            _code.AppendLine("POP");
            _code.AppendLine($"STA {owned[i]}");
        }

        _code.AppendLine("LDA cc_ret");
        _code.AppendLine("LDA cc_ret_h");
        _code.AppendLine("TAX");
        _code.AppendLine("LDA cc_ret");
        _code.AppendLine("RET");
        _code.AppendLine(".endproc");
    }

    private void EmitParamStores(CheckedFunction function)
    {
        for (int i = 0; i < function.Params.Count; i++)
        {
            Cell cell = _cells[function.Params[i].Name];
            (string lo, string? hi) = ArgCells(function.Params, i);
            _code.AppendLine($"LDA {lo}");
            _code.AppendLine($"STA {cell.Lo}");
            if (hi is not null)
            {
                _code.AppendLine($"LDA {hi}");
                _code.AppendLine($"STA {cell.Lo}_h");
            }
        }
    }

    private (string Lo, CType Type) CellOf(string name) =>
        _cells.TryGetValue(name, out Cell? cell)
            ? (cell.Lo, cell.Type)
            : throw new CCodegenException($"unknown cell '{name}'.");

    private string KindOf(Ast.Expr expr) =>
        _types.TryGetValue(expr, out CType? type) ? type.Kind : "uchar";

    private bool IsWideKind(Ast.Expr expr) => KindOf(expr) is "int" or "ptr";

    private string StringLabel(string value)
    {
        if (!_strings.TryGetValue(value, out string? label))
        {
            label = $"{_prefix}__str{_strings.Count}";
            _strings[value] = label;
        }

        return label;
    }

    private bool ReturnsInt(Ast.Call call) =>
        _functions.TryGetValue(call.Name, out CheckedFunction? target)
        && (target.Def.ReturnType == "int" || target.Def.ReturnStars > 0);

    private sealed record Cell(string Lo, CType Type);
}
