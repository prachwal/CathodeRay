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
    private readonly List<(string Name, CType Type, byte[]? Init)> _data = [];

    private readonly Dictionary<string, Cell> _cells = new(StringComparer.Ordinal);

    private readonly Dictionary<string, CheckedFunction> _functions = new(StringComparer.Ordinal);

    private readonly List<(string Label, string Symbol)> _words = [];

    private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal);

    private readonly Stack<(string Break, string Continue)> _loopLabels = new();

    private readonly HashSet<string> _wordGlobals = new(StringComparer.Ordinal);

    private readonly Dictionary<string, CType> _globalsByName = new(StringComparer.Ordinal);

    private readonly List<string> _switchCells = [];

    private readonly Dictionary<string, int> _frames = new(StringComparer.Ordinal);

    private readonly Dictionary<string, HashSet<string>> _calls = new(StringComparer.Ordinal);

    private int _switches;

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

    private IReadOnlyDictionary<Ast.Expr, CType> _types =
        new Dictionary<Ast.Expr, CType>(ReferenceEqualityComparer.Instance);

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

    private static byte[]? InitBytes(TypedSymbol symbol)
    {
        if (symbol.Init is null)
        {
            return null;
        }

        if (symbol.Type.Kind == "array")
        {
            return ArrayBytes(symbol);
        }

        if (symbol.Init is Ast.Number number && TryNumber(number.Text, out int value))
        {
            return symbol.Type.Size == 1 ? [(byte)(value & 0xFF)] : [(byte)(value & 0xFF), (byte)((value >> 8) & 0xFF)];
        }

        throw new CCodegenException($"initializer of '{symbol.Name}' must be a constant.");
    }

    private static byte[] ArrayBytes(TypedSymbol symbol)
    {
        CType elem = symbol.Type.Base!;
        var bytes = new byte[symbol.Type.Size];
        if (symbol.Init is Ast.Str str)
        {
            for (int i = 0; i < str.Value.Length; i++)
            {
                bytes[i] = str.Value[i] <= byte.MaxValue ? (byte)str.Value[i] : throw new CCodegenException("string char above 255.");
            }

            return bytes;
        }

        var items = ((Ast.InitList)symbol.Init!).Items;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] is not Ast.Number number || !TryNumber(number.Text, out int value))
            {
                throw new CCodegenException($"initializer of '{symbol.Name}' must be constant.");
            }

            if (elem.Size == 1)
            {
                bytes[i] = (byte)(value & 0xFF);
            }
            else
            {
                bytes[2 * i] = (byte)(value & 0xFF);
                bytes[(2 * i) + 1] = (byte)((value >> 8) & 0xFF);
            }
        }

        return bytes;
    }

    /// <summary>Adres jako inicjalizator globalnego wskaźnika: napis, <c>&amp;g</c> albo nazwa tablicy.</summary>
    private string? SymbolInit(Ast.Expr? init) => init switch
    {
        Ast.Str str => StringLabel(str.Value),
        Ast.AddressOf address => $"cc_g_{address.Name}",
        Ast.Var variable when _globalsByName.TryGetValue(variable.Name, out CType? type) && type.Kind == "array" => $"cc_g_{variable.Name}",
        _ => null,
    };

    private string Hi(string lo) => _wordGlobals.Contains(lo) ? $"{lo}+1" : $"{lo}_h";

    private string Run(CheckedProgram program, string? fileName, bool objectMode)
    {
        _globals = program.Globals;
        foreach (TypedSymbol g in program.Globals)
        {
            _globalsByName[g.Name] = g.Type;
        }

        _lines = program.Lines;
        _file = fileName;
        foreach (CheckedFunction function in program.Functions)
        {
            _functions[function.Def.Name] = function;
        }

        foreach (TypedSymbol global in program.Globals)
        {
            if (global.Type.Kind == "ptr" && SymbolInit(global.Init) is { } symbol)
            {
                _words.Add(($"cc_g_{global.Name}", symbol));
                _wordGlobals.Add($"cc_g_{global.Name}");
                continue;
            }

            DataCell($"cc_g_{global.Name}", global.Type, InitBytes(global));
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

        var data = new StringBuilder();
        data.AppendLine(".segment \"DATA\"");
        foreach ((string name, CType type, byte[]? init) in _data)
        {
            if (init is null)
            {
                continue;
            }

            if (name.StartsWith("cc_g_", StringComparison.Ordinal))
            {
                data.AppendLine($".global {name}");
            }

            int size = type.Size;
            if (size == 2 && init.Length == 2 && type.Kind != "array")
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
            if (_wordGlobals.Contains(label))
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
            if (name.StartsWith("cc_g_", StringComparison.Ordinal))
            {
                bss.AppendLine($".global {name}");
            }

            if (type.Kind == "array")
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
        return _code.ToString() + data.ToString() + bss.ToString();
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
        _types = function.Types;
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
            if (param.Type.Size == 2)
            {
                owned.Add($"{cell.Lo}_h");
            }
        }

        foreach (TypedSymbol local in function.Locals)
        {
            var cell = new Cell($"{_prefix}__{local.Name}", local.Type);
            _cells[local.Name] = cell;
            DataCell(cell.Lo, local.Type);
            owned.Add(cell.Lo);
            if (local.Type.Size == 2)
            {
                owned.Add($"{cell.Lo}_h");
            }
        }

        _maxTemp = -1;
        _addrs = -1;
        _switchCells.Clear();
        Comment(function.Def);
        _code.AppendLine($".proc {function.Def.Name}");
        _code.AppendLine($".global {function.Def.Name}");
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

        owned.AddRange(_switchCells);
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
        && target.Def.ReturnType == "int";

    private sealed record Cell(string Lo, CType Type);
}
