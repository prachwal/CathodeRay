using System.Runtime.CompilerServices;

namespace CathodeRay.Stub;

/// <summary>Grupa <c>jump</c>: JMP / BNE / BEQ.</summary>
public static partial class StubOps
{
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

    /// <summary>BEQ: PC = adres, gdy Z = 1.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="address">Adres docelowy.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Beq(StubState state, ushort address)
    {
        if (state.Zero)
        {
            state.ProgramCounter = address;
        }
    }
}
