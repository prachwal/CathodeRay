namespace CathodeRay.Cli;

/// <summary>Opisy instrukcji (ISA JSON) wbudowane w program, żeby pojedynczy plik <c>cathode</c> działał bez plików obok.
/// Plik o tej samej nazwie obok programu albo jawna ścieżka (<c>--isa</c>) mają pierwszeństwo.</summary>
internal static class BundledIsa
{
    /// <summary>Otwiera opis instrukcji.</summary>
    /// <param name="explicitPath">Ścieżka z <c>--isa</c> albo null.</param>
    /// <param name="fileName">Nazwa pliku ISA celu, np. <c>mcp_z80_instructions.json</c>.</param>
    /// <returns>Strumień JSON.</returns>
    /// <exception cref="FileNotFoundException">Brak pliku obok programu i w zasobach.</exception>
    public static Stream Open(string? explicitPath, string fileName)
    {
        if (explicitPath is not null)
        {
            return File.OpenRead(explicitPath);
        }

        string beside = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(beside))
        {
            return File.OpenRead(beside);
        }

        return typeof(BundledIsa).Assembly.GetManifestResourceStream("isa/" + fileName)
            ?? throw new FileNotFoundException($"ISA file '{fileName}' not found next to the program or in the bundled resources.", fileName);
    }
}
