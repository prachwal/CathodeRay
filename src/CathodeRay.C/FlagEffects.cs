namespace CathodeRay.C;

/// <summary>Efekty prymitywu ISA: czytane i zapisywane rejestry oraz ustawiane flagi.
/// Wypełnia zadanie 4 planu 38 (fuzz przeciw emulatorom); dziś sam typ i puste mapy w modelach.</summary>
/// <param name="Reads">Rejestry czytane.</param>
/// <param name="Writes">Rejestry zapisywane.</param>
/// <param name="Flags">Flagi ustawiane (<c>N</c>, <c>Z</c>, <c>C</c>, <c>V</c>).</param>
public sealed record FlagEffects(IReadOnlySet<string> Reads, IReadOnlySet<string> Writes, IReadOnlySet<string> Flags)
{
    /// <summary>Brak efektów.</summary>
    public static FlagEffects None { get; } = new(new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
}
