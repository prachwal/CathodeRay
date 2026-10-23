namespace CathodeRay.Emulation;

/// <summary>Opisy instrukcji (ISA JSON) interpreterów: plik obok programu albo w <c>data/instructions</c> repozytorium ma
/// pierwszeństwo (można go podmienić), w przeciwnym razie używany jest egzemplarz wbudowany w bibliotekę.</summary>
internal static class IsaFiles
{
    /// <summary>Czyta opis instrukcji.</summary>
    /// <param name="name">Nazwa pliku, np. <c>mcp_6502_instructions.json</c>.</param>
    /// <returns>Zawartość JSON.</returns>
    /// <exception cref="FileNotFoundException">Brak pliku na dysku i w zasobach.</exception>
    public static string ReadText(string name)
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            foreach (string candidate in new[] { Path.Combine(dir.FullName, name), Path.Combine(dir.FullName, "data", "instructions", name) })
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            dir = dir.Parent;
        }

        using Stream stream = typeof(IsaFiles).Assembly.GetManifestResourceStream("isa/" + name)
            ?? throw new FileNotFoundException($"ISA file '{name}' not found on disk or in the bundled resources.", name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
