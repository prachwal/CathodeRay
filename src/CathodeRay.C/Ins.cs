namespace CathodeRay.C;

/// <summary>Instrukcja kodu pośredniego. Na etapie migracji (plan 30) jedynym rodzajem jest
/// <see cref="Raw"/>; kolejne kroki zastępują go typowanymi operacjami na komórkach.</summary>
public abstract record Ins : IrItem;
