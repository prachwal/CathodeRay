namespace CathodeRay.Assembler.Link;

/// <summary>Wiersz listingu w module obiektu: tekst źródła i bajty względem segmentu.</summary>
/// <param name="Offset">Offset w segmencie (adres w module minus origin 0).</param>
/// <param name="Bytes">Bajty linii (puste dla etykiet/komentarzy).</param>
/// <param name="Text">Tekst linii źródła.</param>
/// <param name="File">Plik źródła (null = wejście).</param>
/// <param name="Line">Numer linii (od 1).</param>
public sealed record ObjectLine(int Offset, byte[] Bytes, string Text, string? File, int Line);
