namespace CathodeRay.Emulation;

/// <summary>Runner procesora Motorola 6809 dla testów celu.</summary>
public sealed class Mc6809Runner : ICpuRunner
{
    private readonly Mc6809Cpu _cpu = new();

    public int Pc => _cpu.Pc;

    public long Cycles => _cpu.Cycles;

    public bool Halted => _cpu.Halted;

    public string Registers() => $"A={_cpu.A:X2} B={_cpu.B:X2} X={_cpu.X:X4} Y={_cpu.Y:X4} U={_cpu.U:X4} S={_cpu.S:X4} DP={_cpu.Dp:X2} CC={_cpu.Cc:X2}";

    public void Load(int address, byte[] bytes) => bytes.CopyTo(_cpu.Memory, address);

    public void Start(int address) => _cpu.Pc = (ushort)address;

    public void Step() => _cpu.Step();

    public int Read(int address) => _cpu.Memory[address & 0xFFFF];
}
