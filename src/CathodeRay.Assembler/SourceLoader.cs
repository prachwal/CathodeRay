using CathodeRay.Assembler.Directives;
using CathodeRay.Assembler.Syntax;

namespace CathodeRay.Assembler;

/// <summary>Przedprzebiegowa ekspansja <c>.include</c>/<c>INCLUDE</c>: wstawia treść plików w miejsce dyrektywy,
/// z atrybucją pliku w każdej linii. Cykle, zbyt głębokie zagnieżdżenie i brak pliku to <see cref="AssemblerException"/>.</summary>
internal static class SourceLoader
{
    private const int MaxDepth = 32;

    /// <summary>Rozwija include'y w tekście pliku wejściowego.</summary>
    /// <param name="source">Tekst pliku wejściowego.</param>
    /// <param name="entryFile">Ścieżka pliku wejściowego (baza ścieżek względnych i atrybucja).</param>
    /// <param name="reader">Czyta plik o ścieżce znormalizowanej; <see langword="null"/> = brak pliku.</param>
    /// <param name="dialect">Dialekt (rozpoznanie linii include).</param>
    /// <param name="isKeyword">Czy słowo jest mnemonikiem/dyrektywą.</param>
    /// <param name="includePaths">Dodatkowe katalogi poszukiwań.</param>
    /// <returns>Linie ze wszystkich plików, w kolejności asemblacji.</returns>
    public static IReadOnlyList<SourceLine> Expand(
        string source,
        string entryFile,
        Func<string, string?> reader,
        SyntaxDialect dialect,
        Func<string, bool> isKeyword,
        IReadOnlyList<string>? includePaths = null)
    {
        string entry = Normalize(entryFile);
        var lines = new List<SourceLine>();
        ExpandText(source, entry, [entry], lines, reader, dialect, isKeyword, includePaths ?? []);
        return lines;
    }

    private static void ExpandText(
        string source,
        string file,
        List<string> stack,
        List<SourceLine> lines,
        Func<string, string?> reader,
        SyntaxDialect dialect,
        Func<string, bool> isKeyword,
        IReadOnlyList<string> includePaths)
    {
        if (stack.Count > MaxDepth)
        {
            throw new AssemblerException(1, $"include nesting too deep (>{MaxDepth}): {string.Join(" -> ", stack)}.", file);
        }

        string[] raw = source.Split('\n');
        for (int i = 0; i < raw.Length; i++)
        {
            SourceLine line = LineParser.Parse(i + 1, raw[i].TrimEnd('\r'), dialect, isKeyword, file);
            if (line.Keyword is null
                || !dialect.Directives.TryGetValue(line.Keyword, out IDirective? directive)
                || directive is not IncludeDirective)
            {
                lines.Add(line);
                continue;
            }

            if (line.Label is not null)
            {
                throw new AssemblerException(line.Number, "label on .include is not allowed.", file);
            }

            string name = Unquote(line.Operand, line.Number, file);
            if (Find(name, file, includePaths, reader) is not (string found, string content))
            {
                throw new AssemblerException(line.Number, $"include file '{name}' not found.", file);
            }

            if (stack.Contains(found, StringComparer.Ordinal))
            {
                throw new AssemblerException(line.Number, $"cyclic include: {string.Join(" -> ", stack)} -> {found}.", file);
            }

            stack.Add(found);
            ExpandText(content, found, stack, lines, reader, dialect, isKeyword, includePaths);
            stack.RemoveAt(stack.Count - 1);
        }
    }

    private static (string Found, string Content)? Find(
        string name,
        string includer,
        IReadOnlyList<string> includePaths,
        Func<string, string?> reader)
    {
        string? home = Path.GetDirectoryName(includer);
        IEnumerable<string> candidates = home is null
            ? includePaths.Select(p => Normalize(Path.Combine(p, name)))
            : new[] { Normalize(Path.Combine(home, name)) }.Concat(includePaths.Select(p => Normalize(Path.Combine(p, name))));
        foreach (string candidate in candidates)
        {
            if (reader(candidate) is { } content)
            {
                return (candidate, content);
            }
        }

        return null;
    }

    private static string Unquote(string? operand, int number, string file)
    {
        if (operand is { Length: >= 2 }
            && ((operand.StartsWith('"') && operand.EndsWith('"'))
                || (operand.StartsWith('\'') && operand.EndsWith('\'')))
            && operand[1..^1] is { Length: > 0 } inner)
        {
            return inner;
        }

        throw new AssemblerException(number, operand is null ? ".include needs a file name." : $"expected quoted file name after .include, got '{operand}'.", file);
    }

    private static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }
}
