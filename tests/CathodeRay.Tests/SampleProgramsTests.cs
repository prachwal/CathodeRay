using CathodeRay.Stub;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class SampleProgramsTests
{
    private static readonly DirectoryInfo Repo = FindRepo();

    private static DirectoryInfo FindRepo()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "samples", "stub")))
        {
            dir = dir.Parent;
        }

        return dir!;
    }

    private static (StubCpu Cpu, StubBus Bus) Run(string sample)
    {
        StubIsa isa = StubIsa.FromJsonFile(Path.Combine(Repo.FullName, "data", "instructions", "mcp_stub_instructions.json"));
        byte[] image = StubAssembler.Assemble(File.ReadAllText(Path.Combine(Repo.FullName, "samples", "stub", sample)), isa);
        var bus = new StubBus();
        for (int i = 0; i < image.Length; i++)
        {
            bus.Write((ushort)i, image[i]);
        }

        var cpu = new StubCpu(isa, bus);
        for (int steps = 0; !cpu.State.Halted; steps++)
        {
            steps.Should().BeLessThan(10_000, "program ma się zatrzymać");
            cpu.Step();
        }

        return (cpu, bus);
    }

    private static byte[] Memory(StubBus bus, int start, int length) =>
        Enumerable.Range(start, length).Select(a => bus.Read((ushort)a)).ToArray();

    private static readonly int[] Fibonacci =
        [0, 1, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, 233, 377, 610, 987, 1597, 2584, 4181, 6765, 10946, 17711, 28657, 46368];

    [Theory]
    [InlineData("fib.asm")]
    [InlineData("fib_selfmod.asm")]
    public void Fib8_Computes_F0_To_F13(string sample)
    {
        var (_, bus) = Run(sample);

        Memory(bus, 0x200, 14).Should().Equal(Fibonacci[..14].Select(static f => (byte)f));
    }

    [Fact]
    public void Indexed_Version_Is_Shorter_Than_Self_Modifying()
    {
        Run("fib.asm").Cpu.InstructionCount.Should().Be(1 + (12 * 6) + 1);
        Run("fib_selfmod.asm").Cpu.InstructionCount.Should().Be(264);
    }

    [Fact]
    public void Fib16_Computes_F0_To_F24()
    {
        var (_, bus) = Run("fib16.asm");

        byte[] lo = Memory(bus, 0x200, 25);
        byte[] hi = Memory(bus, 0x300, 25);
        Enumerable.Range(0, 25).Select(i => lo[i] | (hi[i] << 8)).Should().Equal(Fibonacci);
    }

    [Fact]
    public void Sum_Stores_Result()
    {
        var (cpu, bus) = Run("sum.asm");

        bus.Read(0x20).Should().Be(4);
        cpu.State.Carry.Should().BeTrue();
    }
}
