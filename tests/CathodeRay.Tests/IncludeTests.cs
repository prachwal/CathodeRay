using System.CommandLine;
using CathodeRay.Assembler;
using CathodeRay.Cli;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class IncludeTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("cathode-include-");

    public void Dispose() => _dir.Delete(recursive: true);

    private static AssemblyResult AsmFiles(Dictionary<string, string> files, string entry, string cpu = "stub")
    {
        var full = files.ToDictionary(static kv => Path.GetFullPath(kv.Key), static kv => kv.Value);
        string fullEntry = Path.GetFullPath(entry);
        AssemblerTarget target = AssemblerTargets.Find(cpu)!;
        return new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax)
            .Assemble(files[entry], fullEntry, path => full.TryGetValue(path, out string? text) ? text : null);
    }

    [Fact]
    public void Expands_Include_In_Place_With_Symbols_From_Both_Files()
    {
        AssemblyResult result = AsmFiles(
            new()
        {
            ["main.asm"] = "LDI first\n.include \"defs.asm\"\nSTA result\nHLT\nresult: .byte 0\n",
            ["defs.asm"] = "first: LDI 7\n",
        },
            "main.asm");

        result.Image.Should().Equal(0x01, 0x02, 0x01, 0x07, 0x05, 0x08, 0x00, 0xFF, 0x00);
        result.Symbols["first"].Should().Be(2);
        result.Symbols["result"].Should().Be(8);
    }

    [Fact]
    public void Nested_Include_Resolves_Relative_To_Includer()
    {
        AssemblyResult result = AsmFiles(
            new()
        {
            ["main.asm"] = ".include \"sub/a.asm\"\nHLT\n",
            ["sub/a.asm"] = ".include \"b.asm\"\nLDI 1\n",
            ["sub/b.asm"] = "LDI 2\n",
        },
            "main.asm");

        result.Image.Should().Equal(0x01, 0x02, 0x01, 0x01, 0xFF);
    }

    [Fact]
    public void Missing_File_Reports_Line_And_File()
    {
        var files = new Dictionary<string, string> { ["main.asm"] = "NOP\n.include \"nope.asm\"\n" };

        FluentActions.Invoking(() => AsmFiles(files, "main.asm"))
            .Should().Throw<AssemblerException>()
            .Where(e => e.Line == 2 && e.File == Path.GetFullPath("main.asm"))
            .WithMessage("*include file 'nope.asm' not found*");
    }

    [Fact]
    public void Cyclic_Include_Reports_Chain()
    {
        var files = new Dictionary<string, string>
        {
            ["a.asm"] = ".include \"b.asm\"\n",
            ["b.asm"] = ".include \"a.asm\"\n",
        };

        FluentActions.Invoking(() => AsmFiles(files, "a.asm"))
            .Should().Throw<AssemblerException>().WithMessage("*cyclic include*a.asm*b.asm*a.asm*");
    }

    [Fact]
    public void Label_On_Include_Is_Rejected()
    {
        var files = new Dictionary<string, string>
        {
            ["main.asm"] = "lib: .include \"defs.asm\"\n",
            ["defs.asm"] = "NOP\n",
        };

        FluentActions.Invoking(() => AsmFiles(files, "main.asm"))
            .Should().Throw<AssemblerException>().WithMessage("*label on .include*");
    }

    [Theory]
    [InlineData(".include defs.asm", "expected quoted file name")]
    [InlineData(".include", ".include needs a file name")]
    [InlineData(".include \"\"", "expected quoted file name")]
    public void Bad_Operand_Is_Rejected(string line, string message)
    {
        var files = new Dictionary<string, string> { ["main.asm"] = line + "\n" };

        FluentActions.Invoking(() => AsmFiles(files, "main.asm"))
            .Should().Throw<AssemblerException>().WithMessage($"*{message}*");
    }

    [Fact]
    public void Single_Quotes_Accepted()
    {
        AssemblyResult result = AsmFiles(
            new()
        {
            ["main.asm"] = ".include 'defs.asm'\nHLT\n",
            ["defs.asm"] = "NOP\n",
        },
            "main.asm");

        result.Image.Should().Equal(0x00, 0xFF);
    }

    [Fact]
    public void Bare_Text_Include_Needs_File_Context()
    {
        FluentActions.Invoking(() => Repo.Assemble("stub", ".include \"defs.asm\"\n"))
            .Should().Throw<AssemblerException>().WithMessage("*needs file context*");
    }

    [Fact]
    public void Error_Inside_Include_Carries_Its_File()
    {
        var files = new Dictionary<string, string>
        {
            ["main.asm"] = "NOP\n.include \"bad.asm\"\n",
            ["bad.asm"] = "FOO 1\n",
        };

        FluentActions.Invoking(() => AsmFiles(files, "main.asm"))
            .Should().Throw<AssemblerException>()
            .Where(e => e.Line == 1 && e.File == Path.GetFullPath("bad.asm"));
    }

    [Fact]
    public void Listing_Marks_Lines_From_Includes()
    {
        AssemblyResult result = AsmFiles(
            new()
        {
            ["main.asm"] = ".include \"defs.asm\"\nHLT\n",
            ["defs.asm"] = "NOP\n",
        },
            "main.asm");

        using var writer = new StringWriter();
        result.WriteListing(writer);
        string listing = writer.ToString();
        listing.Should().Contain($"defs.asm:1  NOP").And.Contain($"main.asm:2  HLT");
    }

    [Fact]
    public void Cli_Assembles_With_Include_From_Source_Directory()
    {
        Directory.CreateDirectory(Path.Combine(_dir.FullName, "inc"));
        File.WriteAllText(Path.Combine(_dir.FullName, "inc", "defs.asm"), "LDI 9\n");
        string main = Path.Combine(_dir.FullName, "main.asm");
        File.WriteAllText(main, ".include \"inc/defs.asm\"\nHLT\n");
        string bin = Path.Combine(_dir.FullName, "main.bin");

        var (exit, _, error) = Cli("asm", main, "--cpu", "stub", "-o", bin);

        exit.Should().Be(0, error);
        File.ReadAllBytes(bin).Should().Equal(0x01, 0x09, 0xFF);
    }

    [Fact]
    public void Cli_Finds_Include_Via_Incdir_And_Reports_Its_File_On_Error()
    {
        File.WriteAllText(Path.Combine(_dir.FullName, "lib.asm"), "FOO 1\n");
        string main = Path.Combine(_dir.FullName, "main.asm");
        File.WriteAllText(main, ".include \"lib.asm\"\n");
        string bin = Path.Combine(_dir.FullName, "main.bin");

        var (exit, _, error) = Cli("asm", main, "--cpu", "stub", "-o", bin, "--incdir", _dir.FullName);

        exit.Should().Be(1);
        error.Should().Contain("lib.asm").And.Contain("line 1");
    }

    private static (int Exit, string Out, string Err) Cli(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exit = CliApp.CreateRoot().Parse(args).Invoke(new InvocationConfiguration { Output = output, Error = error });
        return (exit, output.ToString(), error.ToString());
    }
}
