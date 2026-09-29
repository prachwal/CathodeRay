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
/// Podzbiór v1: pełny uchar; int: pamięć, load/store, +,-, porównania, konwersje.
/// Bez: int *,/,%,&lt;&lt;,&gt;&gt;,&amp;,|,^, wskaźników, tablic (jawny błąd).</summary>
public sealed class Codegen
{
    private readonly List<(string Name, CType Type, byte[]? Init)> _data = [];
    private readonly Dictionary<string, Cell> _cells = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckedFunction> _functions = new(StringComparer.Ordinal);
    private readonly List<(string Label, string Symbol)> _words = [];
    private IReadOnlyDictionary<Ast.Node, int> _lines = new Dictionary<Ast.Node, int>();
    private string? _file;
    private int _addrs;
    private StringBuilder _code = new();
    private IReadOnlyList<TypedSymbol> _globals = [];
    private int _labels;
    private int _maxTemp = -1;
    private bool _needMul;
    private bool _needDiv;
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

    private static bool TryConst(Ast.Expr expr, out int value)
    {
        if (expr is Ast.Number number && int.TryParse(number.Text, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool IsWide(CType type) => type.Kind is "int" or "ptr";

    private static int ElemSize(CType type) => type.Kind == "uchar" ? 1 : 2;

    private static byte[]? InitBytes(TypedSymbol symbol)
    {
        if (symbol.Init is null)
        {
            return null;
        }

        if (symbol.Init is Ast.Number number && int.TryParse(number.Text, out int value))
        {
            return symbol.Type.Size == 1 ? [(byte)(value & 0xFF)] : [(byte)(value & 0xFF), (byte)((value >> 8) & 0xFF)];
        }

        throw new CCodegenException($"initializer of '{symbol.Name}' must be a constant (plan 20).");
    }

    private string Run(CheckedProgram program, string? fileName, bool objectMode)
    {
        _globals = program.Globals;
        _lines = program.Lines;
        _file = fileName;
        foreach (CheckedFunction function in program.Functions)
        {
            _functions[function.Def.Name] = function;
        }

        foreach (TypedSymbol global in program.Globals)
        {
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
            _code.AppendLine(".extern cc_arg2");
            _code.AppendLine(".extern cc_arg2_h");
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
            if (size == 2 && init.Length == 2)
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

        foreach ((string label, string symbol) in _words)
        {
            data.AppendLine($"{label}: .word {symbol}");
        }

        var bss = new StringBuilder();
        bss.AppendLine(".segment \"BSS\"");
        int bssBytes = 0;
        foreach ((string name, CType type, byte[]? init) in _data)
        {
            if (init is not null)
            {
                continue;
            }

            int size = type.Size;
            bssBytes += size;
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

        if (bssBytes > 256)
        {
            throw new CCodegenException($"BSS is {bssBytes} bytes (crt0 clears 256).");
        }

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

        for (int temp = 0; temp <= _maxTemp; temp++)
        {
            DataCell($"{_prefix}__t{temp}", CType.UChar);
            DataCell($"{_prefix}__t{temp}_h", CType.UChar);
            owned.Add($"{_prefix}__t{temp}");
            owned.Add($"{_prefix}__t{temp}_h");
        }

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
            if (IsWide(function.Params[i].Type))
            {
                if (i == 0)
                {
                    _code.AppendLine("LDA cc_arg1");
                    _code.AppendLine($"STA {cell.Lo}");
                    _code.AppendLine("LDA cc_arg1_h");
                    _code.AppendLine($"STA {cell.Lo}_h");
                }
                else
                {
                    _code.AppendLine("LDA cc_arg2");
                    _code.AppendLine($"STA {cell.Lo}");
                    _code.AppendLine("LDA cc_arg2_h");
                    _code.AppendLine($"STA {cell.Lo}_h");
                }
            }
            else if (i == 0)
            {
                _code.AppendLine("LDA cc_arg1");
                _code.AppendLine($"STA {cell.Lo}");
            }
            else
            {
                _code.AppendLine("LDA cc_arg1_h");
                _code.AppendLine($"STA {cell.Lo}");
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

    private void EmitStmt(Ast.Stmt stmt)
    {
        if (stmt is not Ast.Block and not Ast.Nop)
        {
            Comment(stmt);
        }

        switch (stmt)
        {
            case Ast.Nop:
                break;
            case Ast.Block block:
                foreach (Ast.Stmt item in block.Items)
                {
                    EmitStmt(item);
                }

                break;
            case Ast.Decl decl:
                if (decl.Init is not null)
                {
                    Store(decl.Name, decl.Init, 0);
                }

                break;
            case Ast.If ifStmt:
                EmitIf(ifStmt);
                break;
            case Ast.While whileStmt:
                EmitWhile(whileStmt);
                break;
            case Ast.For forStmt:
                EmitFor(forStmt);
                break;
            case Ast.Return ret:
                EmitReturn(ret);
                break;
            case Ast.ExprStmt exprStmt:
                Eval(exprStmt.Value, 0);
                break;
            default:
                throw new CCodegenException($"unsupported statement {stmt.GetType().Name}.");
        }
    }

    private void Store(string name, Ast.Expr value, int depth)
    {
        (string cell, CType type) = CellOf(name);
        if (IsWide(type))
        {
            EvalInt(value, depth, out string lo, out string hi);
            _code.AppendLine($"LDA {lo}");
            _code.AppendLine($"STA {cell}");
            _code.AppendLine($"LDA {hi}");
            _code.AppendLine($"STA {cell}_h");
        }
        else
        {
            Eval(value, depth);
            _code.AppendLine($"STA {cell}");
        }
    }

    private void EmitIf(Ast.If ifStmt)
    {
        string els = Label("else");
        string done = Label("endif");
        JumpIfFalse(ifStmt.Cond, els, 0);
        EmitStmt(ifStmt.Then);
        _code.AppendLine($"JMP {done}");
        _code.AppendLine($"{els}:");
        if (ifStmt.Else is not null)
        {
            EmitStmt(ifStmt.Else);
        }

        _code.AppendLine($"{done}:");
    }

    private void EmitWhile(Ast.While whileStmt)
    {
        string loop = Label("while");
        string done = Label("wend");
        _code.AppendLine($"{loop}:");
        JumpIfFalse(whileStmt.Cond, done, 0);
        EmitStmt(whileStmt.Body);
        _code.AppendLine($"JMP {loop}");
        _code.AppendLine($"{done}:");
    }

    private void EmitFor(Ast.For forStmt)
    {
        if (forStmt.Init is not null)
        {
            EmitStmt(forStmt.Init);
        }

        string loop = Label("for");
        string done = Label("fend");
        _code.AppendLine($"{loop}:");
        if (forStmt.Cond is not null)
        {
            JumpIfFalse(forStmt.Cond, done, 0);
        }

        EmitStmt(forStmt.Body);
        if (forStmt.Step is not null)
        {
            Eval(forStmt.Step, 0);
        }

        _code.AppendLine($"JMP {loop}");
        _code.AppendLine($"{done}:");
    }

    private void EmitReturn(Ast.Return ret)
    {
        if (ret.Value is not null)
        {
            if (!_functions.TryGetValue(_prefix, out CheckedFunction? current) || current.Def.ReturnType == "void")
            {
                throw new CCodegenException("return with a value needs a function.");
            }

            if (current.Def.ReturnType == "int")
            {
                EvalInt(ret.Value, 0, out string lo, out string hi);
                _code.AppendLine($"LDA {lo}");
                _code.AppendLine($"LDA {hi}");
                _code.AppendLine("TAX");
                _code.AppendLine($"LDA {lo}");
            }
            else
            {
                Eval(ret.Value, 0);
            }
        }

        _code.AppendLine($"JMP {_prefix}__ret");
    }

    private void JumpIfFalse(Ast.Expr cond, string falseLabel, int depth)
    {
        if (cond is Ast.Binary { Op: "&&" } and)
        {
            JumpIfFalse(and.Left, falseLabel, depth);
            JumpIfFalse(and.Right, falseLabel, depth);
            return;
        }

        if (cond is Ast.Binary { Op: "||" } or)
        {
            string done = Label("or");
            JumpIfTrue(or.Left, done, depth);
            JumpIfFalse(or.Right, falseLabel, depth);
            _code.AppendLine($"{done}:");
            return;
        }

        if (cond is Ast.Unary { Op: "!" } not)
        {
            JumpIfTrue(not.Operand, falseLabel, depth);
            return;
        }

        if (cond is Ast.Binary cmp && cmp.Op is "==" or "!=" or "<" or "<=" or ">" or ">=")
        {
            EmitCompareJump(cmp, falseLabel, depth);
            return;
        }

        if (IsWideKind(cond))
        {
            EvalInt(cond, depth, out string lo, out string hi);
            _code.AppendLine("LDX 0");
            _code.AppendLine($"LDA {lo}");
            _code.AppendLine($"ORA {hi},X");
            _code.AppendLine($"BEQ {falseLabel}");
            return;
        }

        Eval(cond, depth);
        _code.AppendLine("CPA 0");
        _code.AppendLine($"BEQ {falseLabel}");
    }

    private void JumpIfTrue(Ast.Expr cond, string trueLabel, int depth)
    {
        if (cond is Ast.Binary { Op: "&&" } and)
        {
            string done = Label("and");
            JumpIfFalse(and.Left, done, depth);
            JumpIfFalse(and.Right, done, depth);
            _code.AppendLine($"JMP {trueLabel}");
            _code.AppendLine($"{done}:");
            return;
        }

        if (cond is Ast.Binary { Op: "||" } or)
        {
            JumpIfTrue(or.Left, trueLabel, depth);
            JumpIfTrue(or.Right, trueLabel, depth);
            return;
        }

        if (cond is Ast.Unary { Op: "!" } not)
        {
            JumpIfFalse(not.Operand, trueLabel, depth);
            return;
        }

        if (cond is Ast.Binary cmp && cmp.Op is "==" or "!=" or "<" or "<=" or ">" or ">=")
        {
            string skip = Label("ntrue");
            EmitCompareJump(cmp, skip, depth);
            _code.AppendLine($"JMP {trueLabel}");
            _code.AppendLine($"{skip}:");
            return;
        }

        if (IsWideKind(cond))
        {
            EvalInt(cond, depth, out string lo, out string hi);
            _code.AppendLine("LDX 0");
            _code.AppendLine($"LDA {lo}");
            _code.AppendLine($"ORA {hi},X");
            _code.AppendLine($"BNE {trueLabel}");
            return;
        }

        Eval(cond, depth);
        _code.AppendLine("CPA 0");
        _code.AppendLine($"BNE {trueLabel}");
    }

    private void EmitIntCompare(string op, string aLo, string aHi, string bLo, string bHi, string falseLabel)
    {
        string patchHi = Label("cmp");
        string patchLo = Label("cmp");
        string hiLess = Label("hless");
        string hiEq = Label("heq");
        string loLess = Label("lless");
        string loEq = Label("leq");
        string done = Label("cdone");
        _code.AppendLine($"LDA {bHi}");
        _code.AppendLine($"STA {patchHi}+1");
        _code.AppendLine($"LDA {aHi}");
        _code.AppendLine($"{patchHi}: SUB 0");
        _code.AppendLine($"BCC {hiLess}");
        _code.AppendLine($"BEQ {hiEq}");
        switch (op)
        {
            case "<":
            case "<=":
            case "==":
                _code.AppendLine($"JMP {falseLabel}");
                break;
            default:
                _code.AppendLine($"JMP {done}");
                break;
        }

        _code.AppendLine($"{hiLess}:");
        switch (op)
        {
            case "<":
            case "<=":
            case "!=":
                _code.AppendLine($"JMP {done}");
                break;
            default:
                _code.AppendLine($"JMP {falseLabel}");
                break;
        }

        _code.AppendLine($"{hiEq}:");
        _code.AppendLine($"LDA {bLo}");
        _code.AppendLine($"STA {patchLo}+1");
        _code.AppendLine($"LDA {aLo}");
        _code.AppendLine($"{patchLo}: SUB 0");
        _code.AppendLine($"BCC {loLess}");
        _code.AppendLine($"BEQ {loEq}");
        switch (op)
        {
            case ">":
            case ">=":
            case "!=":
                _code.AppendLine($"JMP {done}");
                break;
            default:
                _code.AppendLine($"JMP {falseLabel}");
                break;
        }

        _code.AppendLine($"{loLess}:");
        switch (op)
        {
            case "<":
            case "<=":
            case "!=":
                _code.AppendLine($"JMP {done}");
                break;
            default:
                _code.AppendLine($"JMP {falseLabel}");
                break;
        }

        _code.AppendLine($"{loEq}:");
        switch (op)
        {
            case "==":
            case "<=":
            case ">=":
                _code.AppendLine($"JMP {done}");
                break;
            default:
                _code.AppendLine($"JMP {falseLabel}");
                break;
        }

        _code.AppendLine($"{done}:");
    }

    private void EmitCompareJump(Ast.Binary cmp, string falseLabel, int depth)
    {
        string op = cmp.Op;
        if (TryConst(cmp.Left, out int leftConst) && TryConst(cmp.Right, out int rightConst))
        {
            bool result = op switch
            {
                "==" => leftConst == rightConst,
                "!=" => leftConst != rightConst,
                "<" => leftConst < rightConst,
                "<=" => leftConst <= rightConst,
                ">" => leftConst > rightConst,
                _ => leftConst >= rightConst,
            };
            if (!result)
            {
                _code.AppendLine($"JMP {falseLabel}");
            }

            return;
        }

        if (KindOf(cmp.Left) == "int" || KindOf(cmp.Right) == "int")
        {
            EvalInt(cmp.Left, depth, out string leftLo, out string leftHi);
            EvalInt(cmp.Right, depth + 1, out string rightLo, out string rightHi);
            EmitIntCompare(op, leftLo, leftHi, rightLo, rightHi, falseLabel);
            return;
        }

        if (cmp.Right is Ast.Number number && int.TryParse(number.Text, out int bound) && bound is >= 0 and <= byte.MaxValue)
        {
            Eval(cmp.Left, depth);
            _code.AppendLine($"CPA {bound}");
            BranchOn(op, jumpWhenTrue: false, falseLabel);
            return;
        }

        if (cmp.Left is Ast.Number lnum && int.TryParse(lnum.Text, out int lbound) && lbound is >= 0 and <= byte.MaxValue)
        {
            Eval(cmp.Right, depth);
            _code.AppendLine($"CPA {lbound}");
            BranchOn(Swap(op), jumpWhenTrue: false, falseLabel);
            return;
        }

        EvalInt(cmp.Left, depth, out string llo, out _);
        EvalInt(cmp.Right, depth + 1, out string rlo, out _);
        string patch = Label("cmp");
        _code.AppendLine($"LDA {rlo}");
        _code.AppendLine($"STA {patch}+1");
        _code.AppendLine($"LDA {llo}");
        _code.AppendLine($"{patch}: SUB 0");
        BranchOn(op, jumpWhenTrue: false, falseLabel);
    }

    private void BranchOn(string op, bool jumpWhenTrue, string label)
    {
        // Ostre: jeden skok od C. Nieostre: dwa (osobno ==), bo C nie
        // rozróżnia == od ostrej po tej samej stronie.
        switch (op, jumpWhenTrue)
        {
            case ("==", false):
                _code.AppendLine($"BNE {label}");
                break;
            case ("==", true):
                _code.AppendLine($"BEQ {label}");
                break;
            case ("!=", false):
                _code.AppendLine($"BEQ {label}");
                break;
            case ("!=", true):
                _code.AppendLine($"BNE {label}");
                break;
            case ("<", false):
            case (">=", true):
                _code.AppendLine($"BCS {label}");
                break;
            case ("<", true):
            case (">=", false):
                _code.AppendLine($"BCC {label}");
                break;
            case ("<=", false):
            {
                string skip = Label("nle");
                _code.AppendLine($"BEQ {skip}");
                _code.AppendLine($"BCS {label}");
                _code.AppendLine($"{skip}:");
                break;
            }

            case ("<=", true):
                _code.AppendLine($"BEQ {label}");
                _code.AppendLine($"BCC {label}");
                break;
            case (">", false):
                _code.AppendLine($"BEQ {label}");
                _code.AppendLine($"BCC {label}");
                break;
            case (">", true):
            {
                string skip = Label("ngt");
                _code.AppendLine($"BEQ {skip}");
                _code.AppendLine($"BCS {label}");
                _code.AppendLine($"{skip}:");
                break;
            }

            default:
                throw new CCodegenException($"unknown comparison '{op}'.");
        }
    }

    private void Eval(Ast.Expr expr, int depth)
    {
        if (expr is Ast.Deref or Ast.Index or Ast.AssignTo || IsWideKind(expr))
        {
            EvalInt(expr, depth, out _, out _);
            return;
        }

        switch (expr)
        {
            case Ast.Number number:
                _code.AppendLine($"LDI {number.Text}");
                break;
            case Ast.Var variable:
                (string cell, _) = CellOf(variable.Name);
                _code.AppendLine($"LDA {cell}");
                break;
            case Ast.Call call:
                EmitCall(call, depth);
                break;
            case Ast.Unary unary:
                EmitUnary(unary, depth);
                break;
            case Ast.Binary binary when binary.Op is "&&" or "||":
                EmitLogicValue(binary, depth);
                break;
            case Ast.Binary binary when binary.Op is "==" or "!=" or "<" or "<=" or ">" or ">=":
                EmitCompareValue(binary, depth);
                break;
            case Ast.Binary binary:
                EmitArith(binary, depth);
                break;
            case Ast.Assign assign:
                Store(assign.Name, assign.Value, depth);
                _code.AppendLine($"LDA {CellOf(assign.Name).Lo}");
                break;
            case Ast.AssignTo assignTo:
                StorePtr(assignTo, depth);
                break;
            case Ast.Ternary ternary:
                EmitTernary(ternary, depth);
                break;
            default:
                throw new CCodegenException($"unsupported expression {expr.GetType().Name}.");
        }
    }

    private void EvalInt(Ast.Expr expr, int depth, out string lo, out string hi)
    {
        lo = Temp(depth, hi: false);
        hi = Temp(depth, hi: true);
        switch (expr)
        {
            case Ast.Number number when int.TryParse(number.Text, out int value):
                _code.AppendLine($"LDX {(value >> 8) & 0xFF}");
                _code.AppendLine($"LDI {value & 0xFF}");
                _code.AppendLine($"STA {lo}");
                _code.AppendLine("TXA");
                _code.AppendLine($"STA {hi}");
                break;
            case Ast.Var variable:
            {
                (string cell, CType vtype) = CellOf(variable.Name);
                if (vtype.Kind == "array")
                {
                    EmitAddressOf(cell, lo, hi);
                    break;
                }

                if (vtype.Kind == "uchar")
                {
                    Eval(expr, depth);
                    _code.AppendLine($"STA {lo}");
                    _code.AppendLine("LDX 0");
                    _code.AppendLine("TXA");
                    _code.AppendLine($"STA {hi}");
                    break;
                }

                _code.AppendLine($"LDA {cell}");
                _code.AppendLine($"STA {lo}");
                _code.AppendLine($"LDA {cell}_h");
                _code.AppendLine($"STA {hi}");
                break;
            }

            case Ast.Call call when ReturnsInt(call):
                EmitCall(call, depth);
                _code.AppendLine($"STA {lo}");
                _code.AppendLine("TXA");
                _code.AppendLine($"STA {hi}");
                _code.AppendLine($"LDA {lo}");
                break;
            case Ast.Call call:
                EmitCall(call, depth);
                _code.AppendLine($"STA {lo}");
                _code.AppendLine("LDX 0");
                _code.AppendLine("TXA");
                _code.AppendLine($"STA {hi}");
                break;
            case Ast.Binary binary when binary.Op is "+" or "-"
                && (KindOf(binary.Left) == "ptr" || KindOf(binary.Right) == "ptr"):
                EmitPtrArith(binary, depth, lo, hi);
                break;
            case Ast.Binary binary when binary.Op is "+" or "-":
                EvalIntArith(binary, depth, lo, hi);
                break;
            case Ast.AddressOf addressOf:
            {
                (string acell, _) = CellOf(addressOf.Name);
                EmitAddressOf(acell, lo, hi);
                break;
            }

            case Ast.Deref deref:
                EvalPtrAddr(deref, depth, out string dalo, out string dahi);
                PatchedLoad(dalo, dahi, ElemSize(TypeOfDeref(deref)), lo, hi, depth);
                break;
            case Ast.Index index:
                EvalPtrAddr(index, depth, out string ialo, out string iahi);
                PatchedLoad(ialo, iahi, ElemSize(TypeOfIndex(index)), lo, hi, depth);
                break;
            case Ast.AssignTo assignTo:
                StorePtr(assignTo, depth);
                _code.AppendLine($"LDA {Temp(depth, hi: false)}");
                _code.AppendLine($"LDA {Temp(depth, hi: true)}");
                _code.AppendLine("TAX");
                _code.AppendLine($"LDA {Temp(depth, hi: false)}");
                break;
            case Ast.Binary binary when binary.Op is "==" or "!=" or "<" or "<=" or ">" or ">=" or "&&" or "||":
            case Ast.Unary { Op: "!" }:
                EmitCompareValueTo(expr, depth, lo, hi);
                break;
            case Ast.Binary binary:
                throw new CCodegenException($"int operator '{binary.Op}' needs 16-bit helpers (plan 20).");
            case Ast.Unary unary:
                EmitIntNegate(unary, depth, lo, hi);
                break;
            case Ast.Assign assign:
                Store(assign.Name, assign.Value, depth);
                (string assignCell, CType assignType) = CellOf(assign.Name);
                _code.AppendLine($"LDA {assignCell}");
                _code.AppendLine($"STA {lo}");
                if (assignType.Kind == "uchar")
                {
                    _code.AppendLine("LDX 0");
                    _code.AppendLine("TXA");
                    _code.AppendLine($"STA {hi}");
                    break;
                }

                _code.AppendLine($"LDA {assignCell}_h");
                _code.AppendLine($"STA {hi}");
                break;
            case Ast.Ternary ternary:
                EmitIntTernary(ternary, depth, lo, hi);
                break;
            default:
                throw new CCodegenException($"int expression '{expr.GetType().Name}' not supported (only +,-,comparisons,negation).");
        }

        _code.AppendLine($"LDA {lo}");
        _code.AppendLine($"LDA {hi}");
        _code.AppendLine("TAX");
        _code.AppendLine($"LDA {lo}");
    }

    private void EvalIntArith(Ast.Binary binary, int depth, string lo, string hi)
    {
        EvalInt(binary.Left, depth + 1, out string leftLo, out string leftHi);
        EvalInt(binary.Right, depth + 2, out string rightLo, out string rightHi);
        _code.AppendLine("LDX 0");
        _code.AppendLine($"LDA {leftLo}");
        if (binary.Op == "+")
        {
            _code.AppendLine($"ADD {rightLo},X");
            _code.AppendLine($"STA {lo}");
            _code.AppendLine($"LDA {leftHi}");
            _code.AppendLine($"ADC {rightHi},X");
            _code.AppendLine($"STA {hi}");
            MaskUchar(binary, hi);
            return;
        }

        string patchLo = Label("sub");
        string patchHi = Label("sub");
        string noBorrow = Label("nb");
        _code.AppendLine($"LDA {rightLo}");
        _code.AppendLine($"STA {patchLo}+1");
        _code.AppendLine($"LDA {leftLo}");
        _code.AppendLine($"{patchLo}: SUB 0");
        _code.AppendLine($"STA {lo}");
        _code.AppendLine($"BCS {noBorrow}");
        _code.AppendLine($"LDA {rightHi}");
        _code.AppendLine("ADD 1");
        _code.AppendLine($"STA {rightHi}");
        _code.AppendLine($"{noBorrow}:");
        _code.AppendLine($"LDA {rightHi}");
        _code.AppendLine($"STA {patchHi}+1");
        _code.AppendLine($"LDA {leftHi}");
        _code.AppendLine($"{patchHi}: SUB 0");
        _code.AppendLine($"STA {hi}");
        MaskUchar(binary, hi);
    }

    /// <summary>Maska uchar: operacja na dwóch ucharach daje uchar (jak w checkerze),
    /// więc hi zerujemy (bez propagacji carry/pożyczki).</summary>
    private void MaskUchar(Ast.Binary binary, string hi)
    {
        if (KindOf(binary.Left) != "int" && KindOf(binary.Right) != "int")
        {
            _code.AppendLine("LDX 0");
            _code.AppendLine("TXA");
            _code.AppendLine($"STA {hi}");
        }
    }

    /// <summary>Adres labela do pary (ukryta komórka .word, linker wypełnia).</summary>
    private void EmitAddressOf(string cellLabel, string lo, string hi)
    {
        string addr = $"{_prefix}__addr{++_addrs}";
        _words.Add((addr, cellLabel));
        _code.AppendLine($"LDA {addr}");
        _code.AppendLine($"STA {lo}");
        _code.AppendLine($"LDA {addr}+1");
        _code.AppendLine($"STA {hi}");
    }

    /// <summary>Podwaja parę (lo,hi) w miejscu (skala x2 dla int*).</summary>
    private void DoublePair(string lo, string hi)
    {
        _code.AppendLine("LDX 0");
        _code.AppendLine($"LDA {lo}");
        _code.AppendLine($"ADD {lo},X");
        _code.AppendLine($"STA {lo}");
        _code.AppendLine($"LDA {hi}");
        _code.AppendLine($"ADC {hi},X");
        _code.AppendLine($"STA {hi}");
    }

    /// <summary>Liczy adres celu (*p lub p[i]) do pary Temp(depth).</summary>
    private void EvalPtrAddr(Ast.Expr target, int depth, out string lo, out string hi)
    {
        lo = Temp(depth, hi: false);
        hi = Temp(depth, hi: true);
        if (target is Ast.Deref deref)
        {
            EvalInt(deref.Pointer, depth + 1, out string plo, out string phi);
            _code.AppendLine($"LDA {plo}");
            _code.AppendLine($"STA {lo}");
            _code.AppendLine($"LDA {phi}");
            _code.AppendLine($"STA {hi}");
            return;
        }

        if (target is Ast.Index index)
        {
            EvalInt(index.Base, depth + 1, out string blo, out string bhi);
            EvalInt(index.Offset, depth + 2, out string ilo, out string ihi);
            if (ElemSize(TypeOfIndex(index)) == 2)
            {
                DoublePair(ilo, ihi);
            }

            _code.AppendLine("LDX 0");
            _code.AppendLine($"LDA {blo}");
            _code.AppendLine($"ADD {ilo},X");
            _code.AppendLine($"STA {lo}");
            _code.AppendLine($"LDA {bhi}");
            _code.AppendLine($"ADC {ihi},X");
            _code.AppendLine($"STA {hi}");
            return;
        }

        throw new CCodegenException($"pointer target '{target.GetType().Name}' not supported.");
    }

    /// <summary>Czyta spod adresu (łatany operand, wzorzec divmod); nadpisuje parę.</summary>
    private void PatchedLoad(string addrLo, string addrHi, int elemSize, string outLo, string outHi, int depth)
    {
        string tmp = Temp(depth + 3, hi: false);
        string tmph = Temp(depth + 3, hi: true);
        _code.AppendLine($"LDA {addrLo}");
        _code.AppendLine("ADD 1");
        _code.AppendLine($"STA {tmp}");
        _code.AppendLine($"LDA {addrHi}");
        _code.AppendLine("ADC 0");
        _code.AppendLine($"STA {tmph}");
        string site = Label("ld");
        _code.AppendLine($"LDA {addrLo}");
        _code.AppendLine($"STA {site}+1");
        _code.AppendLine($"LDA {addrHi}");
        _code.AppendLine($"STA {site}+2");
        _code.AppendLine($"{site}: LDA 0");
        _code.AppendLine($"STA {outLo}");
        if (elemSize == 1)
        {
            _code.AppendLine("LDX 0");
            _code.AppendLine("TXA");
            _code.AppendLine($"STA {outHi}");
            return;
        }

        string siteHi = Label("ld");
        _code.AppendLine($"LDA {tmp}");
        _code.AppendLine($"STA {siteHi}+1");
        _code.AppendLine($"LDA {tmph}");
        _code.AppendLine($"STA {siteHi}+2");
        _code.AppendLine($"{siteHi}: LDA 0");
        _code.AppendLine($"STA {outHi}");
    }

    /// <summary>Pisze pod adres (łatany operand); wartość z pary/rejestru.</summary>
    private void PatchedStore(string addrLo, string addrHi, int elemSize, string valLo, string valHi, int depth)
    {
        string site = Label("st");
        _code.AppendLine($"LDA {addrLo}");
        _code.AppendLine($"STA {site}+1");
        _code.AppendLine($"LDA {addrHi}");
        _code.AppendLine($"STA {site}+2");
        _code.AppendLine($"LDA {valLo}");
        _code.AppendLine($"{site}: STA 0");
        if (elemSize == 1)
        {
            return;
        }

        string tmp = Temp(depth + 3, hi: false);
        string tmph = Temp(depth + 3, hi: true);
        _code.AppendLine($"LDA {addrLo}");
        _code.AppendLine("ADD 1");
        _code.AppendLine($"STA {tmp}");
        _code.AppendLine($"LDA {addrHi}");
        _code.AppendLine("ADC 0");
        _code.AppendLine($"STA {tmph}");
        string siteHi = Label("st");
        _code.AppendLine($"LDA {tmp}");
        _code.AppendLine($"STA {siteHi}+1");
        _code.AppendLine($"LDA {tmph}");
        _code.AppendLine($"STA {siteHi}+2");
        _code.AppendLine($"LDA {valHi}");
        _code.AppendLine($"{siteHi}: STA 0");
    }

    /// <summary>Zapis przez wskaźnik/indeks (wartość, potem adres).</summary>
    private void StorePtr(Ast.AssignTo assignTo, int depth)
    {
        CType elem = assignTo.Target is Ast.Index index ? TypeOfIndex(index) : TypeOfDeref(assignTo.Target);
        int size = ElemSize(elem);
        string vlo;
        string vhi;
        if (size == 1)
        {
            Eval(assignTo.Value, depth);
            vlo = Temp(depth, hi: false);
            _code.AppendLine($"STA {vlo}");
            vhi = vlo;
        }
        else
        {
            EvalInt(assignTo.Value, depth, out vlo, out vhi);
        }

        EvalPtrAddr(assignTo.Target, depth + 1, out string alo, out string ahi);
        if (size == 1)
        {
            PatchedStore(alo, ahi, size, vlo, vhi, depth + 1);
            _code.AppendLine($"LDA {vlo}");
            return;
        }

        PatchedStore(alo, ahi, size, vlo, vhi, depth + 1);
        _code.AppendLine($"LDA {vlo}");
        _code.AppendLine($"LDA {vhi}");
        _code.AppendLine("TAX");
        _code.AppendLine($"LDA {vlo}");
    }

    private CType TypeOfIndex(Ast.Index index) =>
        _types.TryGetValue(index, out CType? type) ? type : CType.UChar;

    private CType TypeOfDeref(Ast.Expr target) =>
        _types.TryGetValue(target, out CType? type) ? type : CType.UChar;

    /// <summary>Arytmetyka wskaźników (ptr+int, int+ptr, ptr-int; skala z elementu).</summary>
    private void EmitPtrArith(Ast.Binary binary, int depth, string lo, string hi)
    {
        bool leftPtr = KindOf(binary.Left) == "ptr";
        Ast.Expr ptrSide = leftPtr ? binary.Left : binary.Right;
        Ast.Expr intSide = leftPtr ? binary.Right : binary.Left;
        if (binary.Op == "-" && !leftPtr)
        {
            throw new CCodegenException("int - ptr is not supported.");
        }

        CType? @base = _types.TryGetValue(ptrSide, out CType? ptrType) ? ptrType.Base : null;
        int scale = @base is not null && @base.Kind == "uchar" ? 1 : 2;
        EvalInt(ptrSide, depth + 1, out string plo, out string phi);
        EvalInt(intSide, depth + 2, out string ilo, out string ihi);
        if (scale == 2)
        {
            DoublePair(ilo, ihi);
            _code.AppendLine("LDX 0");
            _code.AppendLine($"LDA {plo}");
            if (binary.Op == "+")
            {
                _code.AppendLine($"ADD {ilo},X");
            }
            else
            {
                string site = Label("psub");
                _code.AppendLine($"LDA {ilo}");
                _code.AppendLine($"STA {site}+1");
                _code.AppendLine($"LDA {plo}");
                _code.AppendLine($"{site}: SUB 0");
            }

            _code.AppendLine($"STA {lo}");
            _code.AppendLine($"LDA {phi}");
            if (binary.Op == "+")
            {
                _code.AppendLine($"ADC {ihi},X");
            }
            else
            {
                string siteHi = Label("psub");
                _code.AppendLine($"LDA {ihi}");
                _code.AppendLine($"STA {siteHi}+1");
                _code.AppendLine($"LDA {phi}");
                _code.AppendLine($"{siteHi}: SUB 0");
            }

            _code.AppendLine($"STA {hi}");
            return;
        }

        _code.AppendLine("LDX 0");
        _code.AppendLine($"LDA {plo}");
        if (binary.Op == "+")
        {
            _code.AppendLine($"ADD {ilo},X");
            _code.AppendLine($"STA {lo}");
            _code.AppendLine($"LDA {phi}");
            _code.AppendLine($"ADC {ihi},X");
            _code.AppendLine($"STA {hi}");
            return;
        }

        string patchLo = Label("psub");
        string patchHi = Label("psub");
        string noBorrow = Label("nb");
        _code.AppendLine($"LDA {ilo}");
        _code.AppendLine($"STA {patchLo}+1");
        _code.AppendLine($"LDA {plo}");
        _code.AppendLine($"{patchLo}: SUB 0");
        _code.AppendLine($"STA {lo}");
        _code.AppendLine($"BCS {noBorrow}");
        _code.AppendLine($"LDA {ihi}");
        _code.AppendLine("ADD 1");
        _code.AppendLine($"STA {ihi}");
        _code.AppendLine($"{noBorrow}:");
        _code.AppendLine($"LDA {ihi}");
        _code.AppendLine($"STA {patchHi}+1");
        _code.AppendLine($"LDA {phi}");
        _code.AppendLine($"{patchHi}: SUB 0");
        _code.AppendLine($"STA {hi}");
    }

    private bool ReturnsInt(Ast.Call call) =>
        _functions.TryGetValue(call.Name, out CheckedFunction? target)
        && target.Def.ReturnType == "int";

    private void EmitCompareValueTo(Ast.Expr expr, int depth, string lo, string hi)
    {
        string isFalse = Label("cfalse");
        string done = Label("cdone");
        JumpIfFalse(expr, isFalse, depth);
        _code.AppendLine("LDI 1");
        _code.AppendLine($"STA {lo}");
        _code.AppendLine($"JMP {done}");
        _code.AppendLine($"{isFalse}:");
        _code.AppendLine("LDI 0");
        _code.AppendLine($"STA {lo}");
        _code.AppendLine($"{done}:");
        _code.AppendLine("LDX 0");
        _code.AppendLine("TXA");
        _code.AppendLine($"STA {hi}");
    }

    private void EmitIntNegate(Ast.Unary unary, int depth, string lo, string hi)
    {
        if (unary.Op != "-" && unary.Op != "~")
        {
            throw new CCodegenException($"operator '{unary.Op}' needs int operands.");
        }

        if (KindOf(unary.Operand) == "uchar")
        {
            Eval(unary.Operand, depth);
            _code.AppendLine("NOT");
            if (unary.Op == "-")
            {
                _code.AppendLine("INC");
            }

            _code.AppendLine($"STA {lo}");
            _code.AppendLine("LDX 0");
            _code.AppendLine("TXA");
            _code.AppendLine($"STA {hi}");
            return;
        }

        EvalInt(unary.Operand, depth + 1, out string olo, out string ohi);
        _code.AppendLine($"LDA {olo}");
        _code.AppendLine("NOT");
        _code.AppendLine($"STA {lo}");
        _code.AppendLine($"LDA {ohi}");
        _code.AppendLine("NOT");
        _code.AppendLine($"STA {hi}");
        if (unary.Op == "-")
        {
            string noBorrow = Label("nborrow");
            _code.AppendLine($"LDA {lo}");
            _code.AppendLine("INC");
            _code.AppendLine($"STA {lo}");
            _code.AppendLine($"BNE {noBorrow}");
            _code.AppendLine($"LDA {hi}");
            _code.AppendLine("INC");
            _code.AppendLine($"STA {hi}");
            _code.AppendLine($"{noBorrow}:");
        }
    }

    private void EmitIntTernary(Ast.Ternary ternary, int depth, string lo, string hi)
    {
        string els = Label("telse");
        string done = Label("tdone");
        JumpIfFalse(ternary.Cond, els, depth);
        EvalInt(ternary.Then, depth + 1, out string tlo, out string thi);
        _code.AppendLine($"LDA {tlo}");
        _code.AppendLine($"STA {lo}");
        _code.AppendLine($"LDA {thi}");
        _code.AppendLine($"STA {hi}");
        _code.AppendLine($"JMP {done}");
        _code.AppendLine($"{els}:");
        EvalInt(ternary.Else, depth + 1, out string elo, out string ehi);
        _code.AppendLine($"LDA {elo}");
        _code.AppendLine($"STA {lo}");
        _code.AppendLine($"LDA {ehi}");
        _code.AppendLine($"STA {hi}");
        _code.AppendLine($"{done}:");
    }

    private void EmitCall(Ast.Call call, int depth)
    {
        if (!_functions.TryGetValue(call.Name, out CheckedFunction? target))
        {
            throw new CCodegenException($"undefined function '{call.Name}'.");
        }

        if (call.Args.Count > 2)
        {
            throw new CCodegenException($"'{call.Name}' takes at most 2 arguments (A, X).");
        }

        bool secondInt = call.Args.Count == 2 && target.Params.Count > 1 && IsWide(target.Params[1].Type);
        bool firstInt = call.Args.Count >= 1 && target.Params.Count > 0 && IsWide(target.Params[0].Type);
        if (call.Args.Count == 2 && secondInt)
        {
            EvalInt(call.Args[1], depth + 1, out _, out _);
        }
        else if (call.Args.Count == 2)
        {
            Eval(call.Args[1], depth + 1);
            _code.AppendLine($"STA {Temp(depth + 1, hi: false)}");
        }

        string firstSpill = Temp(depth + 3, hi: false);
        if (call.Args.Count >= 1)
        {
            if (firstInt)
            {
                EvalInt(call.Args[0], depth + 2, out string lo, out _);
                firstSpill = lo;
            }
            else
            {
                Eval(call.Args[0], depth + 2);
                _code.AppendLine($"STA {firstSpill}");
            }
        }

        if (secondInt)
        {
            _code.AppendLine($"LDA {Temp(depth + 1, hi: false)}");
            _code.AppendLine("STA cc_arg2");
            _code.AppendLine($"LDA {Temp(depth + 1, hi: true)}");
            _code.AppendLine("STA cc_arg2_h");
        }
        else if (call.Args.Count == 2)
        {
            _code.AppendLine($"LDA {Temp(depth + 1, hi: false)}");
            _code.AppendLine("TAX");
        }

        if (call.Args.Count >= 1)
        {
            _code.AppendLine($"LDA {firstSpill}");
        }

        _code.AppendLine($"CALL {call.Name}");
    }

    private void EmitUnary(Ast.Unary unary, int depth)
    {
        switch (unary.Op)
        {
            case "-":
                Eval(unary.Operand, depth);
                _code.AppendLine("NOT");
                _code.AppendLine("INC");
                break;
            case "~":
                Eval(unary.Operand, depth);
                _code.AppendLine("NOT");
                break;
            case "!":
                string isFalse = Label("isfalse");
                string done = Label("notdone");
                JumpIfFalse(unary.Operand, isFalse, depth);
                _code.AppendLine("LDI 0");
                _code.AppendLine($"JMP {done}");
                _code.AppendLine($"{isFalse}:");
                _code.AppendLine("LDI 1");
                _code.AppendLine($"{done}:");
                break;
            default:
                throw new CCodegenException($"unknown operator '{unary.Op}'.");
        }
    }

    private void EmitLogicValue(Ast.Binary binary, int depth)
    {
        string isFalse = Label("lfalse");
        string done = Label("ldone");
        JumpIfFalse(binary, isFalse, depth);
        _code.AppendLine("LDI 1");
        _code.AppendLine($"JMP {done}");
        _code.AppendLine($"{isFalse}:");
        _code.AppendLine("LDI 0");
        _code.AppendLine($"{done}:");
    }

    private void EmitCompareValue(Ast.Binary binary, int depth)
    {
        string isFalse = Label("cfalse");
        string done = Label("cdone");
        JumpIfFalse(binary, isFalse, depth);
        _code.AppendLine("LDI 1");
        _code.AppendLine($"JMP {done}");
        _code.AppendLine($"{isFalse}:");
        _code.AppendLine("LDI 0");
        _code.AppendLine($"{done}:");
    }

    private void EmitArith(Ast.Binary binary, int depth)
    {
        if (binary.Op is "*" or "/" or "%")
        {
            EmitLibCall(binary, depth);
            return;
        }

        if (binary.Op is "<<" or ">>")
        {
            EmitShift(binary, depth);
            return;
        }

        if (binary.Op is not ("+" or "-" or "&" or "|" or "^"))
        {
            throw new CCodegenException($"operator '{binary.Op}' needs int operands.");
        }

        string left = Temp(depth, hi: false);
        Eval(binary.Left, depth + 1);
        _code.AppendLine($"STA {left}");
        Eval(binary.Right, depth + 1);
        string right = Temp(depth + 1, hi: false);
        _code.AppendLine($"STA {right}");
        _code.AppendLine("LDX 0");
        _code.AppendLine($"LDA {left}");
        switch (binary.Op)
        {
            case "+":
                _code.AppendLine($"ADD {right},X");
                break;
            case "-":
                EmitPatchedSub(right, depth);
                break;
            case "&":
                _code.AppendLine($"AND {right},X");
                break;
            case "|":
                _code.AppendLine($"ORA {right},X");
                break;
            default:
                _code.AppendLine($"EOR {right},X");
                break;
        }
    }

    private void EmitPatchedSub(string rightCell, int depth)
    {
        string site = Label("sub");
        string tmp = Temp(depth + 2, hi: false);
        _code.AppendLine($"STA {tmp}");
        _code.AppendLine($"LDA {rightCell}");
        _code.AppendLine($"STA {site}+1");
        _code.AppendLine($"LDA {tmp}");
        _code.AppendLine($"{site}: SUB 0");
    }

    private void EmitShift(Ast.Binary binary, int depth)
    {
        string value = Temp(depth, hi: false);
        string count = Temp(depth + 1, hi: false);
        Eval(binary.Left, depth + 2);
        _code.AppendLine($"STA {value}");
        Eval(binary.Right, depth + 2);
        _code.AppendLine($"STA {count}");
        string loop = Label("sh");
        string done = Label("shd");
        _code.AppendLine($"LDA {value}");
        _code.AppendLine($"{loop}:");
        _code.AppendLine($"LDA {count}");
        _code.AppendLine("CPA 0");
        _code.AppendLine($"BEQ {done}");
        _code.AppendLine($"LDA {value}");
        _code.AppendLine(binary.Op == "<<" ? "SHL" : "SHR");
        _code.AppendLine($"STA {value}");
        _code.AppendLine($"LDA {count}");
        _code.AppendLine("SUB 1");
        _code.AppendLine($"STA {count}");
        _code.AppendLine($"JMP {loop}");
        _code.AppendLine($"{done}:");
        _code.AppendLine($"LDA {value}");
    }

    private void EmitLibCall(Ast.Binary binary, int depth)
    {
        string helper = binary.Op switch
        {
            "*" => "cc_mul8",
            "/" => "cc_divmod",
            _ => "cc_divmod",
        };
        string left = Temp(depth, hi: false);
        Eval(binary.Left, depth + 1);
        _code.AppendLine($"STA {left}");
        Eval(binary.Right, depth + 1);
        string right = Temp(depth + 1, hi: false);
        _code.AppendLine($"STA {right}");
        _code.AppendLine($"LDA {left}");
        _code.AppendLine($"STA {Temp(depth + 2, hi: false)}");
        _code.AppendLine($"LDA {right}");
        _code.AppendLine("TAX");
        _code.AppendLine($"LDA {Temp(depth + 2, hi: false)}");
        if (helper == "cc_mul8")
        {
            _needMul = true;
        }
        else
        {
            _needDiv = true;
        }

        _code.AppendLine($"CALL {helper}");
        if (binary.Op == "%")
        {
            _code.AppendLine("TXA");
        }
    }

    private void EmitTernary(Ast.Ternary ternary, int depth)
    {
        string els = Label("telse");
        string done = Label("tdone");
        JumpIfFalse(ternary.Cond, els, depth);
        Eval(ternary.Then, depth);
        _code.AppendLine($"JMP {done}");
        _code.AppendLine($"{els}:");
        Eval(ternary.Else, depth);
        _code.AppendLine($"{done}:");
    }

    private void EmitMul()
    {
        _code.AppendLine(".proc cc_mul8");
        _code.AppendLine(".global cc_mul8");
        _code.AppendLine("STA cc_m_a");
        _code.AppendLine("TXA");
        _code.AppendLine("STA cc_m_b");
        _code.AppendLine("LDX 0");
        _code.AppendLine("LDI 0");
        _code.AppendLine("STA cc_m_acc");
        _code.AppendLine("cc_m_loop: LDA cc_m_b");
        _code.AppendLine("BEQ cc_m_done");
        _code.AppendLine("SUB 1");
        _code.AppendLine("STA cc_m_b");
        _code.AppendLine("LDA cc_m_acc");
        _code.AppendLine("ADD cc_m_a,X");
        _code.AppendLine("STA cc_m_acc");
        _code.AppendLine("JMP cc_m_loop");
        _code.AppendLine("cc_m_done: LDA cc_m_acc");
        _code.AppendLine("RET");
        _code.AppendLine(".endproc");
        DataCell("cc_m_acc", CType.UChar);
        DataCell("cc_m_a", CType.UChar);
        DataCell("cc_m_b", CType.UChar);
    }

    private void EmitDiv()
    {
        _code.AppendLine(".proc cc_divmod");
        _code.AppendLine(".global cc_divmod");
        _code.AppendLine("CPX 0");
        _code.AppendLine("BEQ cc_d_zero");
        _code.AppendLine("STA cc_d_n");
        _code.AppendLine("TXA");
        _code.AppendLine("STA cc_d_patch+1");
        _code.AppendLine("LDX 0");
        _code.AppendLine("LDI 0");
        _code.AppendLine("STA cc_d_q");
        _code.AppendLine("cc_d_loop: LDA cc_d_n");
        _code.AppendLine("cc_d_patch: SUB 0");
        _code.AppendLine("BCC cc_d_done");
        _code.AppendLine("STA cc_d_n");
        _code.AppendLine("LDA cc_d_q");
        _code.AppendLine("INC");
        _code.AppendLine("STA cc_d_q");
        _code.AppendLine("JMP cc_d_loop");
        _code.AppendLine("cc_d_done: LDA cc_d_n");
        _code.AppendLine("TAX");
        _code.AppendLine("LDA cc_d_q");
        _code.AppendLine("RET");
        _code.AppendLine("cc_d_zero: LDI 0");
        _code.AppendLine("TAX");
        _code.AppendLine("RET");
        _code.AppendLine(".endproc");
        DataCell("cc_d_n", CType.UChar);
        DataCell("cc_d_q", CType.UChar);
    }

    private sealed record Cell(string Lo, CType Type);
}
