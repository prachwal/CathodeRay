using System.Runtime.CompilerServices;

namespace CathodeRay.Stub;

/// <summary>Grupa <c>stack</c>: PUSH / POP / CALL / RET. Stos na stronie <c>01xxh</c>, SP rośnie w dół.
/// Odczyt/zapis pamięci wstrzykuje CPU, żeby operacje zostały czystymi funkcjami stanu.</summary>
public static partial class StubOps
{
    /// <summary>PUSH: M[0100h + SP] = wartość; SP -= 1.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Push(StubState state, Action<ushort, byte> write, byte value)
    {
        write((ushort)(0x100 + state.StackPointer), value);
        state.StackPointer--;
    }

    /// <summary>POP: SP += 1; A = M[0100h + SP]; ustawia Z.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Pop(StubState state, Func<ushort, byte> read)
    {
        state.StackPointer++;
        Lda(state, read((ushort)(0x100 + state.StackPointer)));
    }

    /// <summary>CALL: odkłada PC (starszy, młodszy), PC = adres.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Call(StubState state, Action<ushort, byte> write, ushort returnAddress, ushort target)
    {
        Push(state, write, (byte)(returnAddress >> 8));
        Push(state, write, (byte)returnAddress);
        state.ProgramCounter = target;
    }

    /// <summary>RET: PC = odłożony adres (młodszy, starszy).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Ret(StubState state, Func<ushort, byte> read)
    {
        state.StackPointer++;
        byte lo = read((ushort)(0x100 + state.StackPointer));
        state.StackPointer++;
        byte hi = read((ushort)(0x100 + state.StackPointer));
        state.ProgramCounter = (ushort)(lo | (hi << 8));
    }
}
