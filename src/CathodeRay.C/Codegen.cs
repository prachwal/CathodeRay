using System.Text;

namespace CathodeRay.C;

/// <summary>Generator kodu stub z programu po kontroli typów: tekst asemblera
/// (segment CODE z funkcjami w <c>.proc</c>, segment DATA z komórkami).
/// Konwencja wg <c>docs/stub-calling-conv.md</c>: A = wartość/arg1/wynik
/// (int: A=lo, X=hi), X roboczy w wyrażeniach, zmienne jako komórki absolutne.
/// Dwa inty nie mieszczą się w (A,X): arg2 idzie przez umówione komórki
/// <c>cc_arg2</c>/<c>cc_arg2_h</c> (caller kopiuje tuż przed CALL, callee
/// odczytuje w prologu — bezpieczne przy zagnieżdżeniu).
/// Podzbiór v1: pełny uchar; int: pamięć, load/store, +,-, porównania, konwersje.
/// Bez: int *,/,%,&lt;&lt;,&gt;&gt;,&amp;,|,^, wskaźników, tablic (jawny błąd).</summary>
public sealed class Codegen
{
    private readonly StringBuilder _code = new();
    private readonly List<(string Name, int Size, byte[]? Init)> _data = [];
    private readonly Dictionary<string, Cell> _cells = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckedFunction> _functions = new(StringComparer.Ordinal);
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
    /// <returns>Źródło dla <c>cathode asm --cpu stub</c>.</returns>
    public static string Emit(CheckedProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        var gen = new Codegen();
        return gen.Run(program);
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

    private string Run(CheckedProgram program)
    {
        _globals = program.Globals;
        foreach (CheckedFunction function in program.Functions)
        {
            _functions[function.Def.Name] = function;
        }

        foreach (TypedSymbol global in program.Globals)
        {
            DataCell($"cc_g_{global.Name}", global.Type, InitBytes(global));
        }

        DataCell("cc_arg2", CType.UChar);
        DataCell("cc_arg2_h", CType.UChar);

        _code.AppendLine(".segment \"CODE\"");
        if (_functions.ContainsKey("main"))
        {
            _code.AppendLine("CALL main");
            _code.AppendLine("HLT");
        }

        foreach (CheckedFunction function in program.Functions)
        {
            EmitFunction(function);
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
        foreach ((string name, int size, byte[]? init) in _data)
        {
            if (init is not null)
            {
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
            else if (size == 1)
            {
                data.AppendLine($"{name}: .byte 0");
            }
            else
            {
                data.AppendLine($"{name}: .byte 0");
                data.AppendLine($"{name}_h: .byte 0");
            }
        }

        return _code.ToString() + data.ToString();
    }

    private string Label(string hint) => $"L{++_labels}_{hint}";

    private void DataCell(string name, CType type, byte[]? init = null) =>
        _data.Add((name, type.Size, init));

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

        foreach (TypedSymbol param in function.Params)
        {
            var cell = new Cell($"{_prefix}__{param.Name}", param.Type);
            _cells[param.Name] = cell;
            DataCell(cell.Lo, param.Type);
        }

        foreach (TypedSymbol local in function.Locals)
        {
            var cell = new Cell($"{_prefix}__{local.Name}", local.Type);
            _cells[local.Name] = cell;
            DataCell(cell.Lo, local.Type);
        }

        _maxTemp = -1;
        _code.AppendLine($".proc {function.Def.Name}");
        _code.AppendLine($".global {function.Def.Name}");

        for (int i = 0; i < function.Params.Count; i++)
        {
            Cell cell = _cells[function.Params[i].Name];
            if (function.Params[i].Type.Kind == "int")
            {
                if (i == 0)
                {
                    _code.AppendLine($"STA {cell.Lo}");
                    _code.AppendLine("TXA");
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
                _code.AppendLine($"STA {cell.Lo}");
            }
            else
            {
                _code.AppendLine("TXA");
                _code.AppendLine($"STA {cell.Lo}");
            }
        }

        foreach (Ast.Stmt item in function.Def.Body.Items)
        {
            EmitStmt(item);
        }

        _code.AppendLine($"{_prefix}__ret:");
        _code.AppendLine("RET");
        _code.AppendLine(".endproc");
        for (int t = 0; t <= _maxTemp; t++)
        {
            DataCell($"{_prefix}__t{t}", CType.UChar);
            DataCell($"{_prefix}__t{t}_h", CType.UChar);
        }
    }

    private (string Lo, CType Type) CellOf(string name) =>
        _cells.TryGetValue(name, out Cell? cell)
            ? (cell.Lo, cell.Type)
            : throw new CCodegenException($"unknown cell '{name}'.");

    private string KindOf(Ast.Expr expr) =>
        _types.TryGetValue(expr, out CType? type) ? type.Kind : "uchar";

    private void EmitStmt(Ast.Stmt stmt)
    {
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
        if (type.Kind == "int")
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

        if (KindOf(cond) == "int")
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

        if (KindOf(cond) == "int")
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
        string jump = (op, jumpWhenTrue) switch
        {
            ("==", false) => "BNE",
            ("==", true) => "BEQ",
            ("!=", false) => "BEQ",
            ("!=", true) => "BNE",
            ("<", false) => "BCS",
            ("<", true) => "BCC",
            ("<=", false) => "BCS",
            ("<=", true) => "BCC",
            (">", false) => "BCC",
            (">", true) => "BCS",
            (">=", false) => "BCC",
            (">=", true) => "BCS",
            _ => throw new CCodegenException($"unknown comparison '{op}'."),
        };
        _code.AppendLine($"{jump} {label}");
    }

    private void Eval(Ast.Expr expr, int depth)
    {
        if (KindOf(expr) == "int")
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
            case Ast.Ternary ternary:
                EmitTernary(ternary, depth);
                break;
            case Ast.Deref:
                throw new CCodegenException("pointers need address arithmetic (plan 20).");
            case Ast.AddressOf:
                throw new CCodegenException("address-of needs linker symbols (plan 23).");
            case Ast.Index:
                throw new CCodegenException("arrays need address arithmetic (plan 20).");
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
                (string cell, CType vtype) = CellOf(variable.Name);
                if (vtype.Kind != "int")
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
            case Ast.Binary binary when binary.Op is "+" or "-":
                EvalIntArith(binary, depth, lo, hi);
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
                if (assignType.Kind != "int")
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

        bool secondInt = call.Args.Count == 2 && target.Params.Count > 1 && target.Params[1].Type.Kind == "int";
        bool firstInt = call.Args.Count >= 1 && target.Params.Count > 0 && target.Params[0].Type.Kind == "int";
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
