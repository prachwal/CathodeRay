namespace CathodeRay.Tests;

/// <summary>Runner procesora Motorola 6809 dla testów celu.</summary>
public sealed class Mc6809Runner : ICpuRunner
{
    private readonly Mc6809Cpu _cpu = new();

    public bool Halted => _cpu.Halted;

    public void Load(int address, byte[] bytes) => bytes.CopyTo(_cpu.Memory, address);

    public void Start(int address) => _cpu.Pc = (ushort)address;

    public void Step() => _cpu.Step();

    public int Read(int address) => _cpu.Memory[address & 0xFFFF];
}
