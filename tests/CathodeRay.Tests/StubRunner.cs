using CathodeRay.Stub;

namespace CathodeRay.Tests;

/// <summary>Runner procesora stub.</summary>
public sealed class StubRunner : ICpuRunner
{
    private readonly StubBus _bus = new();
    private readonly StubCpu _cpu;

    public StubRunner() => _cpu = new StubCpu(StubIsa.FromJsonFile(Repo.IsaFile("mcp_stub_instructions.json")), _bus);

    public bool Halted => _cpu.State.Halted;

    public void Load(int address, byte[] bytes)
    {
        for (int i = 0; i < bytes.Length; i++)
        {
            _bus.Write((ushort)(address + i), bytes[i]);
        }
    }

    public void Start(int address) => _cpu.State.ProgramCounter = (ushort)address;

    public void Step() => _cpu.Step();

    public int Read(int address) => _bus.Read((ushort)address);
}
