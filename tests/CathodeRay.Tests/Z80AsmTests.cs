using CathodeRay.Assembler;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Z80 w składni Zilog: przypadki brzegowe mechanizmów rdzenia (przesunięcie, stałe, układ, AF').
/// Pełne pokrycie opcode'ów: <see cref="AsmGoldenTests"/> z wzorcami z z80asm.</summary>
public sealed class Z80AsmTests
{
    private static byte[] Asm(string source, string cpu = "z80") => Repo.Assemble(cpu, source).Image;

    [Theory]
    [InlineData("LD A,(IX+5)", new byte[] { 0xDD, 0x7E, 0x05 })]
    [InlineData("LD A,(IX-1)", new byte[] { 0xDD, 0x7E, 0xFF })]
    [InlineData("LD A,(IX)", new byte[] { 0xDD, 0x7E, 0x00 })]
    [InlineData("LD A,(IY+127)", new byte[] { 0xFD, 0x7E, 0x7F })]
    [InlineData("LD (IX+5),12H", new byte[] { 0xDD, 0x36, 0x05, 0x12 })]
    [InlineData("LD (IX),12H", new byte[] { 0xDD, 0x36, 0x00, 0x12 })]
    [InlineData("LD H,(IX+2)", new byte[] { 0xDD, 0x66, 0x02 })]
    [InlineData("JP (IX)", new byte[] { 0xDD, 0xE9 })]
    public void Index_Registers_With_Signed_Displacement(string source, byte[] expected)
    {
        Asm(source).Should().Equal(expected);
    }

    [Theory]
    [InlineData("RLC (IX+2)", new byte[] { 0xDD, 0xCB, 0x02, 0x06 })]
    [InlineData("RLC (IX)", new byte[] { 0xDD, 0xCB, 0x00, 0x06 })]
    [InlineData("SET 1,(IY-3)", new byte[] { 0xFD, 0xCB, 0xFD, 0xCE })]
    public void Ddcb_Puts_Displacement_Before_Last_Opcode_Byte(string source, byte[] expected)
    {
        Asm(source).Should().Equal(expected);
    }

    [Theory]
    [InlineData("BIT 7,(HL)", new byte[] { 0xCB, 0x7E })]
    [InlineData("BIT 3+4,(HL)", new byte[] { 0xCB, 0x7E })]
    [InlineData("IM 2", new byte[] { 0xED, 0x5E })]
    [InlineData("RST 38H", new byte[] { 0xFF })]
    [InlineData("RST 56", new byte[] { 0xFF })]
    [InlineData("RST $38", new byte[] { 0xFF })]
    public void Constants_Select_Opcode(string source, byte[] expected)
    {
        Asm(source).Should().Equal(expected);
    }

    [Theory]
    [InlineData("JR $", new byte[] { 0x18, 0xFE })]
    [InlineData("JR NZ,$+2", new byte[] { 0x20, 0x00 })]
    [InlineData("DJNZ $-126", new byte[] { 0x10, 0x80 })]
    public void Relative_Jumps(string source, byte[] expected)
    {
        Asm(source).Should().Equal(expected);
    }

    [Theory]
    [InlineData("LD A,(1234H)", new byte[] { 0x3A, 0x34, 0x12 })]
    [InlineData("LD A,(2+3)*4", new byte[] { 0x3E, 0x14 })]
    [InlineData("LD HL,(1234H)", new byte[] { 0x2A, 0x34, 0x12 })]
    [InlineData("LD HL,1234H", new byte[] { 0x21, 0x34, 0x12 })]
    [InlineData("OUT (C),B", new byte[] { 0xED, 0x41 })]
    [InlineData("OUT (0FEH),A", new byte[] { 0xD3, 0xFE })]
    public void Parentheses_Mean_Memory_Only_Around_Whole_Operand(string source, byte[] expected)
    {
        Asm(source).Should().Equal(expected);
    }

    [Fact]
    public void Alternate_Register_Apostrophe_Is_Not_A_String()
    {
        Asm("  EX AF,AF'   ; komentarz z ' apostrofem").Should().Equal(0x08);
    }

    [Theory]
    [InlineData("LD A,IXH", new byte[] { 0xDD, 0x7C })]
    [InlineData("SLL B", new byte[] { 0xCB, 0x30 })]
    [InlineData("OUT (C),0", new byte[] { 0xED, 0x71 })]
    [InlineData("RLC (IX+1),B", new byte[] { 0xDD, 0xCB, 0x01, 0x00 })]
    public void Undocumented_Instructions_Only_In_Z80u(string source, byte[] expected)
    {
        FluentActions.Invoking(() => Asm(source)).Should().Throw<AssemblerException>();
        Asm(source, "z80u").Should().Equal(expected);
    }

    [Theory]
    [InlineData("BIT 8,(HL)", "invalid operand '8' for BIT (allowed constants: 0, 1, 2, 3, 4, 5, 6, 7)")]
    [InlineData("IM 3", "allowed constants: 0, 1, 2")]
    [InlineData("LD A,(IX+128)", "displacement 128 out of range -128..127")]
    [InlineData("JR $+130", "branch target out of range (128 bytes")]
    [InlineData("BIT bitno,A\nbitno EQU 1", "'bitno' must be known at this point (it selects the BIT opcode)")]
    [InlineData("b EQU 1\nBIT b,A", "addressing mode B,A is not available for BIT")]
    public void Reports_Z80_Errors(string source, string message)
    {
        FluentActions.Invoking(() => Asm(source)).Should().Throw<AssemblerException>().WithMessage($"*{message}*");
    }
}
