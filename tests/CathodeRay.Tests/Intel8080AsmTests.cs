using CathodeRay.Assembler;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Dialekt Intel ASM80 dla 8080: bajty policzone ręcznie z tabeli opcode'ów (z80asm używa innych dyrektyw i RST).</summary>
public sealed class Intel8080AsmTests
{
    private static byte[] Asm(string source) => Repo.Assemble("8080", source).Image;

    [Theory]
    [InlineData("RST 0", 0xC7)]
    [InlineData("RST 1", 0xCF)]
    [InlineData("RST 7", 0xFF)]
    [InlineData("RST 3+4", 0xFF)]
    [InlineData("V EQU 2\n RST V", 0xD7)]
    public void Rst_Takes_Vector_Number(string source, int opcode)
    {
        Asm(source).Should().Equal((byte)opcode);
    }

    [Theory]
    [InlineData("JP 1234H", 0xF2)]
    [InlineData("CP 1234H", 0xF4)]
    [InlineData("JM 1234H", 0xFA)]
    [InlineData("CM 1234H", 0xFC)]
    public void Intel_Conditional_Mnemonics_Are_Not_Zilog(string source, int opcode)
    {
        Asm(source).Should().Equal((byte)opcode, 0x34, 0x12);
    }

    [Fact]
    public void Assembles_Intel_Program()
    {
        byte[] expected =
        [
            0x3E, 0x05,             // MVI A,COUNT
            0x21, 0x0C, 0x01,       // LXI H,MSG
            0x06, 0x01,             // MVI B,HIGH MSG
            0x0E, 0x0C,             // MVI C,LOW(MSG)
            0xC3, 0x13, 0x01,       // JMP DONE
            0x48, 0x49, 0x00,       // MSG: DB 'HI',0
            0x34, 0x12,             // DW 1234H
            0x00, 0x00,             // DS 2
            0x76,                   // DONE: HLT
        ];

        Asm("""
            COUNT   EQU 5
                    ORG 100H
                    MVI A,COUNT
                    LXI H,MSG
                    MVI B,HIGH MSG
                    MVI C,LOW(MSG)
                    JMP DONE
            MSG:    DB 'HI',0
                    DW 1234H
                    DS 2
            DONE:   HLT
                    END
                    NOP             ; po END: pomijane
            """).Should().Equal(expected);
    }

    [Theory]
    [InlineData("MVI A,0FFH", 0xFF)]
    [InlineData("MVI A,11110000B", 0xF0)]
    [InlineData("MVI A,17Q", 0x0F)]
    [InlineData("MVI A,17O", 0x0F)]
    [InlineData("MVI A,99D", 99)]
    [InlineData("MVI A,0F0H AND 3CH", 0x30)]
    [InlineData("MVI A,1 SHL 4 OR 1", 0x11)]
    [InlineData("MVI A,NOT 0 AND 0FH", 0x0F)]
    [InlineData("MVI A,10 MOD 3", 1)]
    [InlineData("MVI A,','", 0x2C)]
    [InlineData("MVI A,';'  ; komentarz", 0x3B)]
    public void Evaluates_Intel_Numbers_And_Word_Operators(string source, int value)
    {
        Asm(source).Should().Equal(0x3E, (byte)value);
    }

    [Fact]
    public void Dollar_Is_Program_Counter()
    {
        Asm("  ORG 200H\n  JMP $").Should().Equal(0xC3, 0x00, 0x02);
    }

    [Theory]
    [InlineData("MVI A,FFH", "undefined symbol 'FFH'")]
    [InlineData("MOV A,Q", "addressing mode A,{b} is not available for MOV")]
    [InlineData("LDA B", "addressing mode B is not available for LDA")]
    [InlineData("X EQU 1\nX EQU 2", "duplicate symbol 'X'")]
    [InlineData("RST 8", "invalid operand '8' for RST (allowed constants: 0, 1, 2, 3, 4, 5, 6, 7)")]
    [InlineData("RST V\nV EQU 1", "'V' must be known at this point (it selects the RST opcode)")]
    public void Reports_Intel_Errors(string source, string message)
    {
        FluentActions.Invoking(() => Asm(source)).Should().Throw<AssemblerException>().WithMessage($"*{message}*");
    }
}
