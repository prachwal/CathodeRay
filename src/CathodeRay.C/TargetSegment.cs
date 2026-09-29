namespace CathodeRay.C;

/// <summary>Przypisanie segmentu do obszaru pamięci (kolejność układania segmentów).</summary>
/// <param name="Name">Nazwa segmentu (<c>CODE</c>, <c>INIT</c>, <c>BSS</c>, <c>DATA</c>).</param>
/// <param name="Area">Nazwa obszaru.</param>
public sealed record TargetSegment(string Name, string Area);
