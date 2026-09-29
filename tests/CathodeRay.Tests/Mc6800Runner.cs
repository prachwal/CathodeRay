namespace CathodeRay.Tests;

/// <summary>Runner 6800 dla <see cref="TargetHarness"/>.</summary>
public sealed class Mc6800Runner : ICpuRunner
{
    private readonly Mc6800Cpu _cpu = new();

    public bool Halted => _cpu.Halted;

    public void Load(int address, byte[] bytes) => bytes.CopyTo(_cpu.Memory, address);

    public void Start(int address) => _cpu.Pc = (ushort)address;

    public void Step() => _cpu.Step();

    public int Read(int address) => _cpu.Memory[address];
}
