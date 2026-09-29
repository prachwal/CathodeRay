namespace CathodeRay.Tests;

/// <summary>Runner Z80 (albo 8080) dla <see cref="TargetHarness"/>.</summary>
public sealed class Z80Runner : ICpuRunner
{
    private readonly Z80Cpu _cpu;

    public Z80Runner(bool intel8080 = false) => _cpu = new Z80Cpu(intel8080);

    public bool Halted => _cpu.Halted;

    public void Load(int address, byte[] bytes) => bytes.CopyTo(_cpu.Memory, address);

    public void Start(int address) => _cpu.Pc = (ushort)address;

    public void Step() => _cpu.Step();

    public int Read(int address) => _cpu.Memory[address];
}
