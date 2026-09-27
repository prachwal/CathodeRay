using CathodeRay.Abstractions;

namespace CathodeRay.Stub;

/// <summary>Minimalna zaślepka CPU napędzana tabelą opcode z JSON — do testowania abstrakcji <see cref="ICpu{TState}"/> i <see cref="IBus"/>.</summary>
public sealed class StubCpu : ICpu<StubState>
{
    private readonly StubIsa _isa;
    private readonly IBus _bus;

    /// <summary>Tworzy zaślepkę z tabelą opcode i szyną pamięci.</summary>
    /// <param name="isa">Tabela opcode wczytana z JSON.</param>
    /// <param name="bus">Szyna pamięci.</param>
    public StubCpu(StubIsa isa, IBus bus)
    {
        _isa = isa;
        _bus = bus;
        State = new StubState();
    }

    /// <inheritdoc/>
    public StubState State { get; }

    /// <summary>Suma cykli od ostatniego <see cref="Reset"/>.</summary>
    public int TotalCycles { get; private set; }

    /// <inheritdoc/>
    public int Step()
    {
        if (State.Halted)
        {
            return 0;
        }

        ushort pc = State.ProgramCounter;
        byte opcode = _bus.Read(pc);
        if (!_isa.Opcodes.TryGetValue(opcode, out StubOpcode? op))
        {
            throw new InvalidOperationException($"Unknown opcode 0x{opcode:X2} at 0x{pc:X4}.");
        }

        int operand = op.Words switch
        {
            2 => _bus.Read((ushort)(pc + 1)),
            3 => _bus.Read((ushort)(pc + 1)) | (_bus.Read((ushort)(pc + 2)) << 8),
            _ => 0,
        };
        State.ProgramCounter = (ushort)(pc + op.Words);
        Execute(op.Mnemonic, operand);
        TotalCycles += op.Cycles;
        return op.Cycles;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        State.A = 0;
        State.ProgramCounter = 0;
        State.Halted = false;
        TotalCycles = 0;
    }

    private void Execute(string mnemonic, int operand)
    {
        switch (mnemonic)
        {
            case "NOP":
                break;
            case "LDI":
                State.A = (byte)operand;
                break;
            case "ADD":
                State.A = (byte)(State.A + operand);
                break;
            case "INC":
                State.A = (byte)(State.A + 1);
                break;
            case "STA":
                _bus.Write((ushort)operand, State.A);
                break;
            case "LDA":
                State.A = _bus.Read((ushort)operand);
                break;
            case "JMP":
                State.ProgramCounter = (ushort)operand;
                break;
            case "HLT":
                State.Halted = true;
                break;
            default:
                throw new InvalidOperationException($"Unknown mnemonic '{mnemonic}'.");
        }
    }
}
