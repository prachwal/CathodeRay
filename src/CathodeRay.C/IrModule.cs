namespace CathodeRay.C;

/// <summary>Moduł po wygenerowaniu, przed wyborem celu: kod (funkcje i luźne fragmenty w kolejności emisji),
/// tablica inicjalizatorów, dane i BSS. Cel (<see cref="ICTarget"/>) drukuje go jako asembler.</summary>
/// <param name="Code">Segment kodu.</param>
/// <param name="Init">Segment INIT (tablica inicjalizatorów globali).</param>
/// <param name="Data">Segment danych z wartościami początkowymi.</param>
/// <param name="Bss">Segment danych zerowanych.</param>
public sealed record IrModule(
    IReadOnlyList<IrItem> Code,
    IReadOnlyList<Ins> Init,
    IReadOnlyList<Ins> Data,
    IReadOnlyList<Ins> Bss);
