namespace CathodeRay.C;

/// <summary>Rejestr CPU w jawnym kontrakcie maszyny: nazwa, szerokość i aliasy (te same bity pod inną nazwą).
/// Optymalizator nie zgaduje już, czy dwa adresy to ten sam bajt — wynika to z modelu.</summary>
/// <param name="Name">Nazwa jak w selektorze (<c>a</c>, <c>hl</c>, <c>bc</c>).</param>
/// <param name="Width">Szerokość w bajtach (1 albo 2, jak <see cref="Ir.Cell.W"/>).</param>
/// <param name="Parts">Rejestry składowe, starszy pierwszy (puste dla 8-bitowych; <c>hl</c> to <c>[h, l]</c>).</param>
public sealed record CpuRegister(string Name, int Width, IReadOnlyList<string> Parts);
