namespace CathodeRay.Tests;

/// <summary>Runner 6502 dla <see cref="TargetHarness"/>.</summary>
public sealed class Mos6502Runner : ICpuRunner
{
    private readonly Mos6502 _cpu = new();

    public bool Halted => _cpu.Halted;

    public void Load(int address, byte[] bytes) => bytes.CopyTo(_cpu.Memory, address);

    public void Start(int address) => _cpu.Pc = (ushort)address;

    public void Step() => _cpu.Step();

    public int Read(int address) => _cpu.Memory[address];
}
