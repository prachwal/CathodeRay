using System.CommandLine;
using CathodeRay.Assembler;
using CathodeRay.Cli;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class AlignHexTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("cathode-alignhex-");

    public void Dispose() => _dir.Delete(recursive: true);

    private static byte[] Asm(string source, string cpu = "stub", string? syntax = null) =>
        Repo.Assemble(cpu, source, syntax).Image;

    [Fact]
    public void Align_Pads_With_Zeros_To_Multiple()
    {
        Asm(".byte 1\n.align 4\n.byte 2\n")
            .Should().Equal(0x01, 0x00, 0x00, 0x00, 0x02);
    }

    [Fact]
    public void Align_With_Fill_And_Already_Aligned_Is_No_Op()
    {
        Asm(".byte 1, 2, 3, 4\n.align 4\n.align 2, $EA\n.byte 5\n")
            .Should().Equal(0x01, 0x02, 0x03, 0x04, 0x05);
    }

    [Fact]
    public void Align_Accepts_Any_Size_Like_References()
    {
        Asm(".org $0600\n.byte 1\n.align 3\n.byte 2\n", "6502")
            .Should().Equal(0x01, 0x00, 0x00, 0x02);
    }

    [Theory]
    [InlineData(".align 0", "alignment >= 1")]
    [InlineData(".align -2", "alignment >= 1")]
    [InlineData(".align", "alignment expected")]
    [InlineData(".align 4, 256", "fill 0..255")]
    [InlineData(".align 4, 1, 2", "expected alignment")]
    public void Bad_Align_Is_Rejected(string line, string message)
    {
        FluentActions.Invoking(() => Asm(line + "\n"))
            .Should().Throw<AssemblerException>().WithMessage($"*{message}*");
    }

    [Fact]
    public void Intel_Uses_Align()
    {
        Asm("ORG 100H\nDB 1\nALIGN 4\nDB 2\nHLT", "8080", "intel")
            .Should().Equal(0x01, 0x00, 0x00, 0x00, 0x02, 0x76);
    }

    [Theory]
    [InlineData("6502", null, ".org $0600\n.byte 1\n.align 4\n.byte 2\n")]
    [InlineData("6502x", null, ".org $0600\n.byte 1\n.align 4\n.byte 2\n")]
    [InlineData("65c02", null, ".org $0800\n.byte 1\n.align 4\n.byte 2\n")]
    [InlineData("6502", "mos", "*= $0600\n.BYTE 1\n.ALIGN 4\n.BYTE 2\n")]
    [InlineData("z80", null, "ORG 8000H\nDB 1\nALIGN 4\nDB 2\n")]
    [InlineData("z80u", null, "ORG 8000H\nDB 1\nALIGN 4\nDB 2\n")]
    public void Align_Works_On_Every_Cpu(string cpu, string? syntax, string source)
    {
        Asm(source, cpu, syntax).Should().Equal(0x01, 0x00, 0x00, 0x00, 0x02);
    }

    [Fact]
    public void Intel_Hex_Has_Data_Eof_And_Checksum()
    {
        var result = Repo.Assemble("6502", ".org $0600\n.byte 1\n.align 4\n.byte 2\n.align 8, $EA\n.byte 3\n");
        using var writer = new StringWriter();
        result.WriteIntelHex(writer);

        writer.ToString().Should().Be(":090600000100000002EAEAEA032D\n:00000001FF\n");
    }

    [Fact]
    public void Intel_Hex_Splits_Rows_At_16_Bytes()
    {
        var result = Repo.Assemble("stub", ".byte 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17\n");
        using var writer = new StringWriter();
        result.WriteIntelHex(writer);
        string[] rows = writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        rows.Should().HaveCount(3);
        rows[0].Should().Be(":100000000102030405060708090A0B0C0D0E0F1068");
        rows[1].Should().Be(":0100100011DE");
        rows[2].Should().Be(":00000001FF");
    }

    [Fact]
    public void Cli_Writes_Hex_By_Default_Extension()
    {
        string asm = Path.Combine(_dir.FullName, "prog.asm");
        File.WriteAllText(asm, ".byte $42\n");

        var (exit, output, _) = Cli("asm", asm, "--cpu", "stub", "-f", "hex");

        exit.Should().Be(0);
        output.Should().Contain("prog.asm -> prog.hex");
        File.ReadAllText(Path.ChangeExtension(asm, ".hex")).Should().Be(":0100000042BD\n:00000001FF\n");
    }

    [Fact]
    public void Cli_Rejects_Bad_Format()
    {
        string asm = Path.Combine(_dir.FullName, "prog.asm");
        File.WriteAllText(asm, "NOP\n");

        var (exit, _, error) = Cli("asm", asm, "--cpu", "stub", "-f", "hexbin");

        exit.Should().NotBe(0);
        error.Should().Contain("hexbin");
    }

    private static (int Exit, string Out, string Err) Cli(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exit = CliApp.CreateRoot().Parse(args).Invoke(new InvocationConfiguration { Output = output, Error = error });
        return (exit, output.ToString(), error.ToString());
    }
}
