using System.Text;

namespace CathodeRay.C;

/// <summary>Generator kodu: wyrażenia 8- i 16-bitowe.</summary>
public sealed partial class Codegen
{
    private void Eval(Ast.Expr expr, int depth)
    {
        if (expr is Ast.Deref or Ast.Index or Ast.AssignTo or Ast.AssignOpTo or Ast.Member or Ast.AddressOfExpr || IsWideKind(expr))
        {
            EvalInt(expr, depth, out _, out _);
            return;
        }

        if (expr is not Ast.Number && _constants.TryGetValue(expr, out int folded))
        {
            _code.AppendLine($"LDI {folded & 0xFF}");
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
            case Ast.CallExpr callExpr:
                EmitCallExpr(callExpr, depth);
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
            case not Ast.Number when _constants.TryGetValue(expr, out int constant):
                _code.AppendLine($"LDX {(constant >> 8) & 0xFF}");
                _code.AppendLine($"LDI {constant & 0xFF}");
                _code.AppendLine($"STA {lo}");
                _code.AppendLine("TXA");
                _code.AppendLine($"STA {hi}");
                break;
            case Ast.Number number when TryNumber(number.Text, out int value):
                _code.AppendLine($"LDX {(value >> 8) & 0xFF}");
                _code.AppendLine($"LDI {value & 0xFF}");
                _code.AppendLine($"STA {lo}");
                _code.AppendLine("TXA");
                _code.AppendLine($"STA {hi}");
                break;
            case Ast.Var variable when !_cells.ContainsKey(variable.Name) && _functions.ContainsKey(variable.Name):
                EmitAddressOf(variable.Name, lo, hi);
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
                _code.AppendLine($"LDA {Hi(cell)}");
                _code.AppendLine($"STA {hi}");
                break;
            }

            case Ast.Call or Ast.CallExpr when IsWideKind(expr):
                EmitAnyCall(expr, depth);
                _code.AppendLine($"STA {lo}");
                _code.AppendLine("TXA");
                _code.AppendLine($"STA {hi}");
                _code.AppendLine($"LDA {lo}");
                break;
            case Ast.Call or Ast.CallExpr:
                EmitAnyCall(expr, depth);
                _code.AppendLine($"STA {lo}");
                _code.AppendLine("LDX 0");
                _code.AppendLine("TXA");
                _code.AppendLine($"STA {hi}");
                break;
            case Ast.Binary { Op: "-" } binary when KindOf(binary.Left) == "ptr" && KindOf(binary.Right) == "ptr":
                EmitPtrDiff(binary, depth, lo, hi);
                break;
            case Ast.Binary binary when binary.Op is "+" or "-"
                && (KindOf(binary.Left) == "ptr" || KindOf(binary.Right) == "ptr"):
                EmitPtrArith(binary, depth, lo, hi);
                break;
            case Ast.Binary binary when binary.Op is "+" or "-":
                EvalIntArith(binary, depth, lo, hi);
                break;
            case Ast.AddressOf addressOf when !_cells.ContainsKey(addressOf.Name) && _functions.ContainsKey(addressOf.Name):
                EmitAddressOf(addressOf.Name, lo, hi);
                break;
            case Ast.AddressOf addressOf:
            {
                (string acell, _) = CellOf(addressOf.Name);
                EmitAddressOf(acell, lo, hi);
                break;
            }

            case Ast.Deref { Pointer: var target } when KindOf(target) == "fptr":
                EvalInt(target, depth, out string flo, out string fhi);
                _code.AppendLine($"LDA {flo}");
                _code.AppendLine($"STA {lo}");
                _code.AppendLine($"LDA {fhi}");
                _code.AppendLine($"STA {hi}");
                break;
            case Ast.Deref deref:
                EvalPtrAddr(deref, depth, out string dalo, out string dahi);
                PatchedLoad(dalo, dahi, ElemSize(TypeOfDeref(deref)), lo, hi, depth);
                break;
            case Ast.Index index:
                EvalPtrAddr(index, depth, out string ialo, out string iahi);
                PatchedLoad(ialo, iahi, ElemSize(TypeOfIndex(index)), lo, hi, depth);
                break;
            case Ast.AssignTo or Ast.AssignOpTo:
                StorePtr(expr is Ast.AssignOpTo assignOp ? LowerAssignOp(assignOp, depth) : (Ast.AssignTo)expr, depth);
                _code.AppendLine($"LDA {Temp(depth, hi: false)}");
                _code.AppendLine($"LDA {Temp(depth, hi: true)}");
                _code.AppendLine("TAX");
                _code.AppendLine($"LDA {Temp(depth, hi: false)}");
                break;
            case Ast.Binary binary when binary.Op is "==" or "!=" or "<" or "<=" or ">" or ">=" or "&&" or "||":
            case Ast.Unary { Op: "!" }:
                EmitCompareValueTo(expr, depth, lo, hi);
                break;
            case Ast.Str str:
                EmitAddressOf(StringLabel(str.Value), lo, hi);
                break;
            case Ast.Member member:
            {
                EvalPtrAddr(member, depth, out string malo, out string mahi);
                CType fieldType = FieldOf(member).Type;
                if (fieldType.Kind != "array")
                {
                    PatchedLoad(malo, mahi, ElemSize(fieldType), lo, hi, depth);
                }

                break;
            }

            case Ast.AddressOfExpr addressOf:
                EvalPtrAddr(addressOf.Target, depth, out _, out _);
                break;
            case Ast.Binary binary when !IsWideKind(binary):
                // dwa uchary: wynik 8-bit, rozszerzony zerem
                Eval(binary, depth + 1);
                _code.AppendLine($"STA {lo}");
                _code.AppendLine("LDX 0");
                _code.AppendLine("TXA");
                _code.AppendLine($"STA {hi}");
                break;
            case Ast.Binary { Op: "&" or "|" or "^" } binary:
                EmitIntBitwise(binary, depth, lo, hi);
                break;
            case Ast.Binary { Op: "<<" or ">>" } binary:
                EmitIntShift(binary, depth, lo, hi);
                break;
            case Ast.Binary { Op: "*" or "/" or "%" } binary:
                EmitIntMulDiv(binary, depth, lo, hi);
                break;
            case Ast.Binary binary:
                throw new CCodegenException($"int operator '{binary.Op}' is not supported.");
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

                _code.AppendLine($"LDA {Hi(assignCell)}");
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
}
