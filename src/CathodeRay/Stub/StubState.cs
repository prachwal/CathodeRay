using CathodeRay.Abstractions;

namespace CathodeRay.Stub;

/// <summary>Minimalny stan zaślepki: akumulator, rejestr indeksowy, flagi, licznik programu i flaga zatrzymania.</summary>
public sealed class StubState : ICpuState<StubState>
{
    /// <summary>Akumulator 8-bit.</summary>
    public byte A { get; set; }

    /// <summary>Rejestr indeksowy 8-bit (licznik pętli, przesunięcie w trybie <c>a16,X</c>).</summary>
    public byte X { get; set; }

    /// <summary>Przeniesienie/pożyczka z ostatniej operacji ALU lub CPX.</summary>
    public bool Carry { get; set; }

    /// <summary>Nadmiar ze znaku z ostatniej operacji ALU.</summary>
    public bool Overflow { get; set; }

    /// <summary>Zero: ostatnia wartość zapisana do A lub X (albo wynik CPX) była zerem.</summary>
    public bool Zero { get; set; }

    /// <summary>Wskaźnik stosu (strona <c>01xxh</c>); start <c>FF</c>, rośnie w dół.</summary>
    public byte StackPointer { get; set; } = 0xFF;

    /// <inheritdoc/>
    public ushort ProgramCounter { get; set; }

    /// <summary>Czy CPU jest zatrzymany (po wykonaniu HLT).</summary>
    public bool Halted { get; set; }

    /// <inheritdoc/>
    public StubState Clone() => new()
    {
        A = A,
        X = X,
        Carry = Carry,
        Overflow = Overflow,
        Zero = Zero,
        StackPointer = StackPointer,
        ProgramCounter = ProgramCounter,
        Halted = Halted,
    };
}
