using System.Text;

namespace CathodeRay.C;

/// <summary>Generator kodu: podprogramy pomocnicze emitowane w module (mnożenie, dzielenie).</summary>
public sealed partial class Codegen
{
    private void WideCells()
    {
        if (_wideCells)
        {
            return;
        }

        _wideCells = true;
        foreach (string cell in new[] { "cc_w_a", "cc_w_a_h", "cc_w_b", "cc_w_b_h", "cc_w_r", "cc_w_r_h", "cc_w_n", "cc_w_sa", "cc_w_sb", "cc_w_q", "cc_w_q_h" })
        {
            DataCell(cell, CType.UChar);
        }
    }

    /// <summary>a*b mod 2^16 (wejście: cc_w_a/b, wynik A=lo, X=hi): 16 kroków, res=2res, gdy bit 15 b: res+=a.</summary>
    private void EmitMul16()
    {
        WideCells();
        _code.AppendLine(".proc cc_mul16");
        _code.AppendLine("LDX 0");
        _code.AppendLine("LDI 0");
        _code.AppendLine("STA cc_w_r");
        _code.AppendLine("STA cc_w_r_h");
        _code.AppendLine("LDI 16");
        _code.AppendLine("STA cc_w_n");
        _code.AppendLine("cc_m16_loop: LDA cc_w_r");
        _code.AppendLine("ADD cc_w_r,X");
        _code.AppendLine("STA cc_w_r");
        _code.AppendLine("LDA cc_w_r_h");
        _code.AppendLine("ADC cc_w_r_h,X");
        _code.AppendLine("STA cc_w_r_h");
        _code.AppendLine("LDA cc_w_b");
        _code.AppendLine("ADD cc_w_b,X");
        _code.AppendLine("STA cc_w_b");
        _code.AppendLine("LDA cc_w_b_h");
        _code.AppendLine("ADC cc_w_b_h,X");
        _code.AppendLine("STA cc_w_b_h");
        _code.AppendLine("BCC cc_m16_skip");
        _code.AppendLine("LDA cc_w_r");
        _code.AppendLine("ADD cc_w_a,X");
        _code.AppendLine("STA cc_w_r");
        _code.AppendLine("LDA cc_w_r_h");
        _code.AppendLine("ADC cc_w_a_h,X");
        _code.AppendLine("STA cc_w_r_h");
        _code.AppendLine("cc_m16_skip: LDA cc_w_n");
        _code.AppendLine("SUB 1");
        _code.AppendLine("STA cc_w_n");
        _code.AppendLine("BNE cc_m16_loop");
        _code.AppendLine("LDA cc_w_r_h");
        _code.AppendLine("TAX");
        _code.AppendLine("LDA cc_w_r");
        _code.AppendLine("RET");
        _code.AppendLine(".endproc");
    }

    /// <summary>a/b bez znaku (wejście: cc_w_a/b, iloraz A=lo, X=hi, reszta cc_w_r); b=0 daje 0/0.
    /// Dzielenie pisemne: a jest zarazem ilorazem (bit 0 zwalnia się po przesunięciu).</summary>
    private void EmitDiv16()
    {
        WideCells();
        _code.AppendLine(".proc cc_div16");
        _code.AppendLine("LDX 0");
        _code.AppendLine("LDI 0");
        _code.AppendLine("STA cc_w_r");
        _code.AppendLine("STA cc_w_r_h");
        _code.AppendLine("LDA cc_w_b");
        _code.AppendLine("ORA cc_w_b_h,X");
        _code.AppendLine("BEQ cc_d16_zero");
        _code.AppendLine("LDI 16");
        _code.AppendLine("STA cc_w_n");
        _code.AppendLine("cc_d16_loop: LDA cc_w_a");
        _code.AppendLine("ADD cc_w_a,X");
        _code.AppendLine("STA cc_w_a");
        _code.AppendLine("LDA cc_w_a_h");
        _code.AppendLine("ADC cc_w_a_h,X");
        _code.AppendLine("STA cc_w_a_h");
        _code.AppendLine("LDA cc_w_r");
        _code.AppendLine("ADC cc_w_r,X");
        _code.AppendLine("STA cc_w_r");
        _code.AppendLine("LDA cc_w_r_h");
        _code.AppendLine("ADC cc_w_r_h,X");
        _code.AppendLine("STA cc_w_r_h");
        _code.AppendLine("BCS cc_d16_sub");
        EmitIntCompare(">=", "cc_w_r", "cc_w_r_h", "cc_w_b", "cc_w_b_h", "cc_d16_next");
        _code.AppendLine("cc_d16_sub: NOP");
        EmitSub16("cc_w_r", "cc_w_r_h", "cc_w_b", "cc_w_b_h");
        _code.AppendLine("LDA cc_w_a");
        _code.AppendLine("INC");
        _code.AppendLine("STA cc_w_a");
        _code.AppendLine("cc_d16_next: LDA cc_w_n");
        _code.AppendLine("SUB 1");
        _code.AppendLine("STA cc_w_n");
        _code.AppendLine("BNE cc_d16_loop");
        _code.AppendLine("LDA cc_w_a_h");
        _code.AppendLine("TAX");
        _code.AppendLine("LDA cc_w_a");
        _code.AppendLine("RET");
        _code.AppendLine("cc_d16_zero: LDI 0");
        _code.AppendLine("TAX");
        _code.AppendLine("RET");
        _code.AppendLine(".endproc");
    }

    /// <summary>a/b ze znakiem (C: iloraz do zera, reszta ma znak dzielnej) na bazie cc_div16.</summary>
    private void EmitSDiv16()
    {
        _code.AppendLine(".proc cc_sdiv16");
        _code.AppendLine("LDX 0");
        _code.AppendLine("LDA cc_w_a_h");
        _code.AppendLine("AND 128");
        _code.AppendLine("STA cc_w_sa");
        _code.AppendLine("LDA cc_w_b_h");
        _code.AppendLine("AND 128");
        _code.AppendLine("STA cc_w_sb");
        _code.AppendLine("LDA cc_w_sa");
        _code.AppendLine("BEQ cc_sd_ap");
        EmitNeg16("cc_w_a", "cc_w_a_h");
        _code.AppendLine("cc_sd_ap: LDA cc_w_sb");
        _code.AppendLine("BEQ cc_sd_bp");
        EmitNeg16("cc_w_b", "cc_w_b_h");
        _code.AppendLine("cc_sd_bp: CALL cc_div16");
        _code.AppendLine("STA cc_w_q");
        _code.AppendLine("TXA");
        _code.AppendLine("STA cc_w_q_h");
        _code.AppendLine("LDX 0");
        _code.AppendLine("LDA cc_w_sa");
        _code.AppendLine("EOR cc_w_sb,X");
        _code.AppendLine("BEQ cc_sd_qp");
        EmitNeg16("cc_w_q", "cc_w_q_h");
        _code.AppendLine("cc_sd_qp: LDA cc_w_sa");
        _code.AppendLine("BEQ cc_sd_rp");
        EmitNeg16("cc_w_r", "cc_w_r_h");
        _code.AppendLine("cc_sd_rp: LDA cc_w_q_h");
        _code.AppendLine("TAX");
        _code.AppendLine("LDA cc_w_q");
        _code.AppendLine("RET");
        _code.AppendLine(".endproc");
    }

    /// <summary>Negacja dopełnieniowa pary w miejscu (~x + 1).</summary>
    private void EmitNeg16(string lo, string hi)
    {
        string done = Label("neg");
        _code.AppendLine($"LDA {hi}");
        _code.AppendLine("NOT");
        _code.AppendLine($"STA {hi}");
        _code.AppendLine($"LDA {lo}");
        _code.AppendLine("NOT");
        _code.AppendLine("INC");
        _code.AppendLine($"STA {lo}");
        _code.AppendLine($"BNE {done}");
        _code.AppendLine($"LDA {hi}");
        _code.AppendLine("INC");
        _code.AppendLine($"STA {hi}");
        _code.AppendLine($"{done}:");
    }

    /// <summary>a -= b (16-bit, w miejscu; pożyczka z młodszego bajtu bez mutowania b).</summary>
    private void EmitSub16(string aLo, string aHi, string bLo, string bHi)
    {
        string p1 = Label("s16");
        string p2 = Label("s16");
        string p3 = Label("s16");
        string noBorrow = Label("s16nb");
        string end = Label("s16e");
        _code.AppendLine($"LDA {bLo}");
        _code.AppendLine($"STA {p1}+1");
        _code.AppendLine($"LDA {aLo}");
        _code.AppendLine($"{p1}: SUB 0");
        _code.AppendLine($"STA {aLo}");
        _code.AppendLine($"BCS {noBorrow}");
        _code.AppendLine($"LDA {bHi}");
        _code.AppendLine($"STA {p2}+1");
        _code.AppendLine($"LDA {aHi}");
        _code.AppendLine($"{p2}: SUB 0");
        _code.AppendLine("SUB 1");
        _code.AppendLine($"STA {aHi}");
        _code.AppendLine($"JMP {end}");
        _code.AppendLine($"{noBorrow}:");
        _code.AppendLine($"LDA {bHi}");
        _code.AppendLine($"STA {p3}+1");
        _code.AppendLine($"LDA {aHi}");
        _code.AppendLine($"{p3}: SUB 0");
        _code.AppendLine($"STA {aHi}");
        _code.AppendLine($"{end}:");
    }

    /// <summary>a*b mod 256 (A, X): shift-add po bitach mnożnika (do 8 obrotów zamiast b powtórzeń).
    /// Bez <c>.global</c>: każdy moduł ma własną kopię.</summary>
    private void EmitMul()
    {
        _code.AppendLine(".proc cc_mul8");
        _code.AppendLine("STA cc_m_a");
        _code.AppendLine("TXA");
        _code.AppendLine("STA cc_m_b");
        _code.AppendLine("LDX 0");
        _code.AppendLine("LDI 0");
        _code.AppendLine("STA cc_m_acc");
        _code.AppendLine("cc_m_loop: LDA cc_m_b");
        _code.AppendLine("BEQ cc_m_done");
        _code.AppendLine("SHR");
        _code.AppendLine("STA cc_m_b");
        _code.AppendLine("BCC cc_m_skip");
        _code.AppendLine("LDA cc_m_acc");
        _code.AppendLine("ADD cc_m_a,X");
        _code.AppendLine("STA cc_m_acc");
        _code.AppendLine("cc_m_skip: LDA cc_m_a");
        _code.AppendLine("ADD cc_m_a,X");
        _code.AppendLine("STA cc_m_a");
        _code.AppendLine("JMP cc_m_loop");
        _code.AppendLine("cc_m_done: LDA cc_m_acc");
        _code.AppendLine("RET");
        _code.AppendLine(".endproc");
        DataCell("cc_m_acc", CType.UChar);
        DataCell("cc_m_a", CType.UChar);
        DataCell("cc_m_b", CType.UChar);
    }

    /// <summary>A / X bez znaku: iloraz w A, reszta w X (X=0 daje 0/0). Dzielenie pisemne w 8 krokach;
    /// iloraz zbiera się w dzielnej, dzielnik łata operandy dwóch SUB.</summary>
    private void EmitDiv()
    {
        _code.AppendLine(".proc cc_divmod");
        _code.AppendLine("CPX 0");
        _code.AppendLine("BEQ cc_d_zero");
        _code.AppendLine("STA cc_d_n");
        _code.AppendLine("TXA");
        _code.AppendLine("STA cc_d_p1+1");
        _code.AppendLine("STA cc_d_p2+1");
        _code.AppendLine("LDX 0");
        _code.AppendLine("LDI 0");
        _code.AppendLine("STA cc_d_r");
        _code.AppendLine("LDI 8");
        _code.AppendLine("STA cc_d_c");
        _code.AppendLine("cc_d_loop: LDA cc_d_n");
        _code.AppendLine("SHL");
        _code.AppendLine("STA cc_d_n");
        _code.AppendLine("LDA cc_d_r");
        _code.AppendLine("ADC cc_d_r,X");
        _code.AppendLine("STA cc_d_r");
        _code.AppendLine("BCS cc_d_force");
        _code.AppendLine("cc_d_p1: SUB 0");
        _code.AppendLine("BCC cc_d_next");
        _code.AppendLine("STA cc_d_r");
        _code.AppendLine("JMP cc_d_inc");
        _code.AppendLine("cc_d_force: LDA cc_d_r");
        _code.AppendLine("cc_d_p2: SUB 0");
        _code.AppendLine("STA cc_d_r");
        _code.AppendLine("cc_d_inc: LDA cc_d_n");
        _code.AppendLine("INC");
        _code.AppendLine("STA cc_d_n");
        _code.AppendLine("cc_d_next: LDA cc_d_c");
        _code.AppendLine("SUB 1");
        _code.AppendLine("STA cc_d_c");
        _code.AppendLine("BNE cc_d_loop");
        _code.AppendLine("LDA cc_d_r");
        _code.AppendLine("TAX");
        _code.AppendLine("LDA cc_d_n");
        _code.AppendLine("RET");
        _code.AppendLine("cc_d_zero: LDI 0");
        _code.AppendLine("TAX");
        _code.AppendLine("RET");
        _code.AppendLine(".endproc");
        DataCell("cc_d_n", CType.UChar);
        DataCell("cc_d_r", CType.UChar);
        DataCell("cc_d_c", CType.UChar);
    }
}
