using System.Runtime.CompilerServices;

namespace CathodeRay.Stub;

/// <summary>Grupa <c>system</c>: HLT, CLC, SEC.</summary>
public static partial class StubOps
{
    /// <summary>HLT: zatrzymuje CPU.</summary>
    /// <param name="state">Stan CPU.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Hlt(StubState state) => state.Halted = true;

    /// <summary>CLC: C = false.</summary>
    /// <param name="state">Stan CPU.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clc(StubState state) => state.Carry = false;

    /// <summary>SEC: C = true.</summary>
    /// <param name="state">Stan CPU.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sec(StubState state) => state.Carry = true;
}
