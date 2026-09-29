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

    private readonly HashSet<int> _wideTemps = [];

    private readonly HashSet<string> _volatileSyms = new(StringComparer.Ordinal);

    private readonly TargetByteOrder _byteOrder;

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

    private int _aggregateTemps;

    private bool _returnsStruct;

    private bool _usesReturnBuffer;

    private int _switches;

    private CheckedFunction? _current;
    private Ir.Function? _initFunction;

    public Lowering(CheckedProgram program, string? fileName, bool objectMode, int? stackLimit, TargetByteOrder byteOrder = TargetByteOrder.Little)
    {
        _byteOrder = byteOrder;
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
        return new Ir.Module(_functionsOut, _dataOut, externFunctions, _usesReturnBuffer ? [.. _externCells, ReturnBuffer] : _externCells, _objectMode, _volatileSyms);
    }

    private static string GlobalLabel(string name) => $"cc_g_{name}";

    private static int Width(CType type) => type.Kind is "uchar" or "schar" ? 1 : type.Kind is "long" or "ulong" or "float" ? 4 : 2;

    private static bool IsWide(CType type) => type.Kind is "int" or "uint" or "long" or "ulong" or "ptr" or "fptr";

    /// <summary>Wartość stałej 16-bitowej (nie 32-bitowej: te lądują w komórkach W=4 osobną ścieżką).</summary>
    private static bool TryNumber(string text, out int value)
    {
        value = 0;
        if (!Literal.TryParse(text, out Literal literal) || literal.IsLong)
        {
            return false;
        }

        value = (int)literal.Value;
        return true;
    }

    private static int ReturnWidth(Ast.Function def)
    {
        string bare = TypeQualifiers.Split(def.ReturnType, out _, out _);
        return def.ReturnStars > 0 ? 2
            : bare == "void" || bare.StartsWith("struct ", StringComparison.Ordinal) ? 0
            : bare is "uchar" or "schar" ? 1
            : bare is "long" or "ulong" or "float" ? 4
            : 2;
    }

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

    private static bool ReturnsStruct(Ast.Function def) =>
        def.ReturnStars == 0 && TypeQualifiers.Split(def.ReturnType, out _, out _).StartsWith("struct ", StringComparison.Ordinal);

    /// <summary>Tymczasowa struktura (wynik wywołania zwracającego strukturę): pamięć statyczna, zapisywana w ramce.</summary>
    private string NewAggregate(int size)
    {
        string sym = $"{_prefix}__agg@{_aggregateTemps++}";
        AddBss(sym, size);
        _extraOwned.Add(new Ir.Owned(sym, size, true));
        return sym;
    }

    private void MarkVolatile(VarCell cell)
    {
        CType type = cell.Type;
        while (type.Kind == "array" && type.Base is not null)
        {
            type = type.Base;
        }

        if (type.IsVolatile)
        {
            _volatileSyms.Add(cell.Sym);
        }
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
        if (width == 4)
        {
            _wideTemps.Add(depth);
        }

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
            MarkVolatile(_cells[global.Name]);
        }

        var saved = new List<Ir.Owned>();
        var aggregates = new List<Ir.Owned>();
        var parameters = new List<Ir.Cell>();
        var prologue = new List<Ir.Ins>();
        bool structReturn = ReturnsStruct(function.Def);
        _returnsStruct = structReturn;
        _usesReturnBuffer |= structReturn;
        foreach (TypedSymbol param in function.Params)
        {
            if (param.Type.Kind == "struct")
            {
                // struktura przez wartość: wołający podaje adres, callee kopiuje ją do własnej lokalnej struktury
                var pointer = new Ir.Cell($"{_prefix}__{param.Name}__p", 2);
                AddBss(pointer.Sym, 2);
                saved.Add(new Ir.Owned(pointer.Sym, 2, false));
                parameters.Add(pointer);
                var copy = new VarCell($"{_prefix}__{param.Name}", param.Type);
                _cells[param.Name] = copy;
                AddBss(copy.Sym, param.Type.Size);
                aggregates.Add(new Ir.Owned(copy.Sym, param.Type.Size, true));
                prologue.Add(new Ir.CopyBlock(new Ir.AddrOf(copy.Sym, 0), pointer, param.Type.Size));
                continue;
            }

            var cell = new VarCell($"{_prefix}__{param.Name}", param.Type);
            _cells[param.Name] = cell;
            MarkVolatile(cell);
            AddBss(cell.Sym, Width(param.Type));
            saved.Add(new Ir.Owned(cell.Sym, Width(param.Type), false));
            parameters.Add(new Ir.Cell(cell.Sym, Width(param.Type)));
        }

        foreach (TypedSymbol local in function.Locals)
        {
            var cell = new VarCell($"{_prefix}__{local.Name}", local.Type);
            _cells[local.Name] = cell;
            MarkVolatile(cell);
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
        _wideTemps.Clear();
        _extraOwned.Clear();
        _body = [];
        Comment(function.Def);
        foreach (Ir.Ins ins in prologue)
        {
            Emit(ins);
        }

        foreach (Ast.Stmt item in function.Def.Body.Items)
        {
            LowerStmt(item);
        }

        for (int temp = 0; temp <= _maxTemp; temp++)
        {
            string sym = TempSym(temp);
            int tempSize = _wideTemps.Contains(temp) ? 4 : 2;
            AddBss(sym, tempSize);
            saved.Add(new Ir.Owned(sym, tempSize, false));
        }

        saved.AddRange(_extraOwned);
        int retW = ReturnWidth(function.Def);
        _pending.Add(new Pending(new Ir.Function(function.Def.Name, function.Def.IsStatic, parameters, retW, [], IrPasses.Optimize(DropJumpsToNext(_body), function.Def.Name, null, _volatileSyms)), saved, aggregates));
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
        _wideTemps.Clear();
        _extraOwned.Clear();
        _body = [];
        foreach (TypedSymbol global in _runtimeInits)
        {
            AssignVariable(global.Name, global.Init!, 0);
        }

        for (int temp = 0; temp <= _maxTemp; temp++)
        {
            AddBss(TempSym(temp), _wideTemps.Contains(temp) ? 4 : 2);
        }

        _initFunction = new Ir.Function("__cc_init", true, [], 0, [], IrPasses.Optimize(DropJumpsToNext(_body), "__cc_init"));
    }

    private sealed record VarCell(string Sym, CType Type);

    private sealed record Pending(Ir.Function Function, List<Ir.Owned> Scalars, List<Ir.Owned> Aggregates);
}
