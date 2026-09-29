using System.Text;

namespace CathodeRay.C;

/// <summary>Generator kodu: operatory int (bitowe, przesunięcia, mnożenie/dzielenie).</summary>
public sealed partial class Codegen
{
    private void EmitIntBitwise(Ast.Binary binary, int depth, string lo, string hi)
    {
        EvalInt(binary.Left, depth + 1, out string leftLo, out string leftHi);
        EvalInt(binary.Right, depth + 2, out string rightLo, out string rightHi);
        string op = binary.Op switch { "&" => "AND", "|" => "ORA", _ => "EOR" };
        _code.AppendLine("LDX 0");
        _code.AppendLine($"LDA {leftLo}");
        _code.AppendLine($"{op} {rightLo},X");
        _code.AppendLine($"STA {lo}");
        _code.AppendLine($"LDA {leftHi}");
        _code.AppendLine($"{op} {rightHi},X");
        _code.AppendLine($"STA {hi}");
    }

    /// <summary>Przesunięcie 16-bit (<c>&gt;&gt;</c> arytmetyczne: int jest ze znakiem).</summary>
    private void EmitIntShift(Ast.Binary binary, int depth, string lo, string hi)
    {
        EvalInt(binary.Left, depth + 1, out string valueLo, out string valueHi);
        _code.AppendLine($"LDA {valueLo}");
        _code.AppendLine($"STA {lo}");
        _code.AppendLine($"LDA {valueHi}");
        _code.AppendLine($"STA {hi}");
        string count = Temp(depth + 1, hi: false);
        Eval(binary.Right, depth + 2);
        _code.AppendLine($"STA {count}");
        string loop = Label("wsh");
        string done = Label("wshd");
        _code.AppendLine($"{loop}:");
        _code.AppendLine($"LDA {count}");
        _code.AppendLine("CPA 0");
        _code.AppendLine($"BEQ {done}");
        if (binary.Op == "<<")
        {
            _code.AppendLine("LDX 0");
            _code.AppendLine($"LDA {lo}");
            _code.AppendLine($"ADD {lo},X");
            _code.AppendLine($"STA {lo}");
            _code.AppendLine($"LDA {hi}");
            _code.AppendLine($"ADC {hi},X");
            _code.AppendLine($"STA {hi}");
        }
        else
        {
            string carry = Label("wshc");
            string next = Label("wshn");
            string sign = Temp(depth + 2, hi: false);
            _code.AppendLine($"LDA {hi}");
            _code.AppendLine("AND 128");
            _code.AppendLine($"STA {sign}");
            _code.AppendLine($"LDA {hi}");
            _code.AppendLine("SHR");
            _code.AppendLine($"STA {hi}");
            _code.AppendLine($"BCS {carry}");
            _code.AppendLine($"LDA {lo}");
            _code.AppendLine("SHR");
            _code.AppendLine($"STA {lo}");
            _code.AppendLine($"JMP {next}");
            _code.AppendLine($"{carry}:");
            _code.AppendLine($"LDA {lo}");
            _code.AppendLine("SHR");
            _code.AppendLine("ADD 128");
            _code.AppendLine($"STA {lo}");
            _code.AppendLine($"{next}:");
            _code.AppendLine("LDX 0");
            _code.AppendLine($"LDA {hi}");
            _code.AppendLine($"ADD {sign},X");
            _code.AppendLine($"STA {hi}");
        }

        _code.AppendLine($"LDA {count}");
        _code.AppendLine("SUB 1");
        _code.AppendLine($"STA {count}");
        _code.AppendLine($"JMP {loop}");
        _code.AppendLine($"{done}:");
    }

    private void EmitIntMulDiv(Ast.Binary binary, int depth, string lo, string hi)
    {
        EvalInt(binary.Left, depth + 1, out string leftLo, out string leftHi);
        EvalInt(binary.Right, depth + 2, out string rightLo, out string rightHi);
        _code.AppendLine($"LDA {leftLo}");
        _code.AppendLine("STA cc_w_a");
        _code.AppendLine($"LDA {leftHi}");
        _code.AppendLine("STA cc_w_a_h");
        _code.AppendLine($"LDA {rightLo}");
        _code.AppendLine("STA cc_w_b");
        _code.AppendLine($"LDA {rightHi}");
        _code.AppendLine("STA cc_w_b_h");
        if (binary.Op == "*")
        {
            _needMul16 = true;
            _code.AppendLine("CALL cc_mul16");
        }
        else
        {
            _needDiv16 = true;
            _code.AppendLine("CALL cc_sdiv16");
        }

        if (binary.Op == "%")
        {
            _code.AppendLine("LDA cc_w_r");
            _code.AppendLine($"STA {lo}");
            _code.AppendLine("LDA cc_w_r_h");
            _code.AppendLine($"STA {hi}");
            return;
        }

        _code.AppendLine($"STA {lo}");
        _code.AppendLine("TXA");
        _code.AppendLine($"STA {hi}");
    }
}
