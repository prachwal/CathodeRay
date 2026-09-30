using System.Text;
using System.Text.RegularExpressions;

namespace CathodeRay.C;

/// <summary>Relaksacja skoków warunkowych CPU z krótkimi (względnymi) rozgałęzieniami: selektor emituje bezpieczną postać
/// <c>bXX pomiń; jmp cel; pomiń:</c> (5 B), a tu każda taka trójka, której cel jest w zasięgu, staje się jednym skokiem
/// <c>bYY cel</c> (2 B) z odwróconym warunkiem. Zasięg liczony na układzie z długimi skokami; skracanie innych trójek tylko go zmniejsza,
/// więc wszystkie zamiany można wykonać jednocześnie. <see cref="Shorten"/> robi to samo dla pojedynczych długich skoków (Z80).</summary>
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
        (int[] offsets, Dictionary<string, int> labels) = Layout(lines, size);
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

    /// <summary>Usuwa bezwarunkowy skok (Z80 <c>jp</c>, 8080 <c>jmp</c>) do etykiety, która stoi tuż za nim (dzielą je tylko etykiety
    /// i komentarze), np. skok na koniec <c>if</c> z pustą gałęzią <c>else</c>.</summary>
    /// <param name="text">Tekst asemblera funkcji.</param>
    /// <returns>Tekst bez takich skoków.</returns>
    public static string DropJumpToNext(string text) => JumpToNext().Replace(text, "$2");

    /// <summary>Zamienia pojedyncze długie skoki na krótkie względne (Z80: <c>jp cc,L</c> → <c>jr cc,L</c>), gdy cel jest w zasięgu.
    /// Odległość = adres celu − adres skoku, liczona na układzie z długimi skokami; w przód pomniejszona o skrócenie samego skoku
    /// (leży między nim a celem). Skracanie innych skoków tylko zbliża cele, więc wszystkie zamiany można wykonać jednocześnie.</summary>
    /// <param name="text">Tekst asemblera funkcji.</param>
    /// <param name="size">Rozmiar linii (instrukcji) w bajtach; wolno go zawyżać, nie wolno zaniżać.</param>
    /// <param name="shorten">Krótka postać skoku i jego cel albo <see langword="null"/>, gdy linia nie ma krótkiej postaci.</param>
    /// <param name="back">Najmniejsza dopuszczalna odległość (ujemna).</param>
    /// <param name="forward">Największa dopuszczalna odległość.</param>
    /// <returns>Nowy tekst albo ten sam obiekt, gdy nic się nie zmieniło.</returns>
    public static string Shorten(string text, Func<string, int> size, Func<string, (string Short, string Target)?> shorten, int back, int forward)
    {
        string[] lines = text.Split('\n');
        (int[] offsets, Dictionary<string, int> labels) = Layout(lines, size);
        bool changed = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line[0] == ';' || line[^1] == ':' || shorten(line) is not { } jump || !labels.TryGetValue(jump.Target, out int at))
            {
                continue;
            }

            int distance = at - offsets[i];
            if (distance >= back && distance - (size(line) - size(jump.Short)) <= forward)
            {
                lines[i] = jump.Short;
                changed = true;
            }
        }

        return changed ? string.Join('\n', lines) : text;
    }

    /// <summary>Adresy linii (od początku tekstu) i etykiet.</summary>
    private static (int[] Offsets, Dictionary<string, int> Labels) Layout(string[] lines, Func<string, int> size)
    {
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

        return (offsets, labels);
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

    [GeneratedRegex(@"^[ \t]*(?:jp|jmp)[ \t]+([A-Za-z_.$][\w.$]*)[ \t]*\r?\n((?:[ \t]*(?:[A-Za-z_.$][\w.$]*:|;[^\r\n]*)[ \t]*\r?\n)*[ \t]*\1:)", RegexOptions.Multiline)]
    private static partial Regex JumpToNext();
}
