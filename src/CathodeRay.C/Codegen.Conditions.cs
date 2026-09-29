using System.Text;

namespace CathodeRay.C;

/// <summary>Generator kodu: warunki, skoki i porównania.</summary>
public sealed partial class Codegen
{
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

    private void EmitIntCompare(string op, string aLo, string aHi, string bLo, string bHi, string falseLabel, bool signed = false)
    {
        string patchHi = Label("cmp");
        string patchLo = Label("cmp");
        string hiLess = Label("hless");
        string hiEq = Label("heq");
        string loLess = Label("lless");
        string loEq = Label("leq");
        string done = Label("cdone");
        _code.AppendLine($"LDA {bHi}");
        if (signed)
        {
            _code.AppendLine("EOR 128");
        }

        _code.AppendLine($"STA {patchHi}+1");
        _code.AppendLine($"LDA {aHi}");
        if (signed)
        {
            _code.AppendLine("EOR 128");
        }

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

        if (IsWideKind(cmp.Left) || IsWideKind(cmp.Right))
        {
            EvalInt(cmp.Left, depth, out string leftLo, out string leftHi);
            EvalInt(cmp.Right, depth + 1, out string rightLo, out string rightHi);
            EmitIntCompare(op, leftLo, leftHi, rightLo, rightHi, falseLabel, KindOf(cmp.Left) != "ptr" && KindOf(cmp.Right) != "ptr");
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
}
