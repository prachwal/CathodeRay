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
        string entry = FileResolve.Normalize(entryFile);
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

            string name = FileResolve.Unquote(line.Operand, ".include", message => new AssemblerException(line.Number, message, file));
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
        foreach (string candidate in FileResolve.Candidates(name, includer, includePaths))
        {
            if (reader(candidate) is { } content)
            {
                return (candidate, content);
            }
        }

        return null;
    }
}
