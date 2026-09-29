namespace CathodeRay.C;

/// <summary>Pole struktury: nazwa, typ i przesunięcie od początku (pola bez wyrównania).</summary>
/// <param name="Name">Nazwa pola.</param>
/// <param name="Type">Typ pola.</param>
/// <param name="Offset">Przesunięcie w bajtach.</param>
public sealed record StructField(string Name, CType Type, int Offset);
