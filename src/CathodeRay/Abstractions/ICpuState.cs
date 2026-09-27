namespace CathodeRay.Abstractions;

/// <summary>Kontrakt stanu CPU: licznik programu i typowana kopia (snapshot bez pamięci).</summary>
/// <typeparam name="TSelf">Własny typ stanu, żeby <see cref="Clone"/> nie wymagał rzutowań.</typeparam>
public interface ICpuState<TSelf>
{
    /// <summary>Licznik programu (szerokość i banki zależą od CPU).</summary>
    ushort ProgramCounter { get; set; }

    /// <summary>Kopia stanu bez pamięci (snapshot bez kosztu kopiowania pamięci).</summary>
    /// <returns>Niezależny klon stanu.</returns>
    TSelf Clone();
}
