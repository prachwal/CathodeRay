using System.Runtime.CompilerServices;

namespace CathodeRay.Stub;

/// <summary>Semantyka opcode'ów zaślepki jako czyste funkcje na stanie. Bez szyny i trybów adresowania:
/// CPU podaje gotową wartość (natychmiastową lub odczytaną spod adresu efektywnego) albo adres. Każdy zapis do A lub X ustawia Z.</summary>
public static class StubOps
{
    /// <summary>LDI / LDA: A = wartość; ustawia Z.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="value">Wartość.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Lda(StubState state, byte value) => SetA(state, value);

    /// <summary>ADD: A = A + wartość; ustawia C, V, Z.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="value">Składnik.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(StubState state, byte value) => SetArith(state, Alu.Add(state.A, value));

    /// <summary>ADC: A = A + wartość + C; ustawia C, V, Z.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="value">Składnik.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Adc(StubState state, byte value) => SetArith(state, Alu.Add(state.A, value, state.Carry));

    /// <summary>SUB: A = A - wartość; C = brak pożyczenia, V = nadmiar, Z.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="value">Odjemnik.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sub(StubState state, byte value) => SetArith(state, Alu.Subtract(state.A, value));

    /// <summary>INC: A = A + 1; ustawia Z, C i V bez zmian.</summary>
    /// <param name="state">Stan CPU.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Inc(StubState state) => SetA(state, (byte)(state.A + 1));

    /// <summary>LDX: X = wartość; ustawia Z.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="value">Wartość.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Ldx(StubState state, byte value)
    {
        state.X = value;
        state.Zero = value == 0;
    }

    /// <summary>INX: X = X + 1; ustawia Z.</summary>
    /// <param name="state">Stan CPU.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Inx(StubState state) => Ldx(state, (byte)(state.X + 1));

    /// <summary>CPX: porównuje X z wartością (X - wartość bez zapisu); Z = równe, C = X ≥ wartość, V bez zmian.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="value">Wartość porównywana.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Cpx(StubState state, byte value)
    {
        AluResult result = Alu.Subtract(state.X, value);
        state.Carry = result.Carry;
        state.Zero = result.Zero;
    }

    /// <summary>JMP: PC = adres.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="address">Adres docelowy.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Jmp(StubState state, ushort address) => state.ProgramCounter = address;

    /// <summary>BNE: PC = adres, gdy Z = 0.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="address">Adres docelowy.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Bne(StubState state, ushort address)
    {
        if (!state.Zero)
        {
            state.ProgramCounter = address;
        }
    }

    /// <summary>HLT: zatrzymuje CPU.</summary>
    /// <param name="state">Stan CPU.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Hlt(StubState state) => state.Halted = true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetA(StubState state, byte value)
    {
        state.A = value;
        state.Zero = value == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetArith(StubState state, AluResult result)
    {
        SetA(state, result.Value);
        state.Carry = result.Carry;
        state.Overflow = result.Overflow;
    }
}
