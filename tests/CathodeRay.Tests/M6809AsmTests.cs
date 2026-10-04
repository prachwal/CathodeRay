using CathodeRay.Assembler;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class M6809AsmTests
{
    [Fact]
    public void Addressing_Postbytes_And_Register_Postbytes_Match_Lwasm()
    {
        const string source = """
            NOP
            LDA #$12
            LDA $12
            LDA $1234
            LDA ,X
            LDA -16,X
            LDA -17,X
            LDA 16,X
            LDA 32767,X
            LDA [,Y]
            LDA 1,Y
            LDA A,U
            LDA ,X+
            LDA ,--S
            LDA [$1234]
            TFR A,B
            EXG D,X
            PSHS CC,A,B,X,Y,U,PC
            LBNE end
            end: SYNC
            """;
        byte[] expected =
        [
            0x12,
            0x86, 0x12,
            0x96, 0x12,
            0xB6, 0x12, 0x34,
            0xA6, 0x84,
            0xA6, 0x10,
            0xA6, 0x88, 0xEF,
            0xA6, 0x88, 0x10,
            0xA6, 0x89, 0x7F, 0xFF,
            0xA6, 0xB4,
            0xA6, 0x21,
            0xA6, 0xC6,
            0xA6, 0x80,
            0xA6, 0xE3,
            0xA6, 0x9F, 0x12, 0x34,
            0x1F, 0x89,
            0x1E, 0x01,
            0x34, 0xF7,
            0x10, 0x26, 0x00, 0x00,
            0x13,
        ];

        Asm(source).Should().Equal(expected);
    }

    [Theory]
    [InlineData("LDA -128,X", "A68880")]
    [InlineData("LDA 127,X", "A6887F")]
    [InlineData("LDA -129,X", "A689FF7F")]
    [InlineData("LDA 128,X", "A6890080")]
    [InlineData("LDA -16,S", "A670")]
    [InlineData("LDA 15,U", "A64F")]
    public void Selects_Indexed_Offset_Width(string instruction, string expected) =>
        Asm(instruction).Should().Equal(Convert.FromHexString(expected));

    [Theory]
    [InlineData("LDA -17,PCR", "A68CEC")]
    [InlineData("LDA 128,PCR", "A68C7D")]
    [InlineData("LDA 200,PCR", "A68D00C4")]
    public void Selects_Pc_Relative_Width(string instruction, string expected) =>
        Asm(instruction).Should().Equal(Convert.FromHexString(expected));

    [Fact]
    public void Complete_Opcode_Golden_Matches_Lwasm()
    {
        string directory = Repo.Path("tests", "CathodeRay.Tests", "Asm", "6809");
        string source = File.ReadAllText(Path.Combine(directory, "opcodes.s"));

        Asm(source).Should().Equal(File.ReadAllBytes(Path.Combine(directory, "opcodes.bin")));
    }

    [Fact]
    public void Program_Golden_Matches_Lwasm()
    {
        string directory = Repo.Path("tests", "CathodeRay.Tests", "Asm", "6809");
        string source = File.ReadAllText(Path.Combine(directory, "program.s"));

        Asm(source).Should().Equal(File.ReadAllBytes(Path.Combine(directory, "program.bin")));
    }

    [Fact]
    public void Rejected_Operand_Golden_Is_Rejected()
    {
        string rejected = File.ReadAllText(Repo.Path("tests", "CathodeRay.Tests", "Asm", "6809", "rejected.txt"));
        foreach (string line in rejected.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            FluentActions.Invoking(() => Asm(line)).Should().Throw<CathodeRay.Assembler.AssemblerException>(line);
        }
    }

    private static byte[] Asm(string source) => Repo.Assemble("6809", source).Image;
}
