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
    public void Indexed_Pcr_Uses_Program_Counter_After_Offset()
    {
        Mc6809Cpu cpu = new() { Pc = 0x1000 };
        new byte[] { 0xA6, 0x8C, 0x02, 0x13, 0x13, 0x5A }.CopyTo(cpu.Memory, 0x1000);

        cpu.Step();

        cpu.A.Should().Be(0x5A);
    }

    [Fact]
    public void Infinite_Self_Branch_Does_Not_Halt_The_Cpu()
    {
        var cpu = new Mc6809Cpu { Pc = 0x1000 };
        new byte[] { 0x20, 0xFE }.CopyTo(cpu.Memory, 0x1000);

        cpu.Step();

        cpu.Pc.Should().Be(0x1000);
        cpu.Halted.Should().BeFalse();
    }

    [Fact]
    public void Decimal_Adjust_After_Bcd_Addition()
    {
        Mc6809Cpu cpu = Run(0x86, 0x09, 0x8B, 0x01, 0x19);

        cpu.A.Should().Be(0x10);
        cpu.Flag(Mc6809Cpu.Carry | Mc6809Cpu.Zero | Mc6809Cpu.Negative).Should().BeFalse();
    }

    [Theory]
    [InlineData(0x10, 0x08, 0x0080, true)]
    [InlineData(0x02, 0x03, 0x0006, false)]
    public void Multiply_Produces_D_And_Carry_From_Bit_Seven(byte a, byte b, ushort product, bool carry)
    {
        Mc6809Cpu cpu = Run(0x86, a, 0xC6, b, 0x3D);

        cpu.D.Should().Be(product);
        cpu.Flag(Mc6809Cpu.Carry).Should().Be(carry);
    }

    [Fact]
    public void Sex_Sign_Extends_B_Into_D()
    {
        Mc6809Cpu cpu = Run(0xC6, 0x80, 0x1D);

        cpu.D.Should().Be(0xFF80);
        cpu.Flag(Mc6809Cpu.Negative).Should().BeTrue();
        cpu.Flag(Mc6809Cpu.Overflow).Should().BeFalse();
    }

    [Fact]
    public void Abx_Adds_Unsigned_B_To_X_Without_Changing_Flags()
    {
        var cpu = new Mc6809Cpu { Pc = 0x1000, X = 0x1234, B = 0x10, Cc = Mc6809Cpu.Carry | Mc6809Cpu.Zero };
        cpu.Memory[0x1000] = 0x3A;

        cpu.Step();

        cpu.X.Should().Be(0x1244);
        cpu.Cc.Should().Be(Mc6809Cpu.Carry | Mc6809Cpu.Zero);
    }

    [Theory]
    [InlineData(0x40, 0x80, 0x80, Mc6809Cpu.Negative | Mc6809Cpu.Overflow | Mc6809Cpu.Carry)]
    [InlineData(0x43, 0x00, 0xFF, Mc6809Cpu.Negative | Mc6809Cpu.Carry)]
    [InlineData(0x44, 0x01, 0x00, Mc6809Cpu.Zero | Mc6809Cpu.Overflow | Mc6809Cpu.Carry)]
    [InlineData(0x47, 0x80, 0xC0, Mc6809Cpu.Negative | Mc6809Cpu.Overflow)]
    public void Accumulator_Unary_Operations_Set_Expected_Flags(byte opcode, byte input, byte result, int flags)
    {
        Mc6809Cpu cpu = Run(0x86, input, opcode);

        cpu.A.Should().Be(result);
        (cpu.Cc & 0x0F).Should().Be(flags);
    }

    [Theory]
    [InlineData(0x49, 0x80, 0x01)]
    [InlineData(0x46, 0x00, 0x80)]
    public void Accumulator_Rotates_Through_Carry(byte opcode, byte input, byte result)
    {
        var cpu = new Mc6809Cpu { Pc = 0x1000, Cc = Mc6809Cpu.Carry };
        new byte[] { 0x86, input, opcode, 0x13 }.CopyTo(cpu.Memory, 0x1000);
        cpu.Step();
        cpu.Step();

        cpu.A.Should().Be(result);
        cpu.Flag(Mc6809Cpu.Carry).Should().Be(opcode == 0x49);
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
