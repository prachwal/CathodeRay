namespace CathodeRay.C;

/// <summary>Obszar pamięci celu dla linkera.</summary>
/// <param name="Name">Nazwa obszaru.</param>
/// <param name="Start">Adres początkowy.</param>
/// <param name="Size">Rozmiar w bajtach.</param>
public sealed record TargetArea(string Name, int Start, int Size);
