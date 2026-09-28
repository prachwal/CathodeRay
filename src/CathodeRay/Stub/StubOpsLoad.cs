using System.Runtime.CompilerServices;

namespace CathodeRay.Stub;

/// <summary>Grupa <c>load</c>: LDI / LDA / LDX / STA (STA wykonuje CPU), transfery TAX / TXA.</summary>
public static partial class StubOps
{
    /// <summary>LDI / LDA: A = wartość; ustawia Z.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="value">Wartość.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Lda(StubState state, byte value) => SetA(state, value);

    /// <summary>LDX: X = wartość; ustawia Z.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="value">Wartość.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Ldx(StubState state, byte value)
    {
        state.X = value;
        state.Zero = value == 0;
    }

    /// <summary>TAX: X = A; ustawia Z.</summary>
    /// <param name="state">Stan CPU.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Tax(StubState state) => Ldx(state, state.A);

    /// <summary>TXA: A = X; ustawia Z.</summary>
    /// <param name="state">Stan CPU.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Txa(StubState state) => Lda(state, state.X);
}
