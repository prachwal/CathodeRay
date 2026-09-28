using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CathodeRay.Abstractions;

namespace CathodeRay.Stub;

/// <summary>Minimalna zaślepka CPU napędzana tabelą opcode z JSON — do testowania abstrakcji <see cref="ICpu{TState}"/> i <see cref="IBus"/>.
/// Rejestrację opcode można nadpisać (<see cref="ConfigureOpcodes"/>) i dodać/poprawić/usunąć wpisy (delta CPU).
/// Implementuje introspekcję (<see cref="ICpuStatus"/>), więc nadaje się do diagnostyki (<see cref="CpuDiagnostics"/>).</summary>
public class StubCpu : ICpu<StubState>
{
    private static readonly RegisterLayout Registers = new(
        new RegisterDefinition("A", 8, RegisterRole.Accumulator),
        new RegisterDefinition("X", 8, RegisterRole.Index),
        new RegisterDefinition("C", 1, RegisterRole.Status),
        new RegisterDefinition("V", 1, RegisterRole.Status),
        new RegisterDefinition("Z", 1, RegisterRole.Status),
        new RegisterDefinition("PC", 16, RegisterRole.ProgramCounter));

    private readonly IBus _bus;
    private readonly StubIsa _isa;
    private StubOpcodeTable? _opcodes;
    private BusActivity _activity;

    /// <summary>Tworzy zaślepkę: rejestruje opcode'y z JSON (nieznany mnemonic = wyjątek już tutaj).</summary>
    /// <param name="isa">Tabela opcode wczytana z JSON.</param>
    /// <param name="bus">Szyna pamięci.</param>
    public StubCpu(StubIsa isa, IBus bus)
    {
        ArgumentNullException.ThrowIfNull(isa);
        ArgumentNullException.ThrowIfNull(bus);
        _isa = isa;
        _bus = bus;
        State = new StubState();
        LastOpcode = -1;
    }

    /// <inheritdoc/>
    public StubState State { get; }

    /// <inheritdoc/>
    public ulong CycleCount { get; private set; }

    /// <inheritdoc/>
    public ulong InstructionCount { get; private set; }

    /// <inheritdoc/>
    public BusActivity LastBusActivity { get; private set; }

    /// <inheritdoc/>
    public int LastOpcode { get; private set; }

    /// <inheritdoc/>
    public string? LastMnemonic => LastOpcode >= 0 && _opcodes is not null && _opcodes.TryGet((byte)LastOpcode, out StubOpcodeEntry? entry)
        ? entry.Mnemonic
        : null;

    /// <inheritdoc/>
    ushort ICpuStatus.ProgramCounter => State.ProgramCounter;

    /// <inheritdoc/>
    bool ICpuStatus.Halted => State.Halted;

    /// <summary>Szyna pamięci dla handlerów podklas.</summary>
    protected IBus Bus => _bus;

    /// <inheritdoc/>
    public int Step()
    {
        if (State.Halted)
        {
            return 0;
        }

        ushort pc = State.ProgramCounter;
        byte opcode = _bus.Read(pc);
        StubOpcodeTable opcodes = _opcodes ??= Build();
        if (!opcodes.TryGet(opcode, out StubOpcodeEntry? entry))
        {
            throw new InvalidOperationException($"Unknown opcode 0x{opcode:X2} at 0x{pc:X4}.");
        }

        int operand = entry.Words switch
        {
            2 => _bus.Read((ushort)(pc + 1)),
            3 => _bus.Read((ushort)(pc + 1)) | (_bus.Read((ushort)(pc + 2)) << 8),
            _ => 0,
        };
        _activity = BusActivity.Fetch;
        if (entry.Words > 1)
        {
            _activity |= BusActivity.Read;
        }

        State.ProgramCounter = (ushort)(pc + entry.Words);
        if (entry.Operation == StubOperation.Custom)
        {
            entry.Handler!(new OpcodeContext(opcode, operand, pc));
        }
        else
        {
            Execute(entry.Operation, entry.Mode, operand);
        }

        if (State.Halted)
        {
            _activity |= BusActivity.Halt;
        }

        LastBusActivity = _activity;
        LastOpcode = opcode;
        CycleCount += (ulong)entry.Cycles;
        InstructionCount++;
        return entry.Cycles;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        State.A = 0;
        State.X = 0;
        State.Carry = false;
        State.Overflow = false;
        State.Zero = false;
        State.ProgramCounter = 0;
        State.Halted = false;
        CycleCount = 0;
        InstructionCount = 0;
        LastBusActivity = BusActivity.None;
        LastOpcode = -1;
        _activity = BusActivity.None;
    }

    /// <inheritdoc/>
    public RegisterView CaptureRegisters() => new(
        Registers,
        [State.A, State.X, Bit(State.Carry), Bit(State.Overflow), Bit(State.Zero), State.ProgramCounter]);

    /// <summary>Rejestruje wpis (metadane + handler) w tabeli.</summary>
    /// <param name="table">Rejestr docelowy.</param>
    /// <param name="opcode">Klucz opcode.</param>
    /// <param name="mnemonic">Mnemonik.</param>
    /// <param name="cycles">Liczba cykli.</param>
    /// <param name="words">Liczba słów.</param>
    /// <param name="handler">Handler zachowania.</param>
    protected static void RegisterOpcode(
        StubOpcodeTable table,
        byte opcode,
        string mnemonic,
        int cycles,
        int words,
        Action<OpcodeContext> handler)
    {
        table.Add(opcode, new StubOpcodeEntry(handler, mnemonic, cycles, words));
    }

    /// <summary>Rejestruje opcode'y z JSON; podklasa nadpisuje i woła <c>base</c>, po czym zmienia tabelę (Add/Replace/Remove).
    /// Mnemonik z <see cref="StubOperation"/> idzie szybką ścieżką (switch), pozostałe przez <see cref="ResolveBehavior"/>.</summary>
    /// <param name="isa">Tabela opcode z JSON.</param>
    /// <param name="table">Rejestr docelowy (jeszcze niezamknięty).</param>
    protected virtual void ConfigureOpcodes(StubIsa isa, StubOpcodeTable table)
    {
        foreach ((byte opcode, StubOpcode definition) in isa.Opcodes)
        {
            StubOpcodeEntry entry = TryGetBuiltIn(definition.Mnemonic, out StubOperation operation)
                ? new StubOpcodeEntry(null, definition.Mnemonic, definition.Cycles, definition.Words, operation, definition.Mode)
                : new StubOpcodeEntry(ResolveBehavior(definition.Mnemonic, definition.Mode), definition.Mnemonic, definition.Cycles, definition.Words);
            if (entry.Operation == StubOperation.Custom ? entry.Handler is null : !Supports(operation, definition.Mode))
            {
                throw new InvalidOperationException(
                    $"No handler for '{definition.Mnemonic}' in mode {definition.Mode} (opcode 0x{opcode:X2}).");
            }

            table.Add(opcode, entry);
        }
    }

    /// <summary>Handler instrukcji spoza <see cref="StubOperation"/>; podklasa nadpisuje, żeby dodać własne mnemoniki (wolniejsza ścieżka: delegat).</summary>
    /// <param name="mnemonic">Mnemonik z JSON.</param>
    /// <param name="mode">Tryb adresowania z JSON.</param>
    /// <returns>Handler lub <see langword="null"/> (nieznany mnemonik).</returns>
    protected virtual Action<OpcodeContext>? ResolveBehavior(string mnemonic, OperandMode mode) => null;

    private static bool TryGetBuiltIn(string mnemonic, out StubOperation operation) =>
        Enum.TryParse(mnemonic, ignoreCase: true, out operation)
        && operation != StubOperation.Custom
        && char.IsAsciiLetter(mnemonic[0]);

    private static bool Supports(StubOperation operation, OperandMode mode) => operation switch
    {
        StubOperation.Nop or StubOperation.Inc or StubOperation.Inx or StubOperation.Hlt => mode == OperandMode.None,
        StubOperation.Sta or StubOperation.Jmp or StubOperation.Bne => IsAddress(mode),
        _ => mode == OperandMode.Immediate8 || IsAddress(mode),
    };

    private static bool IsAddress(OperandMode mode) => mode is OperandMode.Address16 or OperandMode.Address16X;

    private static ulong Bit(bool flag) => flag ? 1UL : 0UL;

    [DoesNotReturn]
    private static void ThrowUnhandled(StubOperation operation) =>
        throw new UnreachableException($"Unhandled operation {operation}.");

    private void Execute(StubOperation operation, OperandMode mode, int operand)
    {
        StubState state = State;
        ushort address = (ushort)(mode == OperandMode.Address16X ? operand + state.X : operand);
        switch (operation)
        {
            case StubOperation.Nop:
                break;
            case StubOperation.Ldi:
            case StubOperation.Lda:
                StubOps.Lda(state, Value(mode, operand, address));
                break;
            case StubOperation.Ldx:
                StubOps.Ldx(state, Value(mode, operand, address));
                break;
            case StubOperation.Add:
                StubOps.Add(state, Value(mode, operand, address));
                break;
            case StubOperation.Adc:
                StubOps.Adc(state, Value(mode, operand, address));
                break;
            case StubOperation.Sub:
                StubOps.Sub(state, Value(mode, operand, address));
                break;
            case StubOperation.Cpx:
                StubOps.Cpx(state, Value(mode, operand, address));
                break;
            case StubOperation.Inc:
                StubOps.Inc(state);
                break;
            case StubOperation.Inx:
                StubOps.Inx(state);
                break;
            case StubOperation.Sta:
                Write(address, state.A);
                break;
            case StubOperation.Jmp:
                StubOps.Jmp(state, address);
                break;
            case StubOperation.Bne:
                StubOps.Bne(state, address);
                break;
            case StubOperation.Hlt:
                StubOps.Hlt(state);
                break;
            default:
                ThrowUnhandled(operation);
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Value(OperandMode mode, int operand, ushort address) =>
        mode == OperandMode.Immediate8 ? (byte)operand : Read(address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Read(ushort address)
    {
        _activity |= BusActivity.Read;
        return _bus.Read(address);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Write(ushort address, byte value)
    {
        _activity |= BusActivity.Write;
        _bus.Write(address, value);
    }

    private StubOpcodeTable Build()
    {
        var table = new StubOpcodeTable();
        ConfigureOpcodes(_isa, table);
        return table.Seal();
    }
}
