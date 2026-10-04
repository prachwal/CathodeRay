using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class Mc6809CpuTests
{
    [Theory]
    [InlineData(0x7F, 0x01, 0x80, Mc6809Cpu.Negative | Mc6809Cpu.Overflow | Mc6809Cpu.HalfCarry)]
    [InlineData(0xFF, 0x01, 0x00, Mc6809Cpu.Carry | Mc6809Cpu.Zero | Mc6809Cpu.HalfCarry)]
    [InlineData(0x10, 0x20, 0x30, 0)]
    public void Adda_Sets_Result_And_Flags(byte left, byte right, byte result, int flags)
    {
        Mc6809Cpu cpu = Run(0x86, left, 0x8B, right);

        cpu.A.Should().Be(result);
        (cpu.Cc & 0x2F).Should().Be(flags);
    }

    [Theory]
    [InlineData(0x10, 0x20, 0xF0, Mc6809Cpu.Carry | Mc6809Cpu.Negative)]
    [InlineData(0x20, 0x20, 0x00, Mc6809Cpu.Zero)]
    [InlineData(0x80, 0x01, 0x7F, Mc6809Cpu.Overflow)]
    public void Suba_Sets_Result_And_Flags(byte left, byte right, byte result, int flags)
    {
        Mc6809Cpu cpu = Run(0x86, left, 0x80, right);

        cpu.A.Should().Be(result);
        (cpu.Cc & 0x0F).Should().Be(flags);
    }

    [Fact]
    public void Relative_Branch_Uses_Program_Counter_After_Operand()
    {
        Mc6809Cpu cpu = Run(0x26, 0x02, 0x86, 0x00, 0x86, 0x01);

        cpu.A.Should().Be(1);
    }

    [Fact]
    public void Bsr_Rts_Round_Trip_Uses_Descending_Stack()
    {
        var cpu = new Mc6809Cpu { Pc = 0x1000, S = 0x2000 };
        new byte[] { 0x8D, 0x03, 0x86, 0xEE, 0x13, 0x86, 0x01, 0x39 }.CopyTo(cpu.Memory, 0x1000);

        for (int i = 0; i < 8 && !cpu.Halted; i++)
        {
            cpu.Step();
        }

        cpu.A.Should().Be(0xEE);
        cpu.S.Should().Be(0x2000);
        cpu.Memory[0x1FFE].Should().Be(0x10);
        cpu.Memory[0x1FFF].Should().Be(0x02);
    }

    [Fact]
    public void Runs_Assembler_Program_Golden()
    {
        byte[] program = File.ReadAllBytes(Repo.Path("tests", "CathodeRay.Tests", "Asm", "6809", "program.bin"));
        var cpu = new Mc6809Cpu { Pc = 0x1000 };
        program.CopyTo(cpu.Memory, 0x1000);

        for (int i = 0; i < program.Length && !cpu.Halted; i++)
        {
            cpu.Step();
        }

        cpu.Halted.Should().BeTrue();
        cpu.A.Should().Be(0x2A);
    }

    private static Mc6809Cpu Run(params byte[] program)
    {
        var cpu = new Mc6809Cpu { Pc = 0x1000 };
        program.CopyTo(cpu.Memory, 0x1000);
        cpu.Memory[0x1000 + program.Length] = 0x13;
        for (int i = 0; i <= program.Length && !cpu.Halted; i++)
        {
            cpu.Step();
        }

        return cpu;
    }
}
