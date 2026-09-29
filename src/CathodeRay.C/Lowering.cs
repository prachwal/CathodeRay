using System.Globalization;

namespace CathodeRay.C;

/// <summary>Obniżanie programu po kontroli typów do kodu pośredni (<see cref="Ir"/>): zmienne i tymczasowe to komórki,
/// a każde wyrażenie zwraca operand (komórkę, stałą albo adres). Wybór instrukcji, ABI i rozmieszczenie
/// pamięci należą do celu. Ramki (callee-saves), kontrola stosu i wykrywanie rekurencji są tu, bo nie zależą od CPU.</summary>
internal sealed partial class Lowering
{
    /// <summary>Największa lokalna tablica/struktura funkcji rekurencyjnej (zapisywana bajt po bajcie na stosie).</summary>
    private const int MaxSavedAggregate = 64;

    private readonly List<Ir.Function> _functionsOut = [];
    private readonly List<Pending> _pending = [];

    private readonly List<Ir.Data> _dataOut = [];

    private readonly Dictionary<string, VarCell> _cells = new(StringComparer.Ordinal);

    private readonly Dictionary<string, CheckedFunction> _functions = new(StringComparer.Ordinal);

    private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal);

    private readonly Dictionary<string, CType> _globalsByName = new(StringComparer.Ordinal);

    private readonly Stack<(string Break, string Continue)> _loopLabels = new();

    private readonly List<Ir.Owned> _extraOwned = [];

    private readonly List<TypedSymbol> _runtimeInits = [];

    private readonly HashSet<string> _localSymbols = new(StringComparer.Ordinal);

    private readonly List<string> _externCells = [];

    private readonly Dictionary<string, int> _frames = new(StringComparer.Ordinal);

    private readonly Dictionary<string, HashSet<string>> _calls = new(StringComparer.Ordinal);

    private readonly CheckedProgram _program;

    private readonly string? _file;

    private readonly bool _objectMode;

    private readonly int? _stackLimit;

    private List<Ir.Ins> _body = [];

    private Dictionary<Ast.Expr, CType> _types = new(ReferenceEqualityComparer.Instance);

    private IReadOnlyDictionary<Ast.Expr, int> _constants = new Dictionary<Ast.Expr, int>();

    private IReadOnlyDictionary<Ast.Node, int> _lines = new Dictionary<Ast.Node, int>();

    private IReadOnlyList<TypedSymbol> _globals = [];

    private string _prefix = string.Empty;

    private int _labels;

    private int _maxTemp = -1;

    private int _switches;

    private CheckedFunction? _current;
    private Ir.Function? _initFunction;

    public Lowering(CheckedProgram program, string? fileName, bool objectMode, int? stackLimit)
    {
        _program = program;
        _file = fileName;
        _objectMode = objectMode;
        _stackLimit = stackLimit;
    }

    /// <summary>Obniża cały program.</summary>
    /// <returns>Moduł IR.</returns>
    public Ir.Module Run()
    {
        CheckedProgram program = _program;
        _globals = program.Globals;
        foreach (TypedSymbol g in program.Globals)
        {
            _globalsByName[g.Name] = g.Type;
        }

        _constants = program.Constants ?? _constants;
        _types = new Dictionary<Ast.Expr, CType>(program.GlobalTypes ?? new Dictionary<Ast.Expr, CType>(), ReferenceEqualityComparer.Instance);
        _lines = program.Lines;
        foreach (CheckedFunction function in program.Functions)
        {
            _functions[function.Def.Name] = function;
        }

        foreach (TypedSymbol global in program.Globals)
        {
            string label = GlobalLabel(global.Name);
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

        foreach (CheckedFunction function in program.Functions)
        {
            if (!function.Def.IsExtern)
            {
                LowerFunction(function);
            }
        }

        if (_runtimeInits.Count > 0)
        {
            LowerGlobalInit(program);
            _dataOut.Add(new Ir.Data(string.Empty, "INIT", 2, [new Ir.SymWord("__cc_init", 0)], false));
        }

        FinalizeFrames();

        CheckStack(program.Warnings, _stackLimit);
        string[] externFunctions =
        [
            .. program.Functions
                .Where(static f => f.Def.IsExtern)
                .Select(static f => f.Def.Name)
                .Where(name => !program.Functions.Any(f => !f.Def.IsExtern && f.Def.Name == name))
                .Distinct(StringComparer.Ordinal),
        ];
        return new Ir.Module(_functionsOut, _dataOut, externFunctions, _externCells, _objectMode);
    }

    private static string GlobalLabel(string name) => $"cc_g_{name}";

    private static int Width(CType type) => type.Kind == "uchar" ? 1 : 2;

    private static bool IsWide(CType type) => type.Kind is "int" or "uint" or "ptr" or "fptr";

    private static bool TryNumber(string text, out int value) =>
        text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
            : int.TryParse(text, out value);

    /// <summary>Usuwa <c>Jmp L</c> tuż przed <c>L:</c> (pomijając znaczniki linii).</summary>
    private static List<Ir.Ins> DropJumpsToNext(List<Ir.Ins> body)
    {
        var result = new List<Ir.Ins>(body.Count);
        for (int i = 0; i < body.Count; i++)
        {
            if (body[i] is Ir.Jmp jump)
            {
                int next = i + 1;
                while (next < body.Count && body[next] is Ir.Src)
                {
                    next++;
                }

                if (next < body.Count && body[next] is Ir.Label label && label.Name == jump.Target)
                {
                    continue;
                }
            }

            result.Add(body[i]);
        }

        return result;
    }

    private string TempSym(int depth) => $"{_prefix}__t@{depth}";

    private bool IsTemp(Ir.Cell cell) => cell.Sym.Contains("__t@", StringComparison.Ordinal);

    private string Label(string hint) => $"L{++_labels}_{hint}";

    private void Emit(Ir.Ins ins) => _body.Add(ins);

    private CType TypeOf(Ast.Expr expr) => _types.TryGetValue(expr, out CType? type) ? type : CType.UChar;

    private int WidthOf(Ast.Expr expr) => Width(TypeOf(expr));

    private string KindOf(Ast.Expr expr) => TypeOf(expr).Kind;

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

    private void Comment(Ast.Node node)
    {
        if (_lines.TryGetValue(node, out int line))
        {
            Emit(new Ir.Src(_file, line));
        }
    }

    private Ir.Cell Temp(int depth, int width)
    {
        _maxTemp = Math.Max(_maxTemp, depth);
        return new Ir.Cell(TempSym(depth), width);
    }

    private void AddBss(string sym, int size, bool exported = false) =>
        _dataOut.Add(new Ir.Data(sym, "BSS", size, null, exported));

    private string StringLabel(string value)
    {
        if (!_strings.TryGetValue(value, out string? label))
        {
            label = $"{_prefix}__s@{_strings.Count}";
            _strings[value] = label;
            byte[] bytes = [.. value.Select(static ch => ch <= byte.MaxValue ? (byte)ch : throw new CCodegenException("string char above 255.")), 0];
            _dataOut.Add(new Ir.Data(label, "DATA", bytes.Length, [new Ir.Bytes(bytes)], false));
        }

        return label;
    }

    private void LowerFunction(CheckedFunction function)
    {
        _current = function;
        _prefix = function.Def.Name;
        _types = new Dictionary<Ast.Expr, CType>(function.Types, ReferenceEqualityComparer.Instance);
        _cells.Clear();
        foreach (TypedSymbol global in _globals)
        {
            _cells[global.Name] = new VarCell(GlobalLabel(global.Name), global.Type);
        }

        var saved = new List<Ir.Owned>();
        var aggregates = new List<Ir.Owned>();
        var parameters = new List<Ir.Cell>();
        foreach (TypedSymbol param in function.Params)
        {
            var cell = new VarCell($"{_prefix}__{param.Name}", param.Type);
            _cells[param.Name] = cell;
            AddBss(cell.Sym, Width(param.Type));
            saved.Add(new Ir.Owned(cell.Sym, Width(param.Type), false));
            parameters.Add(new Ir.Cell(cell.Sym, Width(param.Type)));
        }

        foreach (TypedSymbol local in function.Locals)
        {
            var cell = new VarCell($"{_prefix}__{local.Name}", local.Type);
            _cells[local.Name] = cell;
            if (local.Flags.HasFlag(DeclFlags.Static))
            {
                _localSymbols.Add(cell.Sym);
                AddStorage(cell.Sym, local, global: false);
                continue;
            }

            if (local.Type.Kind is "array" or "struct")
            {
                AddBss(cell.Sym, local.Type.Size);
                aggregates.Add(new Ir.Owned(cell.Sym, local.Type.Size, true));
                continue;
            }

            AddBss(cell.Sym, Width(local.Type));
            saved.Add(new Ir.Owned(cell.Sym, Width(local.Type), false));
        }

        _maxTemp = -1;
        _extraOwned.Clear();
        _body = [];
        Comment(function.Def);
        foreach (Ast.Stmt item in function.Def.Body.Items)
        {
            LowerStmt(item);
        }

        for (int temp = 0; temp <= _maxTemp; temp++)
        {
            string sym = TempSym(temp);
            AddBss(sym, 2);
            saved.Add(new Ir.Owned(sym, 2, false));
        }

        saved.AddRange(_extraOwned);
        int retW = function.Def.ReturnType == "void" && function.Def.ReturnStars == 0 ? 0 : function.Def.ReturnType == "uchar" && function.Def.ReturnStars == 0 ? 1 : 2;
        _pending.Add(new Pending(new Ir.Function(function.Def.Name, function.Def.IsStatic, parameters, retW, [], IrPasses.Optimize(DropJumpsToNext(_body))), saved, aggregates));
        _current = null;
    }

    /// <summary>Kod startowy globali z inicjalizatorem niestałym: funkcja <c>__cc_init</c> (lokalna w module),
    /// wpisana do tablicy w segmencie INIT; crt0 woła wszystkie po wyzerowaniu BSS.</summary>
    private void LowerGlobalInit(CheckedProgram program)
    {
        _prefix = "__cc_init";
        _types = new Dictionary<Ast.Expr, CType>(program.GlobalTypes ?? new Dictionary<Ast.Expr, CType>(), ReferenceEqualityComparer.Instance);
        _cells.Clear();
        foreach (TypedSymbol global in _globals)
        {
            _cells[global.Name] = new VarCell(GlobalLabel(global.Name), global.Type);
        }

        _maxTemp = -1;
        _extraOwned.Clear();
        _body = [];
        foreach (TypedSymbol global in _runtimeInits)
        {
            AssignVariable(global.Name, global.Init!, 0);
        }

        for (int temp = 0; temp <= _maxTemp; temp++)
        {
            AddBss(TempSym(temp), 2);
        }

        _initFunction = new Ir.Function("__cc_init", true, [], 0, [], IrPasses.Optimize(DropJumpsToNext(_body)));
    }

    private sealed record VarCell(string Sym, CType Type);

    private sealed record Pending(Ir.Function Function, List<Ir.Owned> Scalars, List<Ir.Owned> Aggregates);
}
