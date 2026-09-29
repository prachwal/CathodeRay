namespace CathodeRay.C;

/// <summary>crt0 runtime mini-C (<c>crt0.s</c>): SP, zerowanie BSS, CALL main, HLT.
/// Linkowany zawsze pierwszy (przed wyjściem codegenu).</summary>
public static class Crt0
{
    /// <summary>Tekst <c>crt0.s</c>.</summary>
    public static string Source { get; } = Load();

    private static string Load()
    {
        using Stream? stream = typeof(Crt0).Assembly.GetManifestResourceStream("CathodeRay.C.crt0.s");
        if (stream is null)
        {
            throw new InvalidOperationException("missing embedded resource 'CathodeRay.C.crt0.s'.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
