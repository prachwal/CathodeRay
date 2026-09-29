using System.Text.RegularExpressions;

namespace CathodeRay.C;

/// <summary>Optymalizator okienkowy tekstu asemblera (przed asemblacją). Reguły są zachowawcze wobec flag
/// (Z ustawia każdy zapis do A/X, C tylko działania) i etykiet (skoki, łatane operandy):
/// (A) <c>STA x</c> + <c>LDA x</c> → sam <c>STA x</c>; (B) martwe ładowanie A tuż przed kolejnym ładowaniem A;
/// (C) <c>JMP L</c> tuż przed <c>L:</c>.</summary>
public static partial class Peephole
{
    /// <summary>Optymalizuje kod.</summary>
    /// <param name="asm">Tekst asemblera (CODE).</param>
    /// <returns>Krótszy, równoważny tekst.</returns>
    public static string Optimize(string asm)
    {
        ArgumentNullException.ThrowIfNull(asm);
        var lines = new List<string>(asm.Split('\n'));
        bool changed = true;
        while (changed)
        {
            changed = false;
            var result = new List<string>(lines.Count);
            foreach (string line in lines)
            {
                if (Try(result, line))
                {
                    changed = true;
                    continue;
                }

                result.Add(line);
            }

            int before = result.Count;
            lines = DropJumpsToNext(result);
            changed |= lines.Count != before;
        }

        return string.Join('\n', lines);
    }

    private static bool Try(List<string> result, string line)
    {
        (string? label, string op, string operand) = Split(line);
        if (op.Length == 0)
        {
            return false;
        }

        int prev = PreviousInstruction(result);
        if (prev < 0)
        {
            return false;
        }

        (string? prevLabel, string prevOp, string prevOperand) = Split(result[prev]);
        if (op == "LDA" && label is null && prevOp == "STA" && prevOperand == operand)
        {
            return true;
        }

        if (prevLabel is null && IsPureLoadA(prevOp) && IsPureLoadA(op))
        {
            result.RemoveAt(prev);
            result.Add(line);
            return true;
        }

        return false;
    }

    private static List<string> DropJumpsToNext(List<string> lines)
    {
        var result = new List<string>(lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            (string? label, string op, string operand) = Split(lines[i]);
            if (op == "JMP" && operand.Length > 0)
            {
                int next = i + 1;
                while (next < lines.Count && IsComment(lines[next]))
                {
                    next++;
                }

                if (next < lines.Count && Split(lines[next]).Label == operand)
                {
                    if (label is not null)
                    {
                        result.Add(label + ":");
                    }

                    continue;
                }
            }

            result.Add(lines[i]);
        }

        return result;
    }

    private static int PreviousInstruction(List<string> result)
    {
        for (int i = result.Count - 1; i >= 0; i--)
        {
            if (IsComment(result[i]))
            {
                continue;
            }

            return Split(result[i]).Op.Length > 0 ? i : -1;
        }

        return -1;
    }

    private static bool IsComment(string line)
    {
        string trimmed = line.Trim();
        return trimmed.Length == 0 || trimmed.StartsWith(';');
    }

    private static bool IsPureLoadA(string op) => op is "LDA" or "LDI" or "TXA";

    private static (string? Label, string Op, string Operand) Split(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('.'))
        {
            return (null, string.Empty, string.Empty);
        }

        string? label = null;
        Match match = LabelPrefix().Match(trimmed);
        if (match.Success)
        {
            label = match.Groups[1].Value;
            trimmed = trimmed[match.Length..].TrimStart();
        }

        int semicolon = trimmed.IndexOf(';', StringComparison.Ordinal);
        if (semicolon >= 0)
        {
            trimmed = trimmed[..semicolon].TrimEnd();
        }

        int space = trimmed.IndexOf(' ', StringComparison.Ordinal);
        return space < 0
            ? (label, trimmed, string.Empty)
            : (label, trimmed[..space], trimmed[(space + 1)..].Trim());
    }

    [GeneratedRegex(@"^([A-Za-z_][\w.]*):")]
    private static partial Regex LabelPrefix();
}
