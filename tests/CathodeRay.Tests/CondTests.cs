using CathodeRay.Assembler;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class CondTests
{
    private static byte[] Asm(string source, string cpu = "stub", string? syntax = null) =>
        Repo.Assemble(cpu, source, syntax).Image;

    public static TheoryData<string, byte[]> BranchCases()
    {
        return new TheoryData<string, byte[]>
        {
            { ".if 1\nLDI 7\n.endif\nHLT", [0x01, 0x07, 0xFF] },
            { ".if 0\nLDI 7\n.endif\nHLT", [0xFF] },
            { ".if 0\nLDI 7\n.else\nLDI 9\n.endif\nHLT", [0x01, 0x09, 0xFF] },
            { ".if 0\nLDI 1\n.elseif 1\nLDI 2\n.else\nLDI 3\n.endif\nHLT", [0x01, 0x02, 0xFF] },
            { ".if 1\nLDI 1\n.elseif 1\nLDI 2\n.endif\nHLT", [0x01, 0x01, 0xFF] },
            { "V = 2\n.if V == 2\nLDI 1\n.endif\n.if V != 2\nLDI 2\n.endif\nHLT", [0x01, 0x01, 0xFF] },
            { ".if 1\n.if 0\nLDI 1\n.else\nLDI 2\n.endif\n.else\nLDI 3\n.endif\nHLT", [0x01, 0x02, 0xFF] },
            { ".if 0\n.if 1\nLDI 1\n.endif\nLDI 2\n.endif\nHLT", [0xFF] },
        };
    }

    [Theory]
    [MemberData(nameof(BranchCases))]
    public void Branches_Select_Bytes(string source, byte[] expected) =>
        Asm(source).Should().Equal(expected);

    [Fact]
    public void Relational_Operators_Select_Bytes()
    {
        const string Source =
            "V = 3\n.if V > 2\nLDI 1\n.endif\n.if V < 2\nLDI 2\n.endif\n.if V >= 3\nLDI 3\n.endif\n.if V <= 2\nLDI 4\n.endif\nHLT";

        Asm(Source).Should().Equal(0x01, 0x01, 0x01, 0x03, 0xFF);
    }

    [Fact]
    public void Inactive_Branch_Defines_No_Symbols()
    {
        byte[] image = Asm("LDI on\nHLT\non: .byte 0\noff: .byte 0\n.if 1\nSTA on\n.else\nSTA off\n.endif\n");

        image.Should().Equal(0x01, 0x03, 0xFF, 0x00, 0x00, 0x05, 0x03, 0x00);
    }

    [Fact]
    public void Unknown_Condition_In_First_Pass_Is_Rejected()
    {
        FluentActions.Invoking(() => Asm(".if later == 1\nLDI 1\n.endif\nlater = 1\nHLT"))
            .Should().Throw<AssemblerException>().WithMessage("*must be known*");
    }

    [Theory]
    [InlineData(".else\nHLT", "'.else' without '.if'")]
    [InlineData(".endif\nHLT", "'.endif' without '.if'")]
    [InlineData(".elseif 1\nHLT", "'.elseif' without '.if'")]
    [InlineData(".if 1\nHLT", "unterminated .if")]
    [InlineData(".if 1\n.else\n.else\nHLT", "multiple '.else'")]
    [InlineData(".if 1\n.else\n.elseif 1\nHLT", "'.elseif' after '.else'")]
    [InlineData(".if\nHLT", "needs a condition")]
    public void Malformed_Blocks_Are_Rejected(string source, string message)
    {
        FluentActions.Invoking(() => Asm(source))
            .Should().Throw<AssemblerException>().WithMessage($"*{message}*");
    }

    [Fact]
    public void Label_On_Conditional_Binds_Current_Address()
    {
        var result = Repo.Assemble("stub", "here: .if 1\nLDI 7\n.endif\nHLT");

        result.Symbols["here"].Should().Be(0);
        result.Image.Should().Equal(0x01, 0x07, 0xFF);
    }

    [Fact]
    public void Intel_Uses_If_Else_Endif()
    {
        Asm("V EQU 0\nORG 100H\nIF V == 0\nMVI A,1\nELSE\nMVI A,2\nENDIF\nHLT", "8080", "intel")
            .Should().Equal(0x3E, 0x01, 0x76);
    }

    [Fact]
    public void Zilog_Uses_Elif()
    {
        Asm("V EQU 2\nORG 8000H\nIF V == 1\nLD A,1\nELIF V == 2\nLD A,2\nELSE\nLD A,3\nENDIF", "z80")
            .Should().Equal(0x3E, 0x02);
    }

    [Fact]
    public void Ca65_Uses_Dotted_Directives()
    {
        Asm("v = 0\n.org $0600\n.if v <> 0\nlda #1\n.else\nlda #2\n.endif\nrts", "6502")
            .Should().Equal(0xA9, 0x02, 0x60);
    }

    [Fact]
    public void Mos_Uses_Dotted_Uppercase_Directives()
    {
        Asm("V = 1\n*= $0600\n.IF V = 1\nLDA #7\n.ELSE\nLDA #9\n.ENDIF\nRTS", "6502", "mos")
            .Should().Equal(0xA9, 0x07, 0x60);
    }

    [Fact]
    public void Intel_Has_No_Elif()
    {
        FluentActions.Invoking(() => Asm("ELIF 1\n", "8080", "intel"))
            .Should().Throw<AssemblerException>().WithMessage("*unknown mnemonic or directive 'ELIF'*");
    }

    [Fact]
    public void Skipped_Lines_Stay_In_Listing_Without_Bytes()
    {
        var result = Repo.Assemble("stub", ".if 0\nLDI 7\n.endif\nHLT");

        result.Listing.Should().HaveCount(4);
        result.Listing[1].Bytes.Should().BeEmpty();
        result.Listing[1].Source.Should().Contain("LDI 7");
    }

    public static TheoryData<string> GoldenNames()
    {
        var data = new TheoryData<string>();
        foreach (string dir in Directory.GetDirectories(Repo.Path("tests", "CathodeRay.Tests", "Cond")).Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(dir));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(GoldenNames))]
    public void Golden_Matches_Reference(string cpu)
    {
        string main = Repo.Path("tests", "CathodeRay.Tests", "Cond", cpu, "main.s");
        AssemblerTarget target = AssemblerTargets.Find(cpu)!;
        var assembler = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax);
        static string? Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

        assembler.Assemble(File.ReadAllText(main), main, Read).Image.Should().Equal(
            File.ReadAllBytes(Repo.Path("tests", "CathodeRay.Tests", "Cond", cpu, "expected.bin")),
            $"Cond/{cpu}/main.s ma dać te same bajty co referencja");
    }
}
