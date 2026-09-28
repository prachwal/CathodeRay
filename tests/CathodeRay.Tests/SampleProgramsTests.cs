using CathodeRay.Assembler;
using CathodeRay.Assembler.Link;
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

    private static (StubCpu Cpu, StubBus Bus, AssemblyResult Result) RunFile(string sample)
    {
        StubIsa isa = StubIsa.FromJsonFile(Repo.IsaFile("mcp_stub_instructions.json"));
        string path = Repo.Path("samples", "stub", sample);
        AssemblerTarget target = AssemblerTargets.Find("stub")!;
        static string? ReadText(string p) => File.Exists(p) ? File.ReadAllText(p) : null;
        static byte[]? ReadBinary(string p) => File.Exists(p) ? File.ReadAllBytes(p) : null;
        AssemblyResult result = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax)
            .Assemble(File.ReadAllText(path), path, ReadText, [], ReadBinary);
        result.Origin.Should().Be(0, "stub ładuje program od adresu 0");
        var bus = new StubBus();
        for (int i = 0; i < result.Image.Length; i++)
        {
            bus.Write((ushort)(result.Origin + i), result.Image[i]);
        }

        var cpu = new StubCpu(isa, bus);
        for (int steps = 0; !cpu.State.Halted; steps++)
        {
            steps.Should().BeLessThan(10_000, "program ma się zatrzymać");
            cpu.Step();
        }

        return (cpu, bus, result);
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
    public void Features_Showcase_Uses_Every_Assembler_Mechanism()
    {
        var (cpu, bus, result) = RunFile(Path.Combine("features", "main.asm"));

        int At(string symbol) => result.Symbols[symbol];
        bus.Read((ushort)At("data::total")).Should().Be(20);
        bus.Read((ushort)At("data::variant")).Should().Be(1);
        bus.Read((ushort)At("data::doubled")).Should().Be(4);
        bus.Read((ushort)At("data::z1")).Should().Be(0);
        bus.Read((ushort)At("data::z2")).Should().Be(5);
        bus.Read((ushort)At("data::pcc")).Should().Be(2);
        cpu.State.X.Should().Be(0);
        cpu.State.StackPointer.Should().Be(0xFF);
        result.Messages.Should().Contain(m => !m.Warning && m.Text == "assembling features");
        result.Messages.Should().Contain(m => m.Warning && m.Text == "demo build");
    }

    [Fact]
    public void Linked_Showcase_Joins_Two_Modules()
    {
        string dir = Repo.Path("samples", "stub", "features", "link");
        AssemblerTarget target = AssemblerTargets.Find("stub")!;
        var assembler = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax);
        static string? Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
        var modules = new List<(string File, ObjectModule Module)>();
        foreach (string name in new[] { "main.s", "lib.s" })
        {
            string path = Path.Combine(dir, name);
            modules.Add((name, assembler.AssembleObject("stub", File.ReadAllText(path), path, Read)));
        }

        AssemblyResult result = Linker.Link(
            modules, LinkerConfig.Parse(File.ReadAllText(Path.Combine(dir, "map.cfg"))));
        result.Origin.Should().Be(0);

        StubIsa isa = StubIsa.FromJsonFile(Repo.IsaFile("mcp_stub_instructions.json"));
        var bus = new StubBus();
        for (int i = 0; i < result.Image.Length; i++)
        {
            bus.Write((ushort)i, result.Image[i]);
        }

        var cpu = new StubCpu(isa, bus);
        for (int steps = 0; !cpu.State.Halted; steps++)
        {
            steps.Should().BeLessThan(10_000, "program ma się zatrzymać");
            cpu.Step();
        }

        result.Symbols["main"].Should().Be(0);
        bus.Read((ushort)result.Symbols["result"]).Should().Be(15);
        cpu.State.A.Should().Be(15);
        cpu.State.StackPointer.Should().Be(0xFF);
    }

    [Fact]
    public void Mul8_Multiplies_With_Zero_Case()
    {
        string dir = Repo.Path("samples", "stub", "features");
        string entry = Path.Combine(dir, "multest.asm");
        string source = "LDI 6\nLDX 7\nCALL mul8\nSTA r1\nLDI 0\nLDX 5\nCALL mul8\nSTA r2\nHLT\nr1: .byte 0\nr2: .byte 0\n.include \"mathlib.s\"\n";
        AssemblerTarget target = AssemblerTargets.Find("stub")!;
        static string? Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
        AssemblyResult result = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax)
            .Assemble(source, entry, Read);

        StubIsa isa = StubIsa.FromJsonFile(Repo.IsaFile("mcp_stub_instructions.json"));
        var bus = new StubBus();
        for (int i = 0; i < result.Image.Length; i++)
        {
            bus.Write((ushort)i, result.Image[i]);
        }

        var cpu = new StubCpu(isa, bus);
        for (int steps = 0; !cpu.State.Halted; steps++)
        {
            steps.Should().BeLessThan(10_000, "program ma się zatrzymać");
            cpu.Step();
        }

        bus.Read((ushort)result.Symbols["r1"]).Should().Be(42);
        bus.Read((ushort)result.Symbols["r2"]).Should().Be(0);
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
