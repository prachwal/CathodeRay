using CathodeRay.Abstractions;

namespace CathodeRay.Stub;

/// <summary>Minimalna zaślepka CPU napędzana tabelą opcode z JSON — do testowania abstrakcji <see cref="ICpu{TState}"/> i <see cref="IBus"/>.
/// Rejestrację opcode można nadpisać (<see cref="ConfigureOpcodes"/>) i dodać/poprawić/usunąć wpisy (delta CPU).
/// Implementuje opcjonalny <see cref="ICpuStatus"/>, więc nadaje się do diagnostyki (<see cref="CpuDiagnostics{TState}"/>).</summary>
public class StubCpu : ICpu<StubState>, ICpuStatus
{
    private readonly IBus _bus;
    private readonly StubIsa _isa;
    private readonly StubOpcodeTable _opcodes;
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
        _opcodes = Build();
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
    public string? LastMnemonic { get; private set; }

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
        if (!_opcodes.TryGet(opcode, out StubOpcodeEntry? entry) || entry is null)
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
        entry.Handler(new OpcodeContext(opcode, operand, pc));
        LastBusActivity = _activity;
        LastOpcode = opcode;
        LastMnemonic = entry.Mnemonic;
        CycleCount += (ulong)entry.Cycles;
        InstructionCount++;
        return entry.Cycles;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        State.A = 0;
        State.ProgramCounter = 0;
        State.Halted = false;
        CycleCount = 0;
        InstructionCount = 0;
        LastBusActivity = BusActivity.None;
        LastOpcode = -1;
        LastMnemonic = null;
        _activity = BusActivity.None;
    }

    /// <inheritdoc/>
    public RegisterView CaptureRegisters() => new(
    [
        new RegisterEntry(new RegisterDefinition("A", 8, RegisterRole.Accumulator), State.A),
        new RegisterEntry(new RegisterDefinition("PC", 16, RegisterRole.ProgramCounter), State.ProgramCounter),
    ]);

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

    /// <summary>Rejestruje opcode'y z JSON; podklasa nadpisuje i woła <c>base</c>, po czym zmienia tabelę (Add/Replace/Remove).</summary>
    /// <param name="isa">Tabela opcode z JSON.</param>
    /// <param name="table">Rejestr docelowy (jeszcze niezamknięty).</param>
    protected virtual void ConfigureOpcodes(StubIsa isa, StubOpcodeTable table)
    {
        foreach ((byte opcode, StubOpcode definition) in isa.Opcodes)
        {
            Action<OpcodeContext>? handler = ResolveBehavior(definition.Mnemonic)
                ?? throw new InvalidOperationException(
                    $"No handler for mnemonic '{definition.Mnemonic}' (opcode 0x{opcode:X2}).");
            RegisterOpcode(table, opcode, definition.Mnemonic, definition.Cycles, definition.Words, handler);
        }
    }

    /// <summary>Mapuje mnemonic na handler; podklasa nadpisuje, żeby dodać własne mnemoniki.</summary>
    /// <param name="mnemonic">Mnemonik z JSON.</param>
    /// <returns>Handler lub <see langword="null"/>.</returns>
    protected virtual Action<OpcodeContext>? ResolveBehavior(string mnemonic) => mnemonic switch
    {
        "NOP" => Nop,
        "LDI" => Ldi,
        "ADD" => Add,
        "INC" => Inc,
        "STA" => Sta,
        "LDA" => Lda,
        "JMP" => Jmp,
        "HLT" => Hlt,
        _ => null,
    };

    private StubOpcodeTable Build()
    {
        var table = new StubOpcodeTable();
        ConfigureOpcodes(_isa, table);
        return table.Seal();
    }

    private void Nop(OpcodeContext ctx) => _ = ctx;

    private void Ldi(OpcodeContext ctx) => State.A = (byte)ctx.Operand;

    private void Add(OpcodeContext ctx) => State.A = (byte)(State.A + ctx.Operand);

    private void Inc(OpcodeContext ctx)
    {
        _ = ctx;
        State.A = (byte)(State.A + 1);
    }

    private void Sta(OpcodeContext ctx)
    {
        _activity |= BusActivity.Write;
        _bus.Write((ushort)ctx.Operand, State.A);
    }

    private void Lda(OpcodeContext ctx)
    {
        _activity |= BusActivity.Read;
        State.A = _bus.Read((ushort)ctx.Operand);
    }

    private void Jmp(OpcodeContext ctx) => State.ProgramCounter = (ushort)ctx.Operand;

    private void Hlt(OpcodeContext ctx)
    {
        _ = ctx;
        _activity |= BusActivity.Halt;
        State.Halted = true;
    }
}
