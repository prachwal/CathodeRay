using CathodeRay.Assembler;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class Mos6502Tests
{
    private static Mos6502 Run(params byte[] program)
    {
        var cpu = new Mos6502 { Pc = 0x1000 };
        program.CopyTo(cpu.Memory, 0x1000);
        cpu.Memory[0x1000 + program.Length] = 0x02; // KIL
        for (int i = 0; i <= program.Length && !cpu.Halted; i++)
        {
            cpu.Step();
        }

        return cpu;
    }

    [Theory]
    [InlineData(0x00, Mos6502.Zero)]
    [InlineData(0x80, Mos6502.Negative)]
    [InlineData(0x42, 0)]
    public void Lda_Immediate_Sets_Nz(byte value, int flags)
    {
        Mos6502 cpu = Run(0xA9, value);
        cpu.A.Should().Be(value);
        (cpu.P & (Mos6502.Zero | Mos6502.Negative)).Should().Be(flags);
        cpu.Cycles.Should().Be(2 + 2);
    }

    [Theory]
    [InlineData(0x50, 0x50, 0, 0xA0, Mos6502.Overflow | Mos6502.Negative)]
    [InlineData(0xFF, 0x01, 0, 0x00, Mos6502.Carry | Mos6502.Zero)]
    [InlineData(0x7F, 0x00, 1, 0x80, Mos6502.Overflow | Mos6502.Negative)]
    [InlineData(0xD0, 0x90, 0, 0x60, Mos6502.Carry | Mos6502.Overflow)]
    public void Adc_Flags(byte a, byte m, int carry, byte result, int flags)
    {
        Mos6502 cpu = Run(carry == 1 ? (byte)0x38 : (byte)0x18, 0xA9, a, 0x69, m);
        cpu.A.Should().Be(result);
        (cpu.P & (Mos6502.Carry | Mos6502.Zero | Mos6502.Overflow | Mos6502.Negative)).Should().Be(flags);
    }

    [Theory]
    [InlineData(0x50, 0xF0, 1, 0x60, 0)]
    [InlineData(0x50, 0xB0, 1, 0xA0, Mos6502.Overflow | Mos6502.Negative)]
    [InlineData(0x00, 0x01, 1, 0xFF, Mos6502.Negative)]
    [InlineData(0x05, 0x05, 0, 0xFF, Mos6502.Negative)]
    [InlineData(0x05, 0x05, 1, 0x00, Mos6502.Carry | Mos6502.Zero)]
    public void Sbc_Flags(byte a, byte m, int carry, byte result, int flags)
    {
        Mos6502 cpu = Run(carry == 1 ? (byte)0x38 : (byte)0x18, 0xA9, a, 0xE9, m);
        cpu.A.Should().Be(result);
        (cpu.P & (Mos6502.Carry | Mos6502.Zero | Mos6502.Overflow | Mos6502.Negative)).Should().Be(flags);
    }

    [Theory]
    [InlineData(0x10, 0x10, Mos6502.Carry | Mos6502.Zero)]
    [InlineData(0x10, 0x20, Mos6502.Negative)]
    [InlineData(0x20, 0x10, Mos6502.Carry)]
    public void Cmp_Flags(byte a, byte m, int flags)
    {
        Mos6502 cpu = Run(0xA9, a, 0xC9, m);
        (cpu.P & (Mos6502.Carry | Mos6502.Zero | Mos6502.Negative)).Should().Be(flags);
        cpu.A.Should().Be(a);
    }

    [Fact]
    public void Shifts_And_Rotates()
    {
        Mos6502 cpu = Run(0x38, 0xA9, 0x81, 0x2A); // SEC; LDA #$81; ROL A
        cpu.A.Should().Be(0x03);
        cpu.Flag(Mos6502.Carry).Should().BeTrue();
        cpu = Run(0x38, 0xA9, 0x01, 0x6A); // ROR A
        cpu.A.Should().Be(0x80);
        cpu.Flag(Mos6502.Carry).Should().BeTrue();
        cpu.Flag(Mos6502.Negative).Should().BeTrue();
        cpu = Run(0xA9, 0xC0, 0x0A); // ASL A
        cpu.A.Should().Be(0x80);
        cpu.Flag(Mos6502.Carry).Should().BeTrue();
        cpu = Run(0xA9, 0x03, 0x4A); // LSR A
        cpu.A.Should().Be(0x01);
        cpu.Flag(Mos6502.Carry).Should().BeTrue();
    }

    [Fact]
    public void Memory_Modes_And_IncDec()
    {
        // LDX #2; LDA #$55; STA $10,X; INC $12; LDY #$FF; INY  -> ($12)=$56, Y=0, Z
        Mos6502 cpu = Run(0xA2, 0x02, 0xA9, 0x55, 0x95, 0x10, 0xE6, 0x12, 0xA0, 0xFF, 0xC8);
        cpu.Memory[0x12].Should().Be(0x56);
        cpu.Y.Should().Be(0);
        cpu.Flag(Mos6502.Zero).Should().BeTrue();
    }

    [Fact]
    public void Indirect_Indexed_Addressing()
    {
        var cpu = new Mos6502 { Pc = 0x1000 };
        cpu.Memory[0x20] = 0x00;
        cpu.Memory[0x21] = 0x30;
        cpu.Memory[0x3005] = 0x99;
        new byte[] { 0xA0, 0x05, 0xB1, 0x20, 0x02 }.CopyTo(cpu.Memory, 0x1000);
        while (!cpu.Halted)
        {
            cpu.Step();
        }

        cpu.A.Should().Be(0x99);
    }

    [Fact]
    public void Stack_Jsr_Rts_And_Branch()
    {
        // JSR $1006; LDX #7; KIL; (1006) LDA #1; RTS   -> not reaching? layout: 1000 JSR, 1003 LDX #7, 1005 KIL, 1006 LDA, 1008 RTS
        Mos6502 cpu = Run(0x20, 0x06, 0x10, 0xA2, 0x07, 0x02, 0xA9, 0x01, 0x60);
        cpu.A.Should().Be(1);
        cpu.X.Should().Be(7);
        cpu.Sp.Should().Be(0xFD);
        cpu = Run(0xA9, 0x00, 0xF0, 0x02, 0xA9, 0x09, 0xA2, 0x03); // BEQ skips LDA #9
        cpu.A.Should().Be(0);
        cpu.X.Should().Be(3);
    }

    [Fact]
    public void Branch_To_Self_Halts()
    {
        Mos6502 cpu = Run(0x4C, 0x00, 0x10); // JMP $1000
        cpu.Halted.Should().BeTrue();
    }

    [Theory]
    [InlineData(0xA7)] // LAX (illegal)
    [InlineData(0x00)] // BRK
    public void Unsupported_Opcodes_Throw(byte opcode)
    {
        var cpu = new Mos6502 { Pc = 0x1000 };
        cpu.Memory[0x1000] = opcode;
        Action act = cpu.Step;
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Runs_Handwritten_Spike_Through_The_Assembler_And_Loader()
    {
        // Ręczny odpowiednik `int main(){return 40+2;}`: wynik w cc_ret/cc_ret_h, potem pętla stopu.
        const string Source = """
            .org $1000
            start:  clc
                    lda #40
                    adc #2
                    sta cc_ret
                    lda #0
                    sta cc_ret_h
            halt:   jmp halt
            .org $0200
            cc_ret: .res 1
            cc_ret_h: .res 1
            """;
        AssemblyResult image = Repo.Assemble("6502", Source);
        ICpuRunner runner = Runners.Create("6502");
        runner.Load(image.Origin, image.Image);
        runner.Start(0x1000);
        for (int i = 0; i < 100 && !runner.Halted; i++)
        {
            runner.Step();
        }

        runner.Halted.Should().BeTrue();
        (runner.Read((int)image.Symbols["cc_ret"]) | (runner.Read((int)image.Symbols["cc_ret_h"]) << 8)).Should().Be(42);
    }
}
