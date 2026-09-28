namespace CathodeRay.Assembler;

/// <summary>Linia listingu.</summary>
/// <param name="Line">Numer linii źródła.</param>
/// <param name="Address">Adres na początku linii.</param>
/// <param name="Bytes">Bajty wygenerowane przez linię.</param>
/// <param name="Source">Tekst źródła.</param>
public sealed record ListingLine(int Line, int Address, byte[] Bytes, string Source);
