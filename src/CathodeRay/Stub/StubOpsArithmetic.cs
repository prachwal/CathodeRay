using System.Runtime.CompilerServices;

namespace CathodeRay.Stub;

/// <summary>Grupa <c>arithmetic</c>: ADD / ADC / SUB / INC / INX / DEC / DEX.</summary>
public static partial class StubOps
{
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

    /// <summary>INX: X = X + 1; ustawia Z.</summary>
    /// <param name="state">Stan CPU.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Inx(StubState state) => Ldx(state, (byte)(state.X + 1));

    /// <summary>DEC: A = A - 1; ustawia Z, C i V bez zmian.</summary>
    /// <param name="state">Stan CPU.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Dec(StubState state) => SetA(state, (byte)(state.A - 1));

    /// <summary>DEX: X = X - 1; ustawia Z.</summary>
    /// <param name="state">Stan CPU.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Dex(StubState state) => Ldx(state, (byte)(state.X - 1));

    /// <summary>SHL: A = A &lt;&lt; 1; C = stary bit 7, Z; V bez zmian.</summary>
    /// <param name="state">Stan CPU.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Shl(StubState state)
    {
        state.Carry = (state.A & 0x80) != 0;
        SetA(state, (byte)(state.A << 1));
    }

    /// <summary>SHR: A = A &gt;&gt; 1; C = stary bit 0, Z; V bez zmian.</summary>
    /// <param name="state">Stan CPU.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Shr(StubState state)
    {
        state.Carry = (state.A & 0x01) != 0;
        SetA(state, (byte)(state.A >> 1));
    }

    /// <summary>NOT: A = ~A; ustawia Z, C i V bez zmian.</summary>
    /// <param name="state">Stan CPU.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Not(StubState state) => SetA(state, (byte)~state.A);
}
