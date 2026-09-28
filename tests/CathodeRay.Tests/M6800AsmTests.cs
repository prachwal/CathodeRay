using CathodeRay.Assembler;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Dialekt Motorola dla 6800: bajty policzone ręcznie z tabeli M6800.
/// as6800 ucina natychmiastowe poza zakresem (ldaa #$1234 → $34) i robi więcej
/// przebiegów (forward-equ → direct) — my odrzucamy nadmiar i bierzemy
/// najdłuższą formę przy odwołaniu w przód (jak ca65).</summary>
public sealed class M6800AsmTests
{
    private static byte[] Asm(string source) => Repo.Assemble("6800", source).Image;

    public static TheoryData<string, byte[]> ModeCases()
    {
        return new TheoryData<string, byte[]>
        {
            { "LDAA #$12", [0x86, 0x12] },
            { "LDAA $12", [0x96, 0x12] },
            { "LDAA $1234", [0xB6, 0x12, 0x34] },
            { "LDAA $12,X", [0xA6, 0x12] },
            { "LDX #$1234", [0xCE, 0x12, 0x34] },
            { "CPX #$1234", [0x8C, 0x12, 0x34] },
            { "STX $1234", [0xFF, 0x12, 0x34] },
            { "NEG $12,X", [0x60, 0x12] },
            { "NEG $1234", [0x70, 0x12, 0x34] },
            { "BRA *+5", [0x20, 0x03] },
        };
    }

    [Theory]
    [MemberData(nameof(ModeCases))]
    public void Modes_Encode_As_M6800(string source, byte[] expected) =>
        Asm("org $0600\n" + source).Should().Equal(expected);

    [Fact]
    public void Word_Is_Big_Endian()
    {
        Asm("org $0600\nLDX #$1234\nFDB $1234\n").Should().Equal(0xCE, 0x12, 0x34, 0x12, 0x34);
    }

    [Fact]
    public void Direct_Wins_Over_Extended_When_Value_Fits()
    {
        Asm("org $0600\nV EQU $12\nLDAA V\n").Should().Equal(0x96, 0x12);
    }

    [Fact]
    public void Forward_Reference_Takes_Longest_Form()
    {
        Asm("org $0600\nLDAA later\nlater EQU $12\n").Should().Equal(0xB6, 0x00, 0x12);
    }

    [Theory]
    [InlineData("LDAA #$1234")]
    [InlineData("LDAA $12345")]
    public void Out_Of_Range_Is_Rejected_Where_Reference_Truncates(string source)
    {
        FluentActions.Invoking(() => Asm("org $0600\n" + source))
            .Should().Throw<AssemblerException>();
    }

    [Fact]
    public void Assembles_Motorola_Program()
    {
        byte[] expected =
        [
            0xCE, 0x00, 0x08,       // LDX #count
            0x86, 0x20,             // LDAA #<message
            0x97, 0x10,             // STAA ptr
            0x86, 0x06,             // LDAA #>message
            0x97, 0x11,             // STAA ptr+1
            0xCE, 0x00, 0x00,       // LDX #0
            0xA6, 0x00,             // loop: LDAA 0,X
            0x27, 0x05,             // BEQ done
            0xA7, 0x01,             // STAA 1,X
            0x08,                   // INX
            0x20, 0xF7,             // BRA loop
            0x7E, 0x06, 0x1F,       // done: JMP finish
            0x96, 0x20,             // LDAA later
            0x09,                   // DEX
            0x2A, 0xE1,             // BPL start
            0x39,                   // finish: RTS
        ];

        Asm(File.ReadAllText(Repo.Path("tests", "CathodeRay.Tests", "Asm", "6800", "program.s")))
            .Should().StartWith(expected);
    }
}
