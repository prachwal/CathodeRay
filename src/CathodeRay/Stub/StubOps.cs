using System.Runtime.CompilerServices;

namespace CathodeRay.Stub;

/// <summary>Semantyka opcode'ów zaślepki jako czyste funkcje na stanie. Bez szyny i trybów adresowania:
/// CPU podaje gotową wartość (natychmiastową lub odczytaną spod adresu efektywnego) albo adres. Każdy zapis do A lub X ustawia Z.
/// Klasa dzielona na pliki per grupa (<c>group</c> w JSON): ten plik to tylko wspólne pomocniki.</summary>
public static partial class StubOps
{
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
