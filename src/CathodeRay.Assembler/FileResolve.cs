namespace CathodeRay.Assembler;

/// <summary>Wspólne rozwiązywanie plików dla <c>.include</c> i <c>.incbin</c>:
/// cudzysłowy, normalizacja i kolejność poszukiwań (katalog includera, potem <c>--incdir</c>).</summary>
internal static class FileResolve
{
    /// <summary>Kandydatury ścieżek dla nazwy: najpierw względem pliku includującego, potem katalogi poszukiwań.</summary>
    /// <param name="name">Nazwa z operandu (bez cudzysłowów).</param>
    /// <param name="includer">Znormalizowana ścieżka pliku includującego.</param>
    /// <param name="includePaths">Dodatkowe katalogi poszukiwań.</param>
    /// <returns>Znormalizowane ścieżki w kolejności prób.</returns>
    public static IEnumerable<string> Candidates(string name, string includer, IReadOnlyList<string> includePaths)
    {
        string? home = Path.GetDirectoryName(includer);
        IEnumerable<string> bases = home is null ? includePaths : new[] { home }.Concat(includePaths);
        return bases.Select(b => Normalize(Path.Combine(b, name)));
    }

    /// <summary>Zdejmuje cudzysłowy z operandu z nazwą pliku.</summary>
    /// <param name="operand">Tekst operandu.</param>
    /// <param name="directive">Nazwa dyrektywy do komunikatów (np. <c>.include</c>).</param>
    /// <param name="error">Buduje błąd (numer linii i plik znane wołającemu).</param>
    /// <returns>Nazwa pliku.</returns>
    public static string Unquote(string? operand, string directive, Func<string, AssemblerException> error)
    {
        if (operand is { Length: >= 2 }
            && ((operand.StartsWith('"') && operand.EndsWith('"'))
                || (operand.StartsWith('\'') && operand.EndsWith('\'')))
            && operand[1..^1] is { Length: > 0 } inner)
        {
            return inner;
        }

        throw error(operand is null
            ? $"{directive} needs a file name."
            : $"expected quoted file name after {directive}, got '{operand}'.");
    }

    /// <summary>Normalizuje ścieżkę do pełnej; nieprawidłowa przechodzi bez zmian.</summary>
    /// <param name="path">Ścieżka.</param>
    /// <returns>Ścieżka znormalizowana.</returns>
    public static string Normalize(string path)
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
