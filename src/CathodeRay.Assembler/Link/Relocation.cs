namespace CathodeRay.Assembler.Link;

/// <summary>Relokacja: pole w segmencie do uzupełnienia adresem symbolu.</summary>
/// <param name="Segment">Segment pola.</param>
/// <param name="Offset">Offset pola w segmencie (od początku segmentu).</param>
/// <param name="Kind">Rodzaj pola.</param>
/// <param name="Symbol">Symbol (globalny z innego modułu lub własny).</param>
/// <param name="Addend">Stała część wyrażenia (wartość przy symbolu = 0).</param>
public sealed record Relocation(string Segment, int Offset, RelocKind Kind, string Symbol, int Addend);
