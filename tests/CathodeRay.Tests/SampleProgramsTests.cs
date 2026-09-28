using CathodeRay.Assembler;
using CathodeRay.Stub;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class SampleProgramsTests
{
    private static (StubCpu Cpu, StubBus Bus) Run(string sample)
    {
        StubIsa isa = StubIsa.FromJsonFile(Repo.IsaFile("mcp_stub_instructions.json"));
        AssemblyResult result = Repo.Assemble("stub", File.ReadAllText(Repo.Path("samples", "stub", sample)));
        result.Origin.Should().Be(0, "stub ładuje program od adresu 0");
        byte[] image = result.Image;
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
    public void Subroutine_Returns_With_Stack_Balanced()
    {
        var (cpu, bus) = Run("subroutine.asm");

        bus.Read(0x20).Should().Be(42);
        cpu.State.A.Should().Be(42);
        cpu.State.StackPointer.Should().Be(0xFF);
    }

    [Fact]
    public void Sum_Stores_Result()
    {
        var (cpu, bus) = Run("sum.asm");

        bus.Read(0x20).Should().Be(4);
        cpu.State.Carry.Should().BeTrue();
    }
}
