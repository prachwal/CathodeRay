using CathodeRay.Assembler;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class LocalsIncbinTests
{
    private static AssemblyResult AsmFiles(
        Dictionary<string, string> files,
        string entry,
        Dictionary<string, byte[]>? binaries = null,
        string cpu = "stub",
        string? syntax = null)
    {
        var full = files.ToDictionary(static kv => Path.GetFullPath(kv.Key), static kv => kv.Value);
        var fullBin = (binaries ?? new Dictionary<string, byte[]>())
            .ToDictionary(static kv => Path.GetFullPath(kv.Key), static kv => kv.Value);
        string fullEntry = Path.GetFullPath(entry);
        AssemblerTarget target = AssemblerTargets.Find(cpu)!;
        return new TwoPassAssembler(
                Repo.LoadTarget(target),
                syntax is null ? target.DefaultSyntax : target.FindSyntax(syntax)!)
            .Assemble(
                files[entry],
                fullEntry,
                path => full.TryGetValue(path, out string? text) ? text : null,
                [],
                path => fullBin.TryGetValue(path, out byte[]? data) ? data : null);
    }

    private static byte[] Asm(string source, string cpu = "stub", string? syntax = null) =>
        Repo.Assemble(cpu, source, syntax).Image;

    [Fact]
    public void Same_Local_In_Two_Scopes_Resolves_Locally()
    {
        Asm("org1: LDI 1\n@x: LDI 2\norg2: LDI 3\n@x: LDI 4\nLDA @x\nHLT")
            .Should().Equal(0x01, 0x01, 0x01, 0x02, 0x01, 0x03, 0x01, 0x04, 0x06, 0x06, 0x00, 0xFF);
    }

    [Fact]
    public void Forward_Reference_Within_Scope_Works()
    {
        Asm("aa: JMP @x\n@x: INC\nHLT")
            .Should().Equal(0x04, 0x03, 0x00, 0x03, 0xFF);
    }

    [Fact]
    public void Duplicate_Local_In_Same_Scope_Is_Rejected()
    {
        FluentActions.Invoking(() => Asm("aa: NOP\n@x: NOP\n@x: NOP\n"))
            .Should().Throw<AssemblerException>().WithMessage("*duplicate symbol '@x'*");
    }

    [Fact]
    public void Local_Without_Preceding_Global_Is_Rejected()
    {
        FluentActions.Invoking(() => Asm("@x: NOP\n"))
            .Should().Throw<AssemblerException>().WithMessage("*no preceding global label for '@x'*");
        FluentActions.Invoking(() => Asm("LDI @x\n"))
            .Should().Throw<AssemblerException>().WithMessage("*no preceding global label for '@x'*");
    }

    [Fact]
    public void Local_Works_In_Expressions_And_Data()
    {
        Asm("aa: NOP\n@v = 3\nLDI @v + 1\n.byte @v\nHLT\nbb: NOP\n")
            .Should().Equal(0x00, 0x01, 0x04, 0x03, 0xFF, 0x00);
    }

    [Fact]
    public void Local_Outside_Its_Scope_Is_Undefined()
    {
        FluentActions.Invoking(() => Asm("aa: NOP\n@v = 3\nbb: LDI @v\n"))
            .Should().Throw<AssemblerException>().WithMessage("*undefined symbol '@v'*");
    }

    [Fact]
    public void Incbin_Inserts_File_Bytes()
    {
        var files = new Dictionary<string, string> { ["main.asm"] = "LDI 1\n.incbin \"b.bin\"\nHLT\n" };
        var bins = new Dictionary<string, byte[]> { ["b.bin"] = [0xAA, 0xBB] };

        AsmFiles(files, "main.asm", bins).Image.Should().Equal(0x01, 0x01, 0xAA, 0xBB, 0xFF);
    }

    [Theory]
    [InlineData(".incbin \"missing.bin\"", "binary file 'missing.bin' not found")]
    [InlineData(".incbin data.bin", "expected quoted file name after .incbin")]
    [InlineData(".incbin", ".incbin needs a file name")]
    public void Bad_Incbin_Is_Rejected(string line, string message)
    {
        var files = new Dictionary<string, string> { ["main.asm"] = line + "\n" };

        FluentActions.Invoking(() => AsmFiles(files, "main.asm"))
            .Should().Throw<AssemblerException>().WithMessage($"*{message}*");
    }

    [Fact]
    public void Bare_Text_Incbin_Needs_File_Context()
    {
        FluentActions.Invoking(() => Asm(".incbin \"b.bin\"\n"))
            .Should().Throw<AssemblerException>().WithMessage("*needs file context*");
    }

    [Fact]
    public void Ca65_Locals_And_Incbin()
    {
        var files = new Dictionary<string, string>
        {
            ["main.s"] = "aa: lda #1\n@x: lda #2\nbb: lda #3\n@x: lda #4\nlda @x\n.incbin \"b.bin\"\nrts\n",
        };
        var bins = new Dictionary<string, byte[]> { ["b.bin"] = [0x42] };

        AsmFiles(files, "main.s", bins, "6502").Image
            .Should().Equal(0xA9, 0x01, 0xA9, 0x02, 0xA9, 0x03, 0xA9, 0x04, 0xA5, 0x06, 0x42, 0x60);
    }

    [Fact]
    public void Zilog_Locals_And_Incbin()
    {
        var files = new Dictionary<string, string>
        {
            ["main.asm"] = "aa: LD A,1\n@x: LD A,2\nbb: LD A,3\n@x: LD A,4\nLD A,(@x)\nINCBIN \"b.bin\"\nRET\n",
        };
        var bins = new Dictionary<string, byte[]> { ["b.bin"] = [0x42] };

        AsmFiles(files, "main.asm", bins, "z80").Image
            .Should().Equal(0x3E, 0x01, 0x3E, 0x02, 0x3E, 0x03, 0x3E, 0x04, 0x3A, 0x06, 0x00, 0x42, 0xC9);
    }

    [Fact]
    public void Mos_And_Intel_Incbin_Names()
    {
        var inc = new Dictionary<string, string> { ["m.s"] = ".INCBIN \"b.bin\"\nRTS" };
        var bins = new Dictionary<string, byte[]> { ["b.bin"] = [0x42] };
        AsmFiles(inc, "m.s", bins, "6502", "mos").Image.Should().Equal(0x42, 0x60);

        var intel = new Dictionary<string, string> { ["m.asm"] = "ORG 100H\nINCBIN \"b.bin\"\nHLT" };
        AsmFiles(intel, "m.asm", bins, "8080").Image.Should().Equal(0x42, 0x76);
    }
}
