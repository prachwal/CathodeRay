namespace CathodeRay.Assembler.Link;

/// <summary>Przypisanie segmentu do obszaru.</summary>
/// <param name="Name">Nazwa segmentu.</param>
/// <param name="Load">Nazwa obszaru.</param>
public sealed record SegmentMapping(string Name, string Load);
