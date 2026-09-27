using System.Text;
using CathodeRay.Abstractions;
using CathodeRay.Stub;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class StubCpuTests
{
    private static StubIsa LoadIsa()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "data", "instructions", "mcp_stub_instructions.json")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("katalog repo z data/instructions ma być osiągalny z katalogu testów");
        return StubIsa.FromJsonFile(Path.Combine(dir!.FullName, "data", "instructions", "mcp_stub_instructions.json"));
    }

    private static (StubCpu Cpu, StubBus Bus) WithProgram(params byte[] program)
    {
        var bus = new StubBus();
        for (int i = 0; i < program.Length; i++)
        {
            bus.Write((ushort)i, program[i]);
        }

        return (new StubCpu(LoadIsa(), bus), bus);
    }

    [Fact]
    public void Isa_Loads_All_Opcodes()
    {
        StubIsa isa = LoadIsa();
        isa.Opcodes.Should().HaveCount(9);
        isa.Opcodes[0x01].Mnemonic.Should().Be("LDI");
        isa.Opcodes[0x01].Words.Should().Be(2);
        isa.Opcodes[0x07].Mnemonic.Should().Be("SUB");
        isa.Opcodes[0xFF].Mnemonic.Should().Be("HLT");
    }

    [Fact]
    public void Nop_Advances_And_Costs_One_Cycle()
    {
        var (cpu, _) = WithProgram(0x00, 0x00);
        cpu.Step().Should().Be(1);
        cpu.State.ProgramCounter.Should().Be(1);
        cpu.CycleCount.Should().Be(1);
    }

    [Fact]
    public void Ldi_Loads_Immediate()
    {
        var (cpu, _) = WithProgram(0x01, 0x2A);
        cpu.Step().Should().Be(2);
        cpu.State.A.Should().Be(0x2A);
        cpu.State.ProgramCounter.Should().Be(2);
    }

    [Fact]
    public void Add_Adds_Immediate()
    {
        var (cpu, _) = WithProgram(0x01, 0x10, 0x02, 0x05);
        cpu.Step();
        cpu.Step();
        cpu.State.A.Should().Be(0x15);
        cpu.State.ProgramCounter.Should().Be(4);
    }

    [Fact]
    public void Sub_Subtracts_Immediate()
    {
        var (cpu, _) = WithProgram(0x01, 0x10, 0x07, 0x05);
        cpu.Step();
        cpu.Step();
        cpu.State.A.Should().Be(0x0B);
    }

    [Fact]
    public void Add_Sets_Carry()
    {
        var (cpu, _) = WithProgram(0x01, 0xFF, 0x02, 0x01);
        cpu.Step();
        cpu.Step();
        cpu.State.A.Should().Be(0x00);
        cpu.State.Carry.Should().BeTrue();
        cpu.State.Overflow.Should().BeFalse();
    }

    [Fact]
    public void Add_Sets_Overflow()
    {
        var (cpu, _) = WithProgram(0x01, 0x7F, 0x02, 0x01);
        cpu.Step();
        cpu.Step();
        cpu.State.A.Should().Be(0x80);
        cpu.State.Carry.Should().BeFalse();
        cpu.State.Overflow.Should().BeTrue();
    }

    [Fact]
    public void Sub_Sets_Carry_When_No_Borrow()
    {
        var (cpu, _) = WithProgram(0x01, 0x05, 0x07, 0x03);
        cpu.Step();
        cpu.Step();
        cpu.State.A.Should().Be(0x02);
        cpu.State.Carry.Should().BeTrue();
        cpu.State.Overflow.Should().BeFalse();
    }

    [Fact]
    public void Sub_Clears_Carry_On_Borrow()
    {
        var (cpu, _) = WithProgram(0x01, 0x03, 0x07, 0x05);
        cpu.Step();
        cpu.Step();
        cpu.State.A.Should().Be(0xFE);
        cpu.State.Carry.Should().BeFalse();
        cpu.State.Overflow.Should().BeFalse();
    }

    [Fact]
    public void Inc_Preserves_Flags()
    {
        var (cpu, _) = WithProgram(0x01, 0xFF, 0x02, 0x01, 0x03);
        cpu.Step();
        cpu.Step();
        cpu.Step();
        cpu.State.A.Should().Be(0x01);
        cpu.State.Carry.Should().BeTrue();
        cpu.State.Overflow.Should().BeFalse();
    }

    [Fact]
    public void Registers_Include_Flags()
    {
        var (cpu, _) = WithProgram(0x01, 0xFF, 0x02, 0x01);
        cpu.Step();
        cpu.Step();

        RegisterView view = cpu.CaptureRegisters();
        view["C"].Value.Should().Be(1);
        view["C"].Role.Should().Be(RegisterRole.Status);
        view["V"].Value.Should().Be(0);
    }

    [Fact]
    public void Inc_Increments()
    {
        var (cpu, _) = WithProgram(0x01, 0xFF, 0x03);
        cpu.Step();
        cpu.Step();
        cpu.State.A.Should().Be(0x00, "inkrementacja zawija się modulo 256");
    }

    [Fact]
    public void Sta_Then_Lda_Roundtrips_Through_Bus()
    {
        var (cpu, bus) = WithProgram(0x01, 0x10, 0x05, 0x20, 0x00, 0x01, 0x00, 0x06, 0x20, 0x00);
        cpu.Step();
        cpu.Step();
        bus.Read(0x20).Should().Be(0x10);
        cpu.Step();
        cpu.State.A.Should().Be(0x00);
        cpu.Step();
        cpu.State.A.Should().Be(0x10);
    }

    [Fact]
    public void Jmp_Sets_ProgramCounter()
    {
        var (cpu, _) = WithProgram(0x04, 0x04, 0x00, 0x00, 0x03);
        cpu.Step().Should().Be(3);
        cpu.State.ProgramCounter.Should().Be(4);
        cpu.Step();
        cpu.State.A.Should().Be(1);
    }

    [Fact]
    public void Hlt_Stops_Stepping()
    {
        var (cpu, _) = WithProgram(0xFF, 0x03);
        cpu.Step().Should().Be(1);
        cpu.State.Halted.Should().BeTrue();
        cpu.Step().Should().Be(0);
        cpu.State.ProgramCounter.Should().Be(1, "zatrzymany CPU nie przesuwa PC");
    }

    [Fact]
    public void Reset_Zeroes_State_And_Cycles()
    {
        var (cpu, _) = WithProgram(0x01, 0x2A, 0xFF);
        cpu.Step();
        cpu.Step();
        cpu.CycleCount.Should().BeGreaterThan(0);
        cpu.Reset();
        cpu.State.A.Should().Be(0);
        cpu.State.ProgramCounter.Should().Be(0);
        cpu.State.Halted.Should().BeFalse();
        cpu.CycleCount.Should().Be(0);
    }

    [Fact]
    public void Clone_Is_Independent()
    {
        var (cpu, _) = WithProgram(0x01, 0x2A);
        cpu.Step();
        StubState snapshot = cpu.State.Clone();
        cpu.State.A = 0x99;
        cpu.State.ProgramCounter = 0x1234;
        snapshot.A.Should().Be(0x2A);
        snapshot.ProgramCounter.Should().Be(2);
    }

    [Fact]
    public void CycleCount_Accumulates_Across_Steps()
    {
        var (cpu, _) = WithProgram(0x01, 0x01, 0x02, 0x02, 0x00);
        cpu.Step();
        cpu.Step();
        cpu.Step();
        cpu.CycleCount.Should().Be(2 + 2 + 1);
    }

    [Fact]
    public void Unknown_Opcode_Throws()
    {
        var (cpu, _) = WithProgram(0x7F);
        FluentActions.Invoking(() => cpu.Step()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Unknown_Mnemonic_Throws_On_First_Step()
    {
        const string json = """{"instructions":[{"opcode":"00","mnemonic":"XYZ","cycles":1,"words":1}]}""";
        StubIsa isa = StubIsa.FromJson(new MemoryStream(Encoding.UTF8.GetBytes(json)));
        var cpu = new StubCpu(isa, new StubBus());

        FluentActions.Invoking(() => cpu.Step())
            .Should().Throw<InvalidOperationException>().WithMessage("*XYZ*");
    }

    [Fact]
    public void Cpu_Is_Usable_Through_Abstraction()
    {
        ICpu<StubState> cpu = new StubCpu(LoadIsa(), new StubBus());
        cpu.State.ProgramCounter.Should().Be(0);
        cpu.Reset();
    }
}
