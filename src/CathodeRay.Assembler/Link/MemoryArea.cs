namespace CathodeRay.Assembler.Link;

/// <summary>Obszar pamięci z konfiguracji linkera.</summary>
/// <param name="Name">Nazwa obszaru.</param>
/// <param name="Start">Adres początkowy.</param>
/// <param name="Size">Rozmiar w bajtach.</param>
public sealed record MemoryArea(string Name, int Start, int Size);
