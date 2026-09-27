namespace CathodeRay.Abstractions;

/// <summary>Pojedynczy dostęp do magistrali (odczyt/zapis) — wejście dla watchpointów.</summary>
/// <param name="Address">Adres.</param>
/// <param name="Value">Wartość (odczytana lub zapisana).</param>
/// <param name="IsWrite"><see langword="true"/> = zapis, <see langword="false"/> = odczyt.</param>
public readonly record struct BusAccess(ushort Address, byte Value, bool IsWrite);
