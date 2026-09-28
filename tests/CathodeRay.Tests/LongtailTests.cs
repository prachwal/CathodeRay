using CathodeRay.Assembler;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class LongtailTests
{
    private static byte[] Asm(string source, string cpu = "stub", string? syntax = null) =>
        Repo.Assemble(cpu, source, syntax).Image;

    [Fact]
    public void Out_And_Warning_Collect_Without_Bytes()
    {
        var result = Repo.Assemble("stub", ".out \"hi\"\n.warning \"careful\"\nHLT");

        result.Image.Should().Equal(0xFF);
        result.Messages.Should().Equal(
            new AsmMessage(null, 1, "hi", false),
            new AsmMessage(null, 2, "careful", true));
    }

    [Fact]
    public void Messages_Carry_File_And_Appear_Once()
    {
        var result = Repo.Assemble("stub", ".out \"once\"\nHLT");

        result.Messages.Should().ContainSingle();
    }

    [Fact]
    public void Assert_Passes_Silently_And_Fails_With_Message()
    {
        Asm(".assert 1 + 1 = 2, error\nHLT").Should().Equal(0xFF);
        FluentActions.Invoking(() => Asm(".assert 1 + 1 = 3, error, \"math broke\"\nHLT"))
            .Should().Throw<AssemblerException>().WithMessage("*math broke*");
        FluentActions.Invoking(() => Asm(".assert 1 + 1 = 3, warning\nHLT"))
            .Should().NotThrow();
        Repo.Assemble("stub", ".assert 1 + 1 = 3, warning\nHLT").Messages
            .Should().ContainSingle().Which.Text.Should().Contain("assertion failed");
    }

    [Fact]
    public void Error_Always_Fails()
    {
        FluentActions.Invoking(() => Asm(".error \"boom\"\nHLT"))
            .Should().Throw<AssemblerException>().WithMessage("*user error: boom*");
    }

    [Theory]
    [InlineData(".out\n", ".out needs a message")]
    [InlineData(".out 42\n", ".out needs a quoted message")]
    [InlineData(".assert 1\n", ".assert needs condition")]
    [InlineData(".assert 1, maybe\n", "must be warning or error")]
    [InlineData(".assert later = 1, error\nlater = 1\nHLT", "must be known")]
    public void Malformed_Message_Directives_Are_Rejected(string source, string message)
    {
        FluentActions.Invoking(() => Asm(source))
            .Should().Throw<AssemblerException>().WithMessage($"*{message}*");
    }

    [Fact]
    public void Define_Substitutes_Text()
    {
        Asm(".define COUNT 3\nLDI COUNT\nHLT")
            .Should().Equal(0x01, 0x03, 0xFF);
    }

    [Fact]
    public void Define_With_Params_Substitutes_Args()
    {
        Asm(".define ADD(a, b) a + b\nLDI ADD(2, 3)\nHLT")
            .Should().Equal(0x01, 0x05, 0xFF);
    }

    [Fact]
    public void Define_Ignores_Quoted_Strings()
    {
        Asm(".define V 9\n.byte \"V\"\nrts\n", "6502")
            .Should().Equal((byte)'V', 0x60);
    }

    [Fact]
    public void Define_Chains_Resolve_And_Recursion_Fails()
    {
        Asm(".define A B\n.define B 7\nLDI A\nHLT")
            .Should().Equal(0x01, 0x07, 0xFF);
        FluentActions.Invoking(() => Asm(".define A B\n.define B A\nLDI A\nHLT"))
            .Should().Throw<AssemblerException>().WithMessage("*recursive '.define'*");
    }

    [Theory]
    [InlineData(".define\n", "needs a name")]
    [InlineData(".define 1x 2\n", "invalid macro name")]
    [InlineData(".define F(a\n", "')' expected")]
    [InlineData(".define F(a, a) 1\n", "duplicate macro parameter")]
    [InlineData(".define F(a) 1\nLDI F(1, 2)\nHLT", "takes 1 parameters, got 2")]
    [InlineData("lbl: .define X 1\n", "label on '.define'")]
    [InlineData(".define X 1\n.define X 2\n", "already defined")]
    public void Malformed_Defines_Are_Rejected(string source, string message)
    {
        FluentActions.Invoking(() => Asm(source))
            .Should().Throw<AssemblerException>().WithMessage($"*{message}*");
    }

    [Fact]
    public void Ifblank_Tests_Macro_Args()
    {
        Asm(".macro m a, b\n.ifblank b\nLDI a\n.else\nLDI b\n.endif\n.endmacro\nm 1\nm 2, 3\nHLT")
            .Should().Equal(0x01, 0x01, 0x01, 0x03, 0xFF);
    }

    [Fact]
    public void Ifnblank_Tests_Macro_Args()
    {
        Asm(".macro m a\n.ifnblank a\nLDI a\n.endif\n.endmacro\nm 5\nm\nHLT")
            .Should().Equal(0x01, 0x05, 0xFF);
    }

    [Fact]
    public void Paramcount_Counts_Args()
    {
        Asm(".macro m a, b, c\nLDI .paramcount\n.endmacro\nm 1\nm 1, 2\nm 1, 2, 3\nHLT")
            .Should().Equal(0x01, 0x01, 0x01, 0x02, 0x01, 0x03, 0xFF);
    }

    public static TheoryData<string> GoldenNames()
    {
        var data = new TheoryData<string>();
        foreach (string dir in Directory.GetDirectories(Repo.Path("tests", "CathodeRay.Tests", "Longtail")).Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(dir));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(GoldenNames))]
    public void Golden_Matches_Reference(string cpu)
    {
        string main = Repo.Path("tests", "CathodeRay.Tests", "Longtail", cpu, "main.s");
        AssemblerTarget target = AssemblerTargets.Find(cpu)!;
        var assembler = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax);
        static string? Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

        assembler.Assemble(File.ReadAllText(main), main, Read).Image.Should().Equal(
            File.ReadAllBytes(Repo.Path("tests", "CathodeRay.Tests", "Longtail", cpu, "expected.bin")),
            $"Longtail/{cpu}/main.s ma dać te same bajty co referencja");
    }
}
