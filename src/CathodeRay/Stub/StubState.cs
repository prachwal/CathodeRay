using CathodeRay.Abstractions;

namespace CathodeRay.Stub;

/// <summary>Minimalny stan zaślepki: akumulator, licznik programu i flaga zatrzymania.</summary>
public sealed class StubState : ICpuState<StubState>
{
    /// <summary>Akumulator 8-bit.</summary>
    public byte A { get; set; }

    /// <inheritdoc/>
    public ushort ProgramCounter { get; set; }

    /// <summary>Czy CPU jest zatrzymany (po wykonaniu HLT).</summary>
    public bool Halted { get; set; }

    /// <inheritdoc/>
    public StubState Clone() =>
        new() { A = A, ProgramCounter = ProgramCounter, Halted = Halted };
}
