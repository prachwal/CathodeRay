using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class Z80CpuTests
{
    private static Z80Cpu Run(bool intel8080, params byte[] program)
    {
        var cpu = new Z80Cpu(intel8080) { Pc = 0x1000, Sp = 0x2000 };
        program.CopyTo(cpu.Memory, 0x1000);
        cpu.Memory[0x1000 + program.Length] = 0x76;
        for (int i = 0; i <= program.Length && !cpu.Halted; i++)
        {
            cpu.Step();
        }

        return cpu;
    }

    [Theory]
    [InlineData(0x50, 0x50, 0xA0, Z80Cpu.FlagP | Z80Cpu.FlagS)]
    [InlineData(0xFF, 0x01, 0x00, Z80Cpu.FlagC | Z80Cpu.FlagZ | Z80Cpu.FlagH)]
    [InlineData(0x0F, 0x01, 0x10, Z80Cpu.FlagH)]
    public void Add_Flags(byte a, byte m, byte result, int flags)
    {
        Z80Cpu cpu = Run(false, 0x3E, a, 0xC6, m); // LD A,a; ADD A,m
        cpu.A.Should().Be(result);
        cpu.F.Should().Be((byte)flags);
    }

    [Theory]
    [InlineData(0x10, 0x20, 0xF0, Z80Cpu.FlagC | Z80Cpu.FlagS | Z80Cpu.FlagN)]
    [InlineData(0x20, 0x20, 0x00, Z80Cpu.FlagZ | Z80Cpu.FlagN)]
    [InlineData(0x80, 0x01, 0x7F, Z80Cpu.FlagP | Z80Cpu.FlagH | Z80Cpu.FlagN)]
    public void Sub_Flags(byte a, byte m, byte result, int flags)
    {
        Z80Cpu cpu = Run(false, 0x3E, a, 0xD6, m); // SUB m
        cpu.A.Should().Be(result);
        cpu.F.Should().Be((byte)flags);
    }

    [Fact]
    public void Cp_Leaves_A_And_Sets_Carry_On_Borrow()
    {
        Z80Cpu cpu = Run(false, 0x3E, 0x05, 0xFE, 0x09);
        cpu.A.Should().Be(5);
        cpu.Flag(Z80Cpu.FlagC).Should().BeTrue();
    }

    [Fact]
    public void Shifts_Through_Carry()
    {
        Z80Cpu cpu = Run(false, 0x3E, 0x81, 0xCB, 0x27, 0xCB, 0x17); // SLA A; RL A
        cpu.A.Should().Be(0x05);
        cpu.Flag(Z80Cpu.FlagC).Should().BeFalse();
        cpu = Run(false, 0x3E, 0x01, 0xCB, 0x3F, 0xCB, 0x1F); // SRL A; RR A
        cpu.A.Should().Be(0x80);
    }

    [Fact]
    public void Loads_Stack_And_Calls()
    {
        // LD HL,$3000; LD (HL),$5A; LD A,(HL); PUSH AF; CALL $1010 ; ... (1010) POP HL? use RET
        var cpu = new Z80Cpu { Pc = 0x1000, Sp = 0x2000 };
        new byte[] { 0x21, 0x00, 0x30, 0x36, 0x5A, 0x7E, 0xCD, 0x10, 0x10, 0x76 }.CopyTo(cpu.Memory, 0x1000);
        new byte[] { 0x3C, 0xC9 }.CopyTo(cpu.Memory, 0x1010); // INC A; RET
        for (int i = 0; i < 20 && !cpu.Halted; i++)
        {
            cpu.Step();
        }

        cpu.A.Should().Be(0x5B);
        cpu.Memory[0x3000].Should().Be(0x5A);
        cpu.Sp.Should().Be(0x2000);
    }

    [Fact]
    public void Eight080_Rejects_Z80_Only_Opcodes()
    {
        var cpu = new Z80Cpu(true) { Pc = 0x1000 };
        cpu.Memory[0x1000] = 0xCB;
        Action act = cpu.Step;
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Jump_To_Self_Halts()
    {
        Z80Cpu cpu = Run(false, 0xC3, 0x00, 0x10);
        cpu.Halted.Should().BeTrue();
    }
}
