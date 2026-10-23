namespace CathodeRay.Emulation;

/// <summary>Runner 6502 (w testach i w <c>cathode run</c>).</summary>
public sealed class Mos6502Runner : ICpuRunner
{
    private readonly Mos6502 _cpu = new();

    public int Pc => _cpu.Pc;

    public long Cycles => _cpu.Cycles;

    public bool Halted => _cpu.Halted;

    public string Registers() => $"A={_cpu.A:X2} X={_cpu.X:X2} Y={_cpu.Y:X2} SP={_cpu.Sp:X2} P={_cpu.P:X2}";

    public void Load(int address, byte[] bytes) => bytes.CopyTo(_cpu.Memory, address);

    public void Start(int address) => _cpu.Pc = (ushort)address;

    public void Step() => _cpu.Step();

    public int Read(int address) => _cpu.Memory[address];
}
