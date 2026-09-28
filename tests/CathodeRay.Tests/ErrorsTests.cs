using CathodeRay.Assembler;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class ErrorsTests
{
    private static AssemblerException Fail(string source, string cpu = "stub", string? syntax = null)
    {
        try
        {
            Repo.Assemble(cpu, source, syntax);
        }
        catch (AssemblerException e)
        {
            return e;
        }

        throw new Xunit.Sdk.XunitException("Expected AssemblerException, but assembly succeeded.");
    }

    [Fact]
    public void Collects_All_Errors_In_Source_Order()
    {
        AssemblerException e = Fail("LDI 1\nFOO 2\nBAR\nINC 1\nHLT");

        e.Errors.Should().HaveCount(3);
        e.Errors[0].Should().Be(new AssemblerError(null, 2, "unknown mnemonic or directive 'FOO'."));
        e.Errors[1].Should().Be(new AssemblerError(null, 3, "unknown mnemonic or directive 'BAR'."));
        e.Errors[2].Should().Be(new AssemblerError(null, 4, "INC takes no operand."));
        e.Line.Should().Be(2);
        e.Message.Should().Contain("line 2: unknown mnemonic").And.Contain("(+2 more)");
    }

    [Fact]
    public void Second_Pass_Runs_Only_When_First_Is_Clean()
    {
        AssemblerException e = Fail("LDI 256\nFOO\n");

        e.Errors.Should().HaveCount(1);
        e.Errors[0].Line.Should().Be(2);
    }

    [Fact]
    public void Second_Pass_Errors_Are_Collected()
    {
        AssemblerException e = Fail("LDI 256\nLDI 257\nHLT");

        e.Errors.Select(static err => err.Line).Should().Equal(1, 2);
    }

    [Fact]
    public void Truncates_After_Twenty_With_Note()
    {
        string source = string.Join('\n', Enumerable.Range(0, 25).Select(static i => $"LDI {i} Bad{i}")) + "\n";
        AssemblerException e = Fail(source);

        e.Errors.Should().HaveCount(21);
        e.Errors.Take(20).Should().OnlyContain(static err => err.Message.StartsWith("unexpected 'Bad"));
        e.Errors[20].Message.Should().Contain("too many errors (showing first 20)");
    }

    [Fact]
    public void Errors_Carry_Files_From_Includes()
    {
        var files = new Dictionary<string, string>
        {
            ["main.asm"] = "NOP\n.include \"bad.asm\"\nFOO\n",
            ["bad.asm"] = "BAR\n",
        };
        var full = files.ToDictionary(static kv => Path.GetFullPath(kv.Key), static kv => kv.Value);
        string entry = Path.GetFullPath("main.asm");
        AssemblerTarget target = AssemblerTargets.Find("stub")!;
        var assembler = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax);

        AssemblerException e = FluentActions.Invoking(() =>
            assembler.Assemble(files["main.asm"], entry, path => full.TryGetValue(path, out string? t) ? t : null))
            .Should().Throw<AssemblerException>().Which;

        e.Errors.Select(static err => (err.File is null ? "?" : Path.GetFileName(err.File), err.Line))
            .Should().Equal([("bad.asm", 1), ("main.asm", 3)]);
    }

    [Fact]
    public void Single_Error_Keeps_Old_Shape()
    {
        AssemblerException e = Fail("FOO\n");

        e.Errors.Should().HaveCount(1);
        e.Line.Should().Be(1);
        e.File.Should().BeNull();
        e.Message.Should().Be("line 1: unknown mnemonic or directive 'FOO'.");
    }
}
