using System.Text.RegularExpressions;

namespace CathodeRay.C;

/// <summary>Optymalizator okienkowy tekstu asemblera (przed asemblacją). Reguły są zachowawcze wobec flag
/// (Z ustawia każdy zapis do A/X, C tylko działania) i etykiet (skoki, łatane operandy):
/// (A) <c>STA x</c> + <c>LDA x</c> → sam <c>STA x</c>; (B) martwe ładowanie A tuż przed kolejnym ładowaniem A;
/// (C) pomijanie łańcuchów skoków z bezpiecznym usunięciem pośrednich etykiet.</summary>
public static partial class Peephole
{
    /// <summary>Optymalizuje kod.</summary>
    /// <param name="asm">Tekst asemblera (CODE).</param>
    /// <param name="volatiles">Symbole <c>volatile</c>: ich odczytów i zapisów reguły nie łączą ani nie usuwają.</param>
    /// <returns>Krótszy, równoważny tekst.</returns>
    public static string Optimize(string asm, IReadOnlySet<string>? volatiles = null)
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
                if (Try(result, line, volatiles))
                {
                    changed = true;
                    continue;
                }

                result.Add(line);
            }

            int before = result.Count;
            lines = ThreadJumpChains(result);
            changed |= lines.Count != before;
            before = lines.Count;
            lines = DropJumpsToNext(lines);
            changed |= lines.Count != before;
        }

        return string.Join('\n', lines);
    }

    private static bool IsVolatile(string operand, IReadOnlySet<string>? volatiles)
    {
        if (volatiles is null || volatiles.Count == 0)
        {
            return false;
        }

        int cut = operand.IndexOfAny(['+', '-', ',']);
        return volatiles.Contains((cut < 0 ? operand : operand[..cut]).Trim());
    }

    private static bool Try(List<string> result, string line, IReadOnlySet<string>? volatiles)
    {
        (string? label, string op, string operand) = Split(line);
        if (op.Length == 0)
        {
            return false;
        }

        if (TryRemoveRedundantLdy(result, line))
        {
            return true;
        }

        if (TryRemoveRedundantLoadZero(result, line))
        {
            return true;
        }

        if (TryRemoveDeadAfterJump(result, line))
        {
            return true;
        }

        int prev = PreviousInstruction(result);
        if (prev < 0)
        {
            return false;
        }

        (string? prevLabel, string prevOp, string prevOperand) = Split(result[prev]);
        if (op == "LDA" && label is null && prevOp == "STA" && prevOperand == operand && !IsVolatile(operand, volatiles))
        {
            return true;
        }

        if (prevLabel is null && IsPureLoadA(prevOp) && IsPureLoadA(op) && !IsVolatile(prevOperand, volatiles))
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

    private static List<string> ThreadJumpChains(List<string> lines)
    {
        var labels = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < lines.Count; i++)
        {
            string? label = Split(lines[i]).Label;
            if (label is not null && !labels.TryAdd(label, i))
            {
                return lines;
            }
        }

        var references = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < lines.Count; i++)
        {
            (string? _, string op, string operand) = Split(lines[i]);
            if ((IsJump(op) || op is "CALL" or "JSR") && labels.ContainsKey(operand))
            {
                references[operand] = references.GetValueOrDefault(operand) + 1;
            }
        }

        for (int source = 0; source < lines.Count; source++)
        {
            (string? _, string op, string operand) = Split(lines[source]);
            if (!IsJump(op) || !labels.ContainsKey(operand))
            {
                continue;
            }

            string destination = operand;
            var visited = new HashSet<string>(StringComparer.Ordinal) { destination };
            var removed = new List<(int Label, int Jump)>();
            for (int depth = 0; depth < 4; depth++)
            {
                if (!labels.TryGetValue(destination, out int labelIndex)
                    || references.GetValueOrDefault(destination) != 1
                    || FallsThrough(lines, labelIndex))
                {
                    break;
                }

                int jumpIndex = NextInstruction(lines, labelIndex);
                if (jumpIndex < 0)
                {
                    break;
                }

                (string? jumpLabel, string jumpOp, string next) = Split(lines[jumpIndex]);
                if (jumpOp != "JMP" || jumpLabel is not null || !labels.ContainsKey(next) || !visited.Add(next))
                {
                    break;
                }

                removed.Add((labelIndex, jumpIndex));
                destination = next;
            }

            if (removed.Count == 0)
            {
                continue;
            }

            var result = new List<string>(lines);
            result[source] = ReplaceJumpTarget(result[source], destination);
            foreach ((int labelIndex, int jumpIndex) in removed.OrderByDescending(static pair => pair.Jump))
            {
                result.RemoveAt(jumpIndex);
                if (labelIndex != jumpIndex)
                {
                    result.RemoveAt(labelIndex);
                }
            }

            return result;
        }

        return lines;
    }

    private static bool IsJump(string op) => op is "JMP" or "BEQ" or "BNE" or "BPL" or "BMI" or "BVC" or "BVS" or "BCC" or "BCS";

    private static bool FallsThrough(List<string> lines, int labelIndex)
    {
        for (int i = labelIndex - 1; i >= 0; i--)
        {
            if (IsComment(lines[i]))
            {
                continue;
            }

            (string? label, string op, string _) = Split(lines[i]);
            if (op.Length == 0)
            {
                // Etykieta bez instrukcji może być aliasem celu i oznacza możliwy fall-through.
                return label is not null || !IsComment(lines[i]);
            }

            return op is not ("JMP" or "RTS" or "RET" or "HLT");
        }

        return false;
    }

    private static int NextInstruction(List<string> lines, int labelIndex)
    {
        for (int i = labelIndex; i < lines.Count; i++)
        {
            if (IsComment(lines[i]))
            {
                continue;
            }

            (string? label, string op, string _) = Split(lines[i]);
            if (i == labelIndex && label is not null && op.Length == 0)
            {
                continue;
            }

            return op.Length > 0 ? i : -1;
        }

        return -1;
    }

    private static string ReplaceJumpTarget(string line, string destination)
    {
        string operand = Split(line).Operand;
        int start = line.LastIndexOf(operand, StringComparison.Ordinal);
        return start < 0 ? line : string.Concat(line.AsSpan(0, start), destination.AsSpan(), line.AsSpan(start + operand.Length));
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

    private static bool TryRemoveRedundantLoadZero(List<string> result, string line)
    {
        (string? label, string op, string operand) = Split(line);

        if (op != "LDA" || operand != "#0" || label is not null)
        {
            return false;
        }

        // Scan backwards to find if A is already set to 0 in the current block
        for (int i = result.Count - 1; i >= 0; i--)
        {
            (string? prevLabel, string prevOp, string prevOperand) = Split(result[i]);

            // Skip comments, but check for labels
            if (prevOp.Length == 0)
            {
                if (prevLabel is not null)
                {
                    return false;  // Hit a label, stop
                }

                continue;  // Skip comments
            }

            // If we hit a label, we've left the block
            if (prevLabel is not null)
            {
                return false;
            }

            // If we find a previous LDA #0, this one is redundant
            if (prevOp == "LDA" && prevOperand == "#0")
            {
                return true;
            }

            // If we find any other instruction that is not STA/STX/STY, stop
            if (prevOp is not ("STA" or "STX" or "STY"))
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryRemoveDeadAfterJump(List<string> result, string line)
    {
        (string? label, string op, string operand) = Split(line);

        // Only skip actual instructions that are not labels
        if (label is not null || op.Length == 0)
        {
            return false;
        }

        int prev = PreviousInstruction(result);
        if (prev < 0)
        {
            return false;
        }

        string prevOp = Split(result[prev]).Op;
        if (prevOp is "JMP" or "RTS")
        {
            // Dead code after unconditional jump/return
            return true;
        }

        return false;
    }

    private static bool TryRemoveRedundantLdy(List<string> result, string line)
    {
        (string? label, string op, string operand) = Split(line);

        if (op != "LDY" || label is not null)
        {
            return false;
        }

        // Scan backwards to find if Y is already set to this value in the current block
        for (int i = result.Count - 1; i >= 0; i--)
        {
            (string? prevLabel, string prevOp, string prevOperand) = Split(result[i]);

            // Skip comments
            if (prevOp.Length == 0)
            {
                continue;
            }

            // If we hit a label, we've left the block
            if (prevLabel is not null)
            {
                return false;
            }

            // If we hit a block-ending instruction, stop
            if (IsBlockEnd(prevOp))
            {
                return false;
            }

            // If we find the same LDY #N, this one is redundant
            if (prevOp == "LDY" && prevOperand == operand)
            {
                return true;
            }

            // If Y is modified by any other instruction, stop
            if (ModifiesY(prevOp))
            {
                return false;
            }
        }

        return false;
    }

    private static bool ModifiesY(string op) => op is "LDY" or "INY" or "DEY" or "TAY" or "JSR";

    private static bool IsBlockEnd(string op) => op is "JMP" or "RTS" or "BEQ" or "BNE" or "BPL" or "BMI" or "BVC" or "BVS" or "BCC" or "BCS";

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
