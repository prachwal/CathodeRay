namespace CathodeRay.Emulation;

/// <summary>Runner Z80 (albo 8080) (w testach i w <c>cathode run</c>).</summary>
public sealed class Z80Runner : ICpuRunner
{
    private readonly Z80Cpu _cpu;

    public Z80Runner(bool intel8080 = false) => _cpu = new Z80Cpu(intel8080);

    public int Pc => _cpu.Pc;

    public long Cycles => 0;

    public bool Halted => _cpu.Halted;

    public string Registers() => $"A={_cpu.A:X2} F={_cpu.F:X2} BC={_cpu.Bc:X4} DE={_cpu.De:X4} HL={_cpu.Hl:X4} SP={_cpu.Sp:X4}";

    public void Load(int address, byte[] bytes) => bytes.CopyTo(_cpu.Memory, address);

    public void Start(int address) => _cpu.Pc = (ushort)address;

    public void Step() => _cpu.Step();

    public int Read(int address) => _cpu.Memory[address];
}
