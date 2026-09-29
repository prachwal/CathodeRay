using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class Mc6800CpuTests
{
    private static Mc6800Cpu Run(params byte[] program)
    {
        var cpu = new Mc6800Cpu { Pc = 0x1000, Sp = 0x2000 };
        program.CopyTo(cpu.Memory, 0x1000);
        cpu.Memory[0x1000 + program.Length] = 0x3E; // WAI
        for (int i = 0; i <= program.Length && !cpu.Halted; i++)
        {
            cpu.Step();
        }

        return cpu;
    }

    [Theory]
    [InlineData(0x50, 0x50, 0xA0, Mc6800Cpu.Overflow | Mc6800Cpu.Negative)]
    [InlineData(0xFF, 0x01, 0x00, Mc6800Cpu.Carry | Mc6800Cpu.Zero)]
    [InlineData(0x10, 0x20, 0x30, 0)]
    public void Adda_Flags(byte a, byte m, byte result, int flags)
    {
        Mc6800Cpu cpu = Run(0x86, a, 0x8B, m); // LDAA #a; ADDA #m
        cpu.A.Should().Be(result);
        (cpu.Cc & 0x0F).Should().Be(flags);
    }

    [Theory]
    [InlineData(0x10, 0x20, 0xF0, Mc6800Cpu.Carry | Mc6800Cpu.Negative)]
    [InlineData(0x20, 0x20, 0x00, Mc6800Cpu.Zero)]
    [InlineData(0x80, 0x01, 0x7F, Mc6800Cpu.Overflow)]
    public void Suba_Flags(byte a, byte m, byte result, int flags)
    {
        Mc6800Cpu cpu = Run(0x86, a, 0x80, m); // SUBA #m
        cpu.A.Should().Be(result);
        (cpu.Cc & 0x0F).Should().Be(flags);
    }

    [Fact]
    public void Indexed_Store_Uses_Big_Endian_Index_Register()
    {
        // LDX #$3000; LDAA #$5A; STAA 3,X; LDAA 3,X (clear A first via LDAA #0)
        Mc6800Cpu cpu = Run(0xCE, 0x30, 0x00, 0x86, 0x5A, 0xA7, 0x03, 0x86, 0x00, 0xA6, 0x03);
        cpu.Memory[0x3003].Should().Be(0x5A);
        cpu.A.Should().Be(0x5A);
        cpu.X.Should().Be(0x3000);
    }

    [Fact]
    public void Subroutine_Call_And_Return()
    {
        var cpu = new Mc6800Cpu { Pc = 0x1000, Sp = 0x2000 };
        new byte[] { 0xBD, 0x10, 0x10, 0x3E }.CopyTo(cpu.Memory, 0x1000); // JSR $1010; WAI
        new byte[] { 0x4C, 0x39 }.CopyTo(cpu.Memory, 0x1010); // INCA; RTS
        for (int i = 0; i < 10 && !cpu.Halted; i++)
        {
            cpu.Step();
        }

        cpu.A.Should().Be(1);
        cpu.Sp.Should().Be(0x2000);
    }

    [Fact]
    public void Unsupported_Opcode_Throws()
    {
        var cpu = new Mc6800Cpu { Pc = 0x1000 };
        cpu.Memory[0x1000] = 0x19; // DAA
        Action act = cpu.Step;
        act.Should().Throw<NotSupportedException>();
    }
}
