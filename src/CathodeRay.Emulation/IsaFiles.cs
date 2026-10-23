namespace CathodeRay.Emulation;

/// <summary>Lokalizacja plików ISA (JSON) emulatorów: obok programu (publikacja) albo w <c>data/instructions</c> repozytorium.</summary>
internal static class IsaFiles
{
    /// <summary>Znajduje plik ISA.</summary>
    /// <param name="name">Nazwa pliku, np. <c>mcp_6502_instructions.json</c>.</param>
    /// <returns>Pełna ścieżka.</returns>
    /// <exception cref="FileNotFoundException">Pliku nie ma obok programu ani w drzewie repozytorium.</exception>
    public static string Find(string name)
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            foreach (string candidate in new[] { Path.Combine(dir.FullName, name), Path.Combine(dir.FullName, "data", "instructions", name) })
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"ISA file '{name}' not found next to the program or under data/instructions.", name);
    }
}
