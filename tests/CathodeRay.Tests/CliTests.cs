using System.CommandLine;
using CathodeRay.Cli;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class CliTests : IDisposable
{
    private const string Sum = """
        start:  LDI 250
                ADD 10
                STA $2000
                INC
                JMP done
                NOP
        done:   HLT
        """;

    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("cathode-cli-");

    public void Dispose() => _dir.Delete(recursive: true);

    private string File(string name, string content)
    {
        string path = Path.Combine(_dir.FullName, name);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    private string Binary(string source)
    {
        string asm = File("prog.asm", source);
        string bin = Path.Combine(_dir.FullName, "prog.bin");
        Cli("asm", asm, "--cpu", "stub", "-o", bin).Exit.Should().Be(0);
        return bin;
    }

    private static (int Exit, string Out, string Err) Cli(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exit = CliApp.CreateRoot().Parse(args).Invoke(new InvocationConfiguration { Output = output, Error = error });
        return (exit, output.ToString(), error.ToString());
    }

    [Fact]
    public void Asm_Writes_Binary_Next_To_Source_By_Default()
    {
        string asm = File("sum.asm", Sum);

        var (exit, output, _) = Cli("asm", asm, "--cpu", "stub");

        exit.Should().Be(0);
        output.Should().Contain("sum.asm -> sum.bin (13 B, load $0000, cpu stub, syntax stub)");
        System.IO.File.ReadAllBytes(Path.ChangeExtension(asm, ".bin")).Should().HaveCount(13);
    }

    [Fact]
    public void Asm_Reports_Error_With_Line()
    {
        string asm = File("bad.asm", "LDI 1\nFOO 2\n");

        var (exit, _, error) = Cli("asm", asm, "--cpu", "stub");

        exit.Should().Be(1);
        error.Should().Contain("bad.asm: line 2: unknown mnemonic or directive 'FOO'.");
    }

    [Fact]
    public void Run_Halts_And_Prints_Registers()
    {
        var (exit, output, _) = Cli("stub", "run", Binary(Sum));

        exit.Should().Be(0);
        output.Should().Contain("steps=6 cycles=12 halted=True time=").And.Contain("A=05 X=00 C=1 V=0 Z=0 PC=000D");
    }

    [Fact]
    public void Run_Trace_Prints_Each_Step()
    {
        var (_, output, _) = Cli("stub", "run", Binary(Sum), "--trace");

        output.Should().Contain("0000  01 LDI  A=FA X=00 C=0 V=0 Z=0 PC=0002  +2 cyc=2 bus=Fetch, Read")
            .And.Contain("0004  05 STA  A=04 X=00 C=1 V=0 Z=0 PC=0007  +3 cyc=7 bus=Fetch, Read, Write")
            .And.Contain("000C  FF HLT  A=05 X=00 C=1 V=0 Z=0 PC=000D  +1 cyc=12 bus=Fetch, Halt");
    }

    [Fact]
    public void Run_Dumps_Memory_Ranges()
    {
        var (exit, output, _) = Cli("stub", "run", Binary(Sum), "--dump", "$2000:4", "-d", "0:18");

        exit.Should().Be(0);
        output.Should().Contain("2000: 04 00 00 00")
            .And.Contain("0000: 01 FA 02 0A 05 00 20 03 04 0C 00 00 FF 00 00 00")
            .And.Contain("0010: 00 00");
    }

    [Theory]
    [InlineData("abc", "Invalid dump range 'abc'")]
    [InlineData("$FFFF:2", "must be non-empty and within 64 KB")]
    [InlineData("0:0", "must be non-empty and within 64 KB")]
    public void Run_Rejects_Bad_Dump_Range(string range, string message)
    {
        var (exit, _, error) = Cli("stub", "run", Binary(Sum), "--dump", range);

        exit.Should().NotBe(0);
        error.Should().Contain(message);
    }

    [Fact]
    public void Run_Stops_At_Max_Steps_With_Exit_2()
    {
        var (exit, output, _) = Cli("stub", "run", Binary("l: JMP l"), "--max-steps", "5");

        exit.Should().Be(2);
        output.Should().Contain("steps=5").And.Contain("halted=False");
    }

    [Fact]
    public void Run_Reports_Unknown_Opcode()
    {
        string bin = Path.Combine(_dir.FullName, "junk.bin");
        System.IO.File.WriteAllBytes(bin, [0x7F]);

        var (exit, _, error) = Cli("stub", "run", bin);

        exit.Should().Be(1);
        error.Should().Contain("Unknown opcode 0x7F at 0x0000.");
    }

    [Fact]
    public void Missing_File_Is_A_Parse_Error()
    {
        var (exit, _, error) = Cli("stub", "run", Path.Combine(_dir.FullName, "nope.bin"));

        exit.Should().NotBe(0);
        error.Should().Contain("File does not exist");
    }

    [Theory]
    [InlineData("6502", null, "  .org $0600\n  lda #$12\n  jmp ($1234)", "6502, syntax ca65", new byte[] { 0xA9, 0x12, 0x6C, 0x34, 0x12 })]
    [InlineData("6502", "mos", "  *= $0600\nL LDA #$12\n  JMP L", "6502, syntax mos", new byte[] { 0xA9, 0x12, 0x4C, 0x00, 0x06 })]
    [InlineData("65c02", null, "  .org $0600\n  stz $12", "65c02, syntax ca65", new byte[] { 0x64, 0x12 })]
    [InlineData("8080", null, "  ORG 100H\n  MVI A,0FFH", "8080, syntax intel", new byte[] { 0x3E, 0xFF })]
    public void Asm_Selects_Cpu_And_Syntax(string cpu, string? syntax, string source, string summary, byte[] expected)
    {
        string asm = File("prog.s", source);
        string bin = Path.Combine(_dir.FullName, "prog.bin");
        string[] args = syntax is null ? ["asm", asm, "--cpu", cpu, "-o", bin] : ["asm", asm, "--cpu", cpu, "--syntax", syntax, "-o", bin];

        var (exit, output, _) = Cli(args);

        exit.Should().Be(0);
        output.Should().Contain($"cpu {summary}");
        System.IO.File.ReadAllBytes(bin).Should().Equal(expected);
    }

    [Fact]
    public void Asm_Illegal_Enables_Undocumented_6502()
    {
        string asm = File("ill.s", "  lax $12");

        Cli("asm", asm, "--cpu", "6502").Exit.Should().Be(1);
        var (exit, output, _) = Cli("asm", asm, "--cpu", "6502", "--illegal");

        exit.Should().Be(0);
        output.Should().Contain("cpu 6502x");
    }

    [Theory]
    [InlineData(new[] { "--cpu", "8080", "--illegal" }, "--illegal applies only to --cpu 6502")]
    [InlineData(new[] { "--cpu", "8080", "--syntax", "ca65" }, "Syntax 'ca65' is not available for --cpu 8080 (allowed: intel)")]
    [InlineData(new[] { "--cpu", "z80" }, "z80")]
    [InlineData(new string[0], "--cpu")]
    public void Asm_Rejects_Bad_Flags(string[] flags, string message)
    {
        string asm = File("prog.s", "  NOP");

        var (exit, _, error) = Cli(["asm", asm, .. flags]);

        exit.Should().NotBe(0);
        error.Should().Contain(message);
    }

    [Fact]
    public void Asm_Writes_Listing()
    {
        string asm = File("prog.s", "  .org $0600\nstart: lda #1\n  rts");
        string listing = Path.Combine(_dir.FullName, "prog.lst");

        Cli("asm", asm, "--cpu", "6502", "-l", listing).Exit.Should().Be(0);

        System.IO.File.ReadAllText(listing).Should().Contain("0600  A9 01            2  start: lda #1").And.Contain("0602  60");
    }
}
