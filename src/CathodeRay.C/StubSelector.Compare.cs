namespace CathodeRay.C;

/// <summary>Selektor stuba: porównania i skoki warunkowe. Po SUB/CPA flaga C oznacza „brak pożyczki” (a &gt;= b), Z — równość.</summary>
internal sealed partial class StubSelector
{
    private static string OperatorOf(Ir.Cond condition) => condition switch
    {
        Ir.Cond.Eq => "==",
        Ir.Cond.Ne => "!=",
        Ir.Cond.Lt or Ir.Cond.Ltu => "<",
        Ir.Cond.Le or Ir.Cond.Leu => "<=",
        Ir.Cond.Gt or Ir.Cond.Gtu => ">",
        _ => ">=",
    };

    private static string InvertOperator(string op) => op switch
    {
        "==" => "!=",
        "!=" => "==",
        "<" => ">=",
        ">=" => "<",
        "<=" => ">",
        _ => "<=",
    };

    private static bool IsSigned(Ir.Cond condition) => condition is Ir.Cond.Lt or Ir.Cond.Le or Ir.Cond.Gt or Ir.Cond.Ge;

    private void EmitBranch(Ir.BrCmp branch)
    {
        bool zeroTest = branch.C is Ir.Cond.Eq or Ir.Cond.Ne && branch.B is Ir.Imm { Value: 0 };
        if (zeroTest)
        {
            string mnemonic = branch.C == Ir.Cond.Eq ? "BEQ" : "BNE";
            if (WidthOf(branch.A) == 1)
            {
                LoadA(branch.A, 0);
                Line($"{mnemonic} {branch.Target}");
                return;
            }

            Octet high = ByteOf(branch.A, 1);
            if (NeedsIndex(high))
            {
                Line("LDX 0");
            }

            LoadA(branch.A, 0);
            Alu("ORA", high);
            Line($"{mnemonic} {branch.Target}");
            return;
        }

        int width = Math.Max(WidthOf(branch.A), WidthOf(branch.B));
        if (width == 1)
        {
            SubOrCompare("CPA", ByteOf(branch.B, 0), () => LoadA(branch.A, 0));
            BranchOn(OperatorOf(branch.C), branch.Target);
            return;
        }

        BranchCompare(branch.C, branch.A, branch.B, branch.Target);
    }

    /// <summary>Skok po porównaniu 8-bitowym (flagi z CPA/SUB).</summary>
    private void BranchOn(string op, string target)
    {
        switch (op)
        {
            case "==":
                Line($"BEQ {target}");
                break;
            case "!=":
                Line($"BNE {target}");
                break;
            case "<":
                Line($"BCC {target}");
                break;
            case ">=":
                Line($"BCS {target}");
                break;
            case "<=":
                Line($"BEQ {target}");
                Line($"BCC {target}");
                break;
            default:
                string skip = Label("ngt");
                Line($"BEQ {skip}");
                Line($"BCS {target}");
                Line($"{skip}:");
                break;
        }
    }

    /// <summary>Skok 16-bitowy do <paramref name="target"/>, gdy <c>a cond b</c>.</summary>
    private void BranchCompare(Ir.Cond condition, Ir.Op a, Ir.Op b, string target) =>
        CompareJumpIfFalse(InvertOperator(OperatorOf(condition)), IsSigned(condition), a, b, target);

    /// <summary>Porównanie 16-bitowe: skacze do <paramref name="falseLabel"/>, gdy <c>a op b</c> jest fałszem; przy prawdzie
    /// przechodzi dalej. Najpierw starsze bajty (ze znakiem: obie strony z odwróconym bitem 7), potem młodsze.</summary>
    private void CompareJumpIfFalse(string op, bool signed, Ir.Op a, Ir.Op b, string falseLabel)
    {
        Octet a0 = ByteOf(a, 0);
        Octet a1 = ByteOf(a, 1);
        Octet b0 = ByteOf(b, 0);
        Octet b1 = ByteOf(b, 1);
        string hiLess = Label("hless");
        string hiEq = Label("heq");
        string loLess = Label("lless");
        string loEq = Label("leq");
        string done = Label("cdone");
        if (b1.IsImmediate)
        {
            LoadA(a1);
            if (signed)
            {
                Line("EOR 128");
            }

            int value = int.Parse(b1.Text, System.Globalization.CultureInfo.InvariantCulture);
            Line($"SUB {(signed ? value ^ 0x80 : value)}");
        }
        else
        {
            string site = Label("patch");
            Line($"LDA {b1.Text}");
            if (signed)
            {
                Line("EOR 128");
            }

            Line($"STA {site}+1");
            LoadA(a1);
            if (signed)
            {
                Line("EOR 128");
            }

            Line($"{site}: SUB 0");
        }

        Line($"BCC {hiLess}");
        Line($"BEQ {hiEq}");
        Line($"JMP {(op is "<" or "<=" or "==" ? falseLabel : done)}");
        Line($"{hiLess}:");
        Line($"JMP {(op is "<" or "<=" or "!=" ? done : falseLabel)}");
        Line($"{hiEq}:");
        SubOrCompare("SUB", b0, () => LoadA(a0));
        Line($"BCC {loLess}");
        Line($"BEQ {loEq}");
        Line($"JMP {(op is ">" or ">=" or "!=" ? done : falseLabel)}");
        Line($"{loLess}:");
        Line($"JMP {(op is "<" or "<=" or "!=" ? done : falseLabel)}");
        Line($"{loEq}:");
        Line($"JMP {(op is "==" or "<=" or ">=" ? done : falseLabel)}");
        Line($"{done}:");
    }
}
