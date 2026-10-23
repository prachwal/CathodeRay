using System.CommandLine;
using CathodeRay.Cli;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Diagnostyka: <c>cc --emit-ir / --dump-regalloc / --verbose</c> oraz <c>run</c> (ślad, zrzut, wynik main).</summary>
public sealed class DiagnosticsCliTests : IDisposable
{
    private const string Source = "int sum(int n) { int s = 0; int i; for (i = 0; i < n; i++) s += i; return s; }\nint main(void) { return sum(10); }\n";

    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("cathode-diag-");

    public void Dispose() => _dir.Delete(recursive: true);

    private static (int Exit, string Out, string Err) Cli(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exit = CliApp.CreateRoot().Parse(args).Invoke(new InvocationConfiguration { Output = output, Error = error });
        return (exit, output.ToString(), error.ToString());
    }

    private string Path(string name) => System.IO.Path.Combine(_dir.FullName, name);

    private string Source_c()
    {
        string path = Path("d.c");
        File.WriteAllText(path, Source);
        return path;
    }

    [Fact]
    public void EmitIr_Writes_Numbered_Ir_Of_User_Modules_Only()
    {
        var (exit, _, err) = Cli("cc", Source_c(), "-o", Path("d.bin"), "--cpu", "z80", "--emit-ir", Path("d.ir"));

        exit.Should().Be(0, err);
        string ir = File.ReadAllText(Path("d.ir"));
        ir.Should().Contain("IR after 'lowered'").And.Contain("func sum(").And.Contain("br Ge").And.Contain("ret ");
        ir.Should().NotContain("<stdlib>");
    }

    [Fact]
    public void EmitIr_On_Vreg_Path_Adds_The_Vreg_Stage()
    {
        var (exit, _, err) = Cli("cc", Source_c(), "-o", Path("d.bin"), "--cpu", "z80", "--ir", "vreg", "--emit-ir", Path("d.ir"));

        exit.Should().Be(0, err);
        File.ReadAllText(Path("d.ir")).Should().Contain("IR after 'lowered'").And.Contain("IR after 'vreg'");
    }

    [Theory]
    [InlineData("z80")]
    [InlineData("8080")]
    public void DumpRegalloc_Lists_Cells_With_Register_Or_Cause(string cpu)
    {
        var (exit, _, err) = Cli("cc", Source_c(), "-o", Path("d.bin"), "--cpu", cpu, "--dump-regalloc", Path("d.ra"));

        exit.Should().Be(0, err);
        string dump = File.ReadAllText(Path("d.ra"));
        dump.Should().Contain("register allocation").And.Contain("func main").And.Contain("register");
        dump.Should().MatchRegex(@"sum__i\s+memory\s+\w+");
    }

    [Fact]
    public void DumpRegalloc_Warns_On_Target_Without_Allocator()
    {
        var (exit, _, err) = Cli("cc", Source_c(), "-o", Path("d.bin"), "--cpu", "6502", "--dump-regalloc", Path("d.ra"));

        exit.Should().Be(0, err);
        err.Should().Contain("--dump-regalloc has no effect for '6502'");
    }

    [Fact]
    public void Verbose_Reports_Settings_Stdlib_And_Image()
    {
        var (exit, _, err) = Cli("cc", Source_c(), "-o", Path("d.bin"), "--cpu", "z80", "-v");

        exit.Should().Be(0, err);
        err.Should().Contain("cc: target=z80 ir=cell abi=v1 optimize=on").And.Contain("cc: linked ").And.Contain("modules, image");
    }

    [Fact]
    public void Diagnostics_Do_Not_Change_The_Image()
    {
        Cli("cc", Source_c(), "-o", Path("plain.bin"), "--cpu", "z80").Exit.Should().Be(0);
        Cli("cc", Source_c(), "-o", Path("diag.bin"), "--cpu", "z80", "-v", "--emit-ir", Path("a.ir"), "--dump-regalloc", Path("a.ra")).Exit.Should().Be(0);

        File.ReadAllBytes(Path("diag.bin")).Should().Equal(File.ReadAllBytes(Path("plain.bin")));
    }

    [Theory]
    [InlineData("z80")]
    [InlineData("8080")]
    [InlineData("6502")]
    [InlineData("6800")]
    [InlineData("6809")]
    public void Run_Executes_The_Image_And_Reads_The_Result_From_The_Listing(string cpu)
    {
        Cli("cc", Source_c(), "-o", Path("d.bin"), "-l", Path("d.lst"), "--cpu", cpu).Exit.Should().Be(0);

        var (exit, output, err) = Cli("run", Path("d.bin"), "--cpu", cpu, "-l", Path("d.lst"));

        exit.Should().Be(0, err);
        output.Should().Contain("halted=True").And.Contain("result: 45 (0x002D)");
    }

    [Fact]
    public void Run_Trace_Is_Limited_And_Dump_Prints_Memory()
    {
        Cli("cc", Source_c(), "-o", Path("d.bin"), "--cpu", "z80").Exit.Should().Be(0);

        var (exit, output, _) = Cli("run", Path("d.bin"), "--cpu", "z80", "--trace", "--trace-limit", "3", "--dump", "0x1000:4");

        exit.Should().Be(0);
        output.Should().Contain("1000  31 00 10").And.Contain("trace truncated at 3").And.Contain("1000: 31 00 10 21");
        output.Split('\n').Count(static l => l.StartsWith("10", StringComparison.Ordinal) && l.Contains("SP=", StringComparison.Ordinal)).Should().Be(3);
    }

    [Fact]
    public void Run_Reports_Step_Limit_With_Exit_Code_2()
    {
        Cli("cc", Source_c(), "-o", Path("d.bin"), "--cpu", "z80").Exit.Should().Be(0);

        var (exit, output, err) = Cli("run", Path("d.bin"), "--cpu", "z80", "--max-steps", "10");

        exit.Should().Be(2);
        output.Should().Contain("halted=False");
        err.Should().Contain("step limit 10 reached");
    }
}
