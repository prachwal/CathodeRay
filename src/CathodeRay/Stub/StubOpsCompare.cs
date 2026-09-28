using System.Runtime.CompilerServices;

namespace CathodeRay.Stub;

/// <summary>Grupa <c>compare</c>: CPX.</summary>
public static partial class StubOps
{
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
}
