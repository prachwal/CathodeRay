namespace CathodeRay.C;

/// <summary>Domyślny układ pamięci celu (frontend C nie zależy od asemblera, więc to dane, a nie <c>LinkerConfig</c>).</summary>
/// <param name="Areas">Obszary pamięci.</param>
/// <param name="Segments">Segmenty w kolejności układania.</param>
public sealed record TargetLayout(IReadOnlyList<TargetArea> Areas, IReadOnlyList<TargetSegment> Segments);
