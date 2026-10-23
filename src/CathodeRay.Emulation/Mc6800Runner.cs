namespace CathodeRay.Emulation;

/// <summary>Runner 6800 (w testach i w <c>cathode run</c>).</summary>
public sealed class Mc6800Runner : ICpuRunner
{
    private readonly Mc6800Cpu _cpu = new();

    public int Pc => _cpu.Pc;

    public long Cycles => _cpu.Cycles;

    public bool Halted => _cpu.Halted;

    public string Registers() => $"A={_cpu.A:X2} B={_cpu.B:X2} X={_cpu.X:X4} SP={_cpu.Sp:X4} CC={_cpu.Cc:X2}";

    public void Load(int address, byte[] bytes) => bytes.CopyTo(_cpu.Memory, address);

    public void Start(int address) => _cpu.Pc = (ushort)address;

    public void Step() => _cpu.Step();

    public int Read(int address) => _cpu.Memory[address];
}
