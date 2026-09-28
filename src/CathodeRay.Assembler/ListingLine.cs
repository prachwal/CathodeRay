namespace CathodeRay.Assembler;

/// <summary>Linia listingu.</summary>
/// <param name="Line">Numer linii w pliku źródła.</param>
/// <param name="Address">Adres na początku linii.</param>
/// <param name="Bytes">Bajty wygenerowane przez linię.</param>
/// <param name="Source">Tekst źródła.</param>
/// <param name="File">Plik źródła (<see langword="null"/> = tekst bez kontekstu pliku).</param>
/// <param name="Segment">Segment linii (<see langword="null"/> = sprzed segmentów).</param>
public sealed record ListingLine(int Line, int Address, byte[] Bytes, string Source, string? File = null, string? Segment = null);
