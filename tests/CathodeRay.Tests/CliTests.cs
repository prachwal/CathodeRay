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
        Cli("stub", "asm", asm, "-o", bin).Exit.Should().Be(0);
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

        var (exit, output, _) = Cli("stub", "asm", asm);

        exit.Should().Be(0);
        output.Should().Contain("sum.asm -> sum.bin (13 B)");
        System.IO.File.ReadAllBytes(Path.ChangeExtension(asm, ".bin")).Should().HaveCount(13);
    }

    [Fact]
    public void Asm_Reports_Error_With_Line()
    {
        string asm = File("bad.asm", "LDI 1\nFOO 2\n");

        var (exit, _, error) = Cli("stub", "asm", asm);

        exit.Should().Be(1);
        error.Should().Contain("bad.asm: line 2: unknown mnemonic 'FOO'.");
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
}
