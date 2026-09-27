using CathodeRay.Abstractions;

namespace CathodeRay.Stub;

/// <summary>Semantyka opcode'ów zaślepki jako czyste funkcje na stanie i szynie (bez dekodowania i dispatchu).</summary>
public static class StubOps
{
    /// <summary>LDI: A = wartość.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="value">Wartość natychmiastowa.</param>
    public static void Ldi(StubState state, byte value) => state.A = value;

    /// <summary>ADD: A = A + wartość; ustawia C i V.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="value">Składnik.</param>
    public static void Add(StubState state, byte value) => SetArith(state, Alu.Add(state.A, value));

    /// <summary>SUB: A = A - wartość; C = brak pożyczenia, V = nadmiar.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="value">Odjemnik.</param>
    public static void Sub(StubState state, byte value) => SetArith(state, Alu.Subtract(state.A, value));

    /// <summary>INC: A = A + 1; flagi bez zmian.</summary>
    /// <param name="state">Stan CPU.</param>
    public static void Inc(StubState state) => state.A++;

    /// <summary>JMP: PC = adres.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="address">Adres docelowy.</param>
    public static void Jmp(StubState state, ushort address) => state.ProgramCounter = address;

    /// <summary>STA: M[adres] = A.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="bus">Szyna.</param>
    /// <param name="address">Adres zapisu.</param>
    /// <returns>Aktywność magistrali.</returns>
    public static BusActivity Sta(StubState state, IBus bus, ushort address)
    {
        bus.Write(address, state.A);
        return BusActivity.Write;
    }

    /// <summary>LDA: A = M[adres].</summary>
    /// <param name="state">Stan CPU.</param>
    /// <param name="bus">Szyna.</param>
    /// <param name="address">Adres odczytu.</param>
    /// <returns>Aktywność magistrali.</returns>
    public static BusActivity Lda(StubState state, IBus bus, ushort address)
    {
        state.A = bus.Read(address);
        return BusActivity.Read;
    }

    /// <summary>HLT: zatrzymuje CPU.</summary>
    /// <param name="state">Stan CPU.</param>
    /// <returns>Aktywność magistrali.</returns>
    public static BusActivity Hlt(StubState state)
    {
        state.Halted = true;
        return BusActivity.Halt;
    }

    private static void SetArith(StubState state, AluResult result)
    {
        state.A = result.Value;
        state.Carry = result.Carry;
        state.Overflow = result.Overflow;
    }
}
