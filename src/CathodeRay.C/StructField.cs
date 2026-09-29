namespace CathodeRay.C;

/// <summary>Pole struktury: nazwa, typ i przesunięcie od początku (pola bez wyrównania).</summary>
/// <param name="Name">Nazwa pola.</param>
/// <param name="Type">Typ pola.</param>
/// <param name="Offset">Przesunięcie w bajtach (dla pola bitowego: początek jednostki pamięci).</param>
/// <param name="BitWidth">Szerokość pola bitowego w bitach (0 = zwykłe pole).</param>
/// <param name="BitShift">Numer najmłodszego bitu pola bitowego w jednostce.</param>
public sealed record StructField(string Name, CType Type, int Offset, int BitWidth = 0, int BitShift = 0);
