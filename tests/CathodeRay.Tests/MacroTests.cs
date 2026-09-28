using CathodeRay.Assembler;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class MacroTests
{
    private static byte[] Asm(string source, string cpu = "stub", string? syntax = null) =>
        Repo.Assemble(cpu, source, syntax).Image;

    [Fact]
    public void Definition_Emits_Nothing_Call_Expands_Body()
    {
        Asm(".macro add2 val\nLDI val\nADD val\n.endmacro\nLDI 1\nadd2 5\nadd2 7\nHLT")
            .Should().Equal(0x01, 0x01, 0x01, 0x05, 0x02, 0x05, 0x01, 0x07, 0x02, 0x07, 0xFF);
    }

    [Fact]
    public void Parameterless_Macro_Works()
    {
        Asm(".macro nop2\nNOP\nNOP\n.endmacro\nnop2\nHLT")
            .Should().Equal(0x00, 0x00, 0xFF);
    }

    [Fact]
    public void Missing_Argument_Expands_Empty_Like_Ca65()
    {
        Asm(".macro m v\nLDI v+1\n.endmacro\nm 5\nm\nHLT")
            .Should().Equal(0x01, 0x06, 0x01, 0x01, 0xFF);
    }

    [Fact]
    public void Too_Many_Arguments_Are_Rejected()
    {
        FluentActions.Invoking(() => Asm(".macro pair a, b\nLDI a\nLDI b\n.endmacro\npair 1, , 2\nHLT"))
            .Should().Throw<AssemblerException>().WithMessage("*too many macro parameters*");
    }

    [Fact]
    public void Local_Is_Unique_Per_Expansion()
    {
        byte[] image = Asm(
            "aa: LDI 0\nSTA done\n.macro spin n\n.local loop\nloop: INC\nCPX n\nBNE loop\n.endmacro\nspin 2\nspin 3\ndone: HLT\n");

        image.Should().Equal(0x01, 0x00, 0x05, 0x11, 0x00, 0x03, 0x0A, 0x02, 0x0B, 0x05, 0x00, 0x03, 0x0A, 0x03, 0x0B, 0x0B, 0x00, 0xFF);
        var result = Repo.Assemble("stub", "aa: LDI 0\nSTA done\n.macro spin n\n.local loop\nloop: INC\nCPX n\nBNE loop\n.endmacro\nspin 2\nspin 3\ndone: HLT\n");
        result.Symbols.Should().ContainKey("__M1_loop").And.ContainKey("__M2_loop");
    }

    [Fact]
    public void Label_Without_Local_Duplicates_Like_References()
    {
        FluentActions.Invoking(() => Asm(".macro twice\n lab: NOP\n.endmacro\ntwice\ntwice\n"))
            .Should().Throw<AssemblerException>().WithMessage("*duplicate symbol 'lab'*");
    }

    [Fact]
    public void Call_Label_Binds_Expansion_Start()
    {
        var result = Repo.Assemble("stub", ".macro two\nLDI 1\nLDI 2\n.endmacro\nsub: two\nHLT");

        result.Symbols["sub"].Should().Be(0);
        result.Image.Should().Equal(0x01, 0x01, 0x01, 0x02, 0xFF);
    }

    [Fact]
    public void Macro_Beats_Mnemonic_With_Same_Name()
    {
        Asm(".macro INC\nLDI 9\n.endmacro\nINC\nHLT")
            .Should().Equal(0x01, 0x09, 0xFF);
    }

    [Fact]
    public void Self_Recursion_Stops_At_Limit_With_Chain()
    {
        FluentActions.Invoking(() => Asm(".macro self\nself\n.endmacro\nself\n"))
            .Should().Throw<AssemblerException>().WithMessage("*macro recursion too deep*self -> self*");
    }

    [Theory]
    [InlineData(".macro m\nNOP\n", "unterminated '.macro'")]
    [InlineData(".endmacro\n", "'.endmacro' without '.macro'")]
    [InlineData(".local x\n", "'.local' outside macro definition")]
    [InlineData(".macro m\nNOP\n.endmacro\n.macro m\nNOP\n.endmacro\n", "already defined")]
    [InlineData(".macro m\n.macro n\nNOP\n.endmacro\n.endmacro\n", "nested '.macro'")]
    [InlineData(".macro\nNOP\n.endmacro\n", "needs a name")]
    [InlineData(".macro 1x\nNOP\n.endmacro\n", "invalid macro name")]
    [InlineData(".macro m a, a\nNOP\n.endmacro\n", "duplicate macro parameter")]
    [InlineData(".macro m a\nNOP\n.endmacro\nm 1, 2\n", "too many macro parameters")]
    [InlineData(".macro m a\n.local a\nNOP\n.endmacro\nm 1\n", "both a parameter and a .local")]
    [InlineData("lbl: .macro m\nNOP\n.endmacro\n", "label on '.macro'")]
    public void Malformed_Macros_Are_Rejected(string source, string message)
    {
        FluentActions.Invoking(() => Asm(source))
            .Should().Throw<AssemblerException>().WithMessage($"*{message}*");
    }

    [Fact]
    public void Zilog_Uses_Label_Form_And_Endm()
    {
        Asm("ADD2: MACRO val\nLD A,val\nADD A,val\nENDM\nORG 8000H\nADD2 5\nADD2 7\n", "z80")
            .Should().Equal(0x3E, 0x05, 0xC6, 0x05, 0x3E, 0x07, 0xC6, 0x07);
    }

    [Fact]
    public void Zilog_Local_Works()
    {
        Asm("DJ: MACRO n\nLOCAL again\nagain: DEC B\nJR NZ,again\nENDM\nORG 8000H\nDJ 1\nDJ 2\n", "z80")
            .Should().Equal(0x05, 0x20, 0xFD, 0x05, 0x20, 0xFD);
    }

    [Fact]
    public void Intel_Uses_Macro_Endm()
    {
        Asm("PUT: MACRO v\nMVI A,v\nENDM\nORG 100H\nPUT 9\nHLT", "8080", "intel")
            .Should().Equal(0x3E, 0x09, 0x76);
    }

    [Fact]
    public void Mos_Uses_Dotted_Macro()
    {
        Asm(".MACRO ADD2 VAL\nLDA #VAL\nCLC\nADC #VAL\n.ENDMACRO\n*= $0600\nADD2 5\nRTS", "6502", "mos")
            .Should().Equal(0xA9, 0x05, 0x18, 0x69, 0x05, 0x60);
    }

    [Fact]
    public void Expansion_Lines_Carry_Call_Location()
    {
        var result = Repo.Assemble("stub", ".macro add2 val\nLDI val\nADD val\n.endmacro\nadd2 5\nHLT");

        result.Listing[0].Source.Should().Be("LDI 5");
        result.Listing[0].Line.Should().Be(5);
    }

    [Fact]
    public void Error_Points_To_Call_And_Definition()
    {
        FluentActions.Invoking(() => Asm(".macro bad\nFOO\n.endmacro\nbad\nHLT"))
            .Should().Throw<AssemblerException>()
            .Where(e => e.Line == 4)
            .WithMessage("*unknown mnemonic*'FOO'*(in expansion of 'bad' defined at line 1)*");
    }

    [Fact]
    public void Default_Applies_When_Argument_Missing_Or_Empty()
    {
        Asm(".macro add2 val, acc=0\nLDI val\nADD acc\n.endmacro\nadd2 5\nadd2 7, 1\nadd2 8,\nHLT")
            .Should().Equal(0x01, 0x05, 0x02, 0x00, 0x01, 0x07, 0x02, 0x01, 0x01, 0x08, 0x02, 0x00, 0xFF);
    }

    [Fact]
    public void Default_May_Be_An_Expression()
    {
        Asm(".macro m v=4+1\nLDI v\n.endmacro\nm\nHLT")
            .Should().Equal(0x01, 0x05, 0xFF);
    }

    [Theory]
    [InlineData(".macro m a=, NOP\n.endmacro\n", "empty default for macro parameter 'a'")]
    [InlineData(".macro m 1x=2\nNOP\n.endmacro\n", "invalid macro parameter '1x'")]
    public void Bad_Defaults_Are_Rejected(string source, string message)
    {
        FluentActions.Invoking(() => Asm(source))
            .Should().Throw<AssemblerException>().WithMessage($"*{message}*");
    }

    public static TheoryData<string> GoldenNames()
    {
        var data = new TheoryData<string>();
        foreach (string dir in Directory.GetDirectories(Repo.Path("tests", "CathodeRay.Tests", "Macros")).Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(dir));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(GoldenNames))]
    public void Golden_Matches_Reference(string cpu)
    {
        string main = Repo.Path("tests", "CathodeRay.Tests", "Macros", cpu, "main.s");
        AssemblerTarget target = AssemblerTargets.Find(cpu)!;
        var assembler = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax);
        static string? Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

        assembler.Assemble(File.ReadAllText(main), main, Read).Image.Should().Equal(
            File.ReadAllBytes(Repo.Path("tests", "CathodeRay.Tests", "Macros", cpu, "expected.bin")),
            $"Macros/{cpu}/main.s ma dać te same bajty co referencja");
    }
}
