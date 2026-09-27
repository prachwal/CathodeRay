namespace CathodeRay.Abstractions;

/// <summary>Diagnostyczny zapis jednego kroku CPU: stan przed, stan po i koszt w cyklach.</summary>
/// <param name="Before">Snapshot przed krokiem.</param>
/// <param name="After">Snapshot po kroku.</param>
/// <param name="Cycles">Liczba cykli zwrócona przez krok.</param>
public readonly record struct CpuStepTrace(CpuDebugSnapshot Before, CpuDebugSnapshot After, int Cycles);
