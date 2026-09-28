using System.Runtime.CompilerServices;

namespace CathodeRay.Stub;

/// <summary>Grupa <c>logic</c>: AND / ORA / EOR. Wynik do A, ustawia tylko Z (C i V bez zmian).</summary>
public static partial class StubOps
{
    /// <summary>AND: A = A &amp; wartość; ustawia Z.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="value">Maska.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void And(StubState state, byte value) => SetA(state, (byte)(state.A & value));

    /// <summary>ORA: A = A | wartość; ustawia Z.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="value">Maska.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Ora(StubState state, byte value) => SetA(state, (byte)(state.A | value));

    /// <summary>EOR: A = A ^ wartość; ustawia Z.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="value">Maska.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Eor(StubState state, byte value) => SetA(state, (byte)(state.A ^ value));
}
