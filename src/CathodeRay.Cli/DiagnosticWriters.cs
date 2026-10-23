namespace CathodeRay.Cli;

/// <summary>Otwiera pliki zrzutów (albo stderr dla <c>-</c>) i zamyka je razem z komendą.</summary>
internal sealed class DiagnosticWriters(TextWriter error) : IDisposable
{
    private readonly Dictionary<string, TextWriter> _open = new(StringComparer.Ordinal);

    /// <summary>Zwraca pisarza dla ścieżki; ta sama ścieżka daje tego samego pisarza (wspólny plik).</summary>
    /// <param name="path">Ścieżka, <c>-</c> = stderr, null = wyłączone.</param>
    /// <returns>Pisarz albo null.</returns>
    public TextWriter? Open(string? path)
    {
        if (path is null)
        {
            return null;
        }

        if (path == "-")
        {
            return error;
        }

        string full = Path.GetFullPath(path);
        if (!_open.TryGetValue(full, out TextWriter? writer))
        {
            writer = File.CreateText(full);
            _open[full] = writer;
        }

        return writer;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (TextWriter writer in _open.Values)
        {
            writer.Dispose();
        }
    }
}
