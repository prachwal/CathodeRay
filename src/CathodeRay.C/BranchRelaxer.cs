using System.Text;
using System.Text.RegularExpressions;

namespace CathodeRay.C;

/// <summary>Relaksacja skoków warunkowych CPU z krótkimi (względnymi) rozgałęzieniami: selektor emituje bezpieczną postać
/// <c>bXX pomiń; jmp cel; pomiń:</c> (5 B), a tu każda taka trójka, której cel jest w zasięgu, staje się jednym skokiem
/// <c>bYY cel</c> (2 B) z odwróconym warunkiem. Zasięg liczony na układzie z długimi skokami; skracanie innych trójek tylko go zmniejsza,
/// więc wszystkie zamiany można wykonać jednocześnie.</summary>
internal static partial class BranchRelaxer
{
    private const int Reach = 120;

    /// <summary>Zamienia trójki w tekście funkcji.</summary>
    /// <param name="text">Tekst asemblera funkcji.</param>
    /// <param name="size">Rozmiar linii (instrukcji) w bajtach; etykiety i komentarze mają 0.</param>
    /// <param name="invert">Odwrócenie mnemonika skoku warunkowego (<c>bne</c> → <c>beq</c>) albo <see langword="null"/>, gdy to nie jest skok warunkowy.</param>
    /// <returns>Nowy tekst albo ten sam obiekt, gdy nic się nie zmieniło.</returns>
    public static string Apply(string text, Func<string, int> size, Func<string, string?> invert)
    {
        string[] lines = text.Split('\n');
        var offsets = new int[lines.Length + 1];
        var labels = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            offsets[i + 1] = offsets[i] + (line.Length == 0 || line[0] == ';' || line[^1] == ':' ? 0 : size(line));
            if (line.Length > 0 && line[^1] == ':')
            {
                labels[line[..^1]] = offsets[i];
            }
        }

        var output = new StringBuilder(text.Length);
        bool changed = false;
        for (int i = 0; i < lines.Length; i++)
        {
            if (i + 2 < lines.Length && Triple(lines, i) is { } triple && invert(triple.Branch) is { } inverted && labels.TryGetValue(triple.Target, out int at))
            {
                int distance = at - (offsets[i] + 2);
                if (distance is >= -Reach and <= Reach)
                {
                    output.Append("        ").Append(inverted).Append(' ').Append(triple.Target).Append('\n');
                    i += 2;
                    changed = true;
                    continue;
                }
            }

            output.Append(lines[i]);
            if (i < lines.Length - 1)
            {
                output.Append('\n');
            }
        }

        return changed ? output.ToString() : text;
    }

    private static (string Branch, string Target)? Triple(string[] lines, int index)
    {
        Match first = BranchLine().Match(lines[index]);
        Match jump = JumpLine().Match(lines[index + 1]);
        if (!first.Success || !jump.Success || lines[index + 2].Trim() != first.Groups[2].Value + ":")
        {
            return null;
        }

        return (first.Groups[1].Value, jump.Groups[1].Value);
    }

    [GeneratedRegex(@"^\s*(b[a-z]{2})\s+(\S+)\s*$")]
    private static partial Regex BranchLine();

    [GeneratedRegex(@"^\s*jmp\s+(\S+)\s*$")]
    private static partial Regex JumpLine();
}
