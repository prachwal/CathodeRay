using System.CommandLine;
using CathodeRay.Assembler;
using CathodeRay.C;
using CathodeRay.Cli;
using CathodeRay.Stub;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Ekran 40x25: biblioteka screen.s, dekoder i dump do Markdown.</summary>
public sealed class ScreenTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("cathode-screen-");

    public void Dispose() => _dir.Delete(recursive: true);

    private static string LibDir() => Repo.Path("samples", "stub", "lib");

    private static (StubCpu Cpu, StubBus Bus, AssemblyResult Result) RunAsm(string source, string name)
    {
        AssemblerTarget target = AssemblerTargets.Find("stub")!;
        string entry = Path.Combine(LibDir(), name);
        static string? Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
        var origins = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["CODE"] = 0x1000,
            ["DATA"] = 0x2000,
        };
        AssemblyResult result = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax)
            .Assemble(source, entry, Read, [], null, origins);
        var bus = new StubBus();
        for (int i = 0; i < result.Image.Length; i++)
        {
            bus.Write((ushort)(result.Origin + i), result.Image[i]);
        }

        StubIsa isa = StubIsa.FromJsonFile(Repo.IsaFile("mcp_stub_instructions.json"));
        var cpu = new StubCpu(isa, bus);
        cpu.State.ProgramCounter = 0x1000;
        for (int steps = 0; !cpu.State.Halted; steps++)
        {
            steps.Should().BeLessThan(10_000, "program ma się zatrzymać");
            cpu.Step();
        }

        return (cpu, bus, result);
    }

    [Fact]
    public void Screen_Putc_And_Clear_Draw_And_Wipe()
    {
        const string Driver = """
            .segment "CODE"
            CALL scr_clear
            LDI 72
            CALL scr_putc
            LDI 105
            CALL scr_putc
            HLT
            .include "screen.s"
            """;
        var (_, bus, result) = RunAsm(Driver, "screentest.asm");
        int buf = result.Symbols["__scr_buf"];
        IReadOnlyList<string> rows = new ScreenDecoder().Render(bus.Read, buf);

        rows.Should().HaveCount(25);
        rows[0].Should().Be("Hi");
        rows.Skip(1).Should().OnlyContain(static s => s.Length == 0);
        string md = new ScreenDecoder().ToMarkdown(rows, buf);
        md.Should().Contain($"# Screen dump (40x25 @ ${buf:X4})");
        md.Should().Contain("|Hi" + new string(' ', 38) + "|");
    }

    [Fact]
    public void Screen_Clear_Wipes_Previous_Content()
    {
        const string Driver = """
            .segment "CODE"
            LDI 65
            CALL scr_putc
            CALL scr_clear
            HLT
            .include "screen.s"
            """;
        var (_, bus, result) = RunAsm(Driver, "screentest.asm");
        int buf = result.Symbols["__scr_buf"];

        new ScreenDecoder().Render(bus.Read, buf).Should().OnlyContain(static s => s.Length == 0);
        bus.Read((ushort)result.Symbols["__scr_cur"]).Should().Be(0);
    }

    [Fact]
    public void C_Draws_On_Screen_Through_Prototypes()
    {
        const string Source = """
            void scr_putc(uchar c);
            void scr_clear();
            int main() {
                scr_clear();
                scr_putc(65);
                scr_putc(66);
                scr_putc(67);
                return 0;
            }
            """;
        string screen = File.ReadAllText(Path.Combine(LibDir(), "screen.s"));
        CheckedProgram checkedProgram = TypeChecker.Check(Parser.Parse(Source));
        string asm = Crt0.Source + screen + Codegen.Emit(checkedProgram);
        AssemblerTarget target = AssemblerTargets.Find("stub")!;
        var origins = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["CODE"] = 0x1000,
            ["INIT"] = 0x6F00,
            ["BSS"] = 0x7000,
            ["DATA"] = 0x8000,
        };
        AssemblyResult result = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax)
            .Assemble(asm, "prog.c", _ => null, [], null, origins);
        var bus = new StubBus();
        for (int i = 0; i < result.Image.Length; i++)
        {
            bus.Write((ushort)(result.Origin + i), result.Image[i]);
        }

        StubIsa isa = StubIsa.FromJsonFile(Repo.IsaFile("mcp_stub_instructions.json"));
        var cpu = new StubCpu(isa, bus);
        cpu.State.ProgramCounter = (ushort)origins["CODE"];
        for (int steps = 0; !cpu.State.Halted; steps++)
        {
            steps.Should().BeLessThan(100_000, "program ma się zatrzymać");
            cpu.Step();
        }

        int buf = result.Symbols["__scr_buf"];
        new ScreenDecoder().Render(bus.Read, buf)[0].Should().Be("ABC");
    }

    [Fact]
    public void Decoder_Accepts_Custom_Size_From_Constructor()
    {
        byte[] memory = [65, 66, 0, 67, 68, 69, 7, 68];
        var decoder = new ScreenDecoder(4, 2);

        decoder.Width.Should().Be(4);
        decoder.Height.Should().Be(2);
        decoder.Size.Should().Be(8);
        decoder.Render(a => memory[a], 0).Should().Equal("AB C", "DE D");
        decoder.ToMarkdown(["AB C", "DE D"], 0x100).Should().Contain("# Screen dump (4x2 @ $0100)");
        FluentActions.Invoking(() => new ScreenDecoder(0, 25)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new ScreenDecoder(40, 0)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Cli_Run_Writes_Screen_Markdown_File()
    {
        const string Driver = """
            .segment "CODE"
            CALL scr_clear
            LDI 72
            CALL scr_putc
            LDI 105
            CALL scr_putc
            HLT
            .include "screen.s"
            """;
        string asmPath = Path.Combine(_dir.FullName, "draw.asm");
        File.WriteAllText(asmPath, Driver.Replace(".include \"screen.s\"", $".include \"{Path.Combine(LibDir(), "screen.s")}\""));
        string bin = Path.Combine(_dir.FullName, "draw.bin");
        string listing = Path.Combine(_dir.FullName, "draw.lst");
        string md = Path.Combine(_dir.FullName, "screen.md");

        var (asmExit, _, asmErr) = Cli("asm", asmPath, "--cpu", "stub", "-o", bin, "-l", listing, "-m", "CODE@0x1000", "-m", "DATA@0x2000");
        asmExit.Should().Be(0, asmErr);

        string bufHex = System.Text.RegularExpressions.Regex.Match(
            File.ReadAllText(listing),
            @"^([0-9A-F]+)\s.*__scr_buf:",
            System.Text.RegularExpressions.RegexOptions.Multiline).Groups[1].Value;
        bufHex.Should().NotBeEmpty();

        var (exit, output, err) = Cli("stub", "run", bin, "--load", "0x1000", "--screen-at", "0x" + bufHex, "--screen-out", md);
        exit.Should().Be(0, err);
        output.Should().Contain("screen (40x25 @ $");
        string mdText = File.ReadAllText(md);
        mdText.Should().Contain("|Hi" + new string(' ', 38) + "|");
        mdText.Should().Contain("+----------------------------------------+");
        mdText.Should().Contain($"# Screen dump (40x25 @ $");
    }

    [Fact]
    public void Cli_Run_Accepts_Custom_Screen_Size()
    {
        const string Driver = """
            .segment "CODE"
            CALL scr_clear
            LDI 72
            CALL scr_putc
            LDI 105
            CALL scr_putc
            HLT
            .include "screen.s"
            """;
        string asmPath = Path.Combine(_dir.FullName, "draw.asm");
        File.WriteAllText(asmPath, Driver.Replace(".include \"screen.s\"", $".include \"{Path.Combine(LibDir(), "screen.s")}\""));
        string bin = Path.Combine(_dir.FullName, "draw.bin");
        string listing = Path.Combine(_dir.FullName, "draw.lst");
        string md = Path.Combine(_dir.FullName, "screen.md");

        var (asmExit, _, asmErr) = Cli("asm", asmPath, "--cpu", "stub", "-o", bin, "-l", listing, "-m", "CODE@0x1000", "-m", "DATA@0x2000");
        asmExit.Should().Be(0, asmErr);

        string bufHex = System.Text.RegularExpressions.Regex.Match(
            File.ReadAllText(listing),
            @"^([0-9A-F]+)\s.*__scr_buf:",
            System.Text.RegularExpressions.RegexOptions.Multiline).Groups[1].Value;

        var (exit, output, err) = Cli("stub", "run", bin, "--load", "0x1000", "--screen-at", "0x" + bufHex, "--screen-size", "8x2", "--screen-out", md);
        exit.Should().Be(0, err);
        output.Should().Contain("screen (8x2 @ $");
        string mdText = File.ReadAllText(md);
        mdText.Should().Contain("# Screen dump (8x2 @ $");
        mdText.Should().Contain("+--------+");
        mdText.Should().Contain("|Hi      |");

        var (badExit, _, badErr) = Cli("stub", "run", bin, "--load", "0x1000", "--screen-at", "0x" + bufHex, "--screen-size", "ax25");
        badExit.Should().Be(1);
        badErr.Should().Contain("Invalid --screen-size");
    }

    private static (int Exit, string Out, string Err) Cli(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exit = CliApp.CreateRoot().Parse(args).Invoke(new InvocationConfiguration { Output = output, Error = error });
        return (exit, output.ToString(), error.ToString());
    }

    private static IReadOnlyList<string> RunCScreen(string name, int maxSteps = 500_000) =>
        RunCScreenFull(name, maxSteps).Rows;

    private static (IReadOnlyList<string> Rows, StubBus Bus, int Buf) RunCScreenFull(string name, int maxSteps = 500_000)
    {
        string screen = File.ReadAllText(Path.Combine(LibDir(), "screen.s"));
        string path = Repo.Path("samples", "minic", name);
        CheckedProgram checkedProgram = TypeChecker.Check(Parser.Parse(File.ReadAllText(path)));
        string asm = Crt0.Source + screen + Codegen.Emit(checkedProgram);
        AssemblerTarget target = AssemblerTargets.Find("stub")!;
        var origins = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["CODE"] = 0x1000,
            ["INIT"] = 0x6F00,
            ["BSS"] = 0x7000,
            ["DATA"] = 0x8000,
        };
        AssemblyResult result = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax)
            .Assemble(asm, path, _ => null, [], null, origins);
        var bus = new StubBus();
        for (int i = 0; i < result.Image.Length; i++)
        {
            bus.Write((ushort)(result.Origin + i), result.Image[i]);
        }

        StubIsa isa = StubIsa.FromJsonFile(Repo.IsaFile("mcp_stub_instructions.json"));
        var cpu = new StubCpu(isa, bus);
        cpu.State.ProgramCounter = (ushort)origins["CODE"];
        for (int steps = 0; !cpu.State.Halted; steps++)
        {
            steps.Should().BeLessThan(maxSteps, "program ma się zatrzymać");
            cpu.Step();
        }

        return (new ScreenDecoder().Render(bus.Read, result.Symbols["__scr_buf"]), bus, result.Symbols["__scr_buf"]);
    }

    [Fact]
    public void Demo_Empty_Screen_Has_No_Text()
    {
        RunCScreen("scr_empty.c").Should().OnlyContain(static s => s.Length == 0);
    }

    [Fact]
    public void Demo_Full_Row_Stays_In_Row_Zero()
    {
        IReadOnlyList<string> rows = RunCScreen("scr_row.c");

        rows[0].Should().Be(new string('A', 40));
        rows.Skip(1).Should().OnlyContain(static s => s.Length == 0);
    }

    [Fact]
    public void Demo_41st_Char_Wraps_To_Row_One()
    {
        IReadOnlyList<string> rows = RunCScreen("scr_wrap.c");

        rows[0].Should().Be(new string('B', 40));
        rows[1].Should().Be("B");
        rows.Skip(2).Should().OnlyContain(static s => s.Length == 0);
    }

    [Fact]
    public void Demo_Full_Screen_Fills_Last_Cell()
    {
        IReadOnlyList<string> rows = RunCScreen("scr_full.c");

        rows.Should().HaveCount(25);
        rows.Should().OnlyContain(s => s == new string('C', 40));
    }

    [Fact]
    public void Demo_Overflow_Scrolls_Screen_Up()
    {
        IReadOnlyList<string> rows = RunCScreen("scr_over.c");

        rows.Should().HaveCount(25);
        rows.Take(24).Should().OnlyContain(s => s == new string('A', 40));
        rows[24].Should().Be("BBBBB" + new string('A', 35));
    }

    [Fact]
    public void Demo_Control_Chars_Decode_As_Spaces()
    {
        IReadOnlyList<string> rows = RunCScreen("scr_ctrl.c");

        rows[0].Should().Be("A B");
        rows.Skip(1).Should().OnlyContain(static s => s.Length == 0);
    }

    [Fact]
    public void Demo_Goto_Corners_And_Clamps_Out_Of_Range()
    {
        var (rows, bus, buf) = RunCScreenFull("scr_goto.c");

        bus.Read((ushort)buf).Should().Be(65);
        bus.Read((ushort)(buf + 39)).Should().Be(66);
        bus.Read((ushort)(buf + 960)).Should().Be(67);
        bus.Read((ushort)(buf + 999)).Should().Be(69);
        rows[0][0].Should().Be('A');
        rows[24][39].Should().Be('E');
    }

    [Fact]
    public void Demo_Newline_Moves_To_Next_Row_Start()
    {
        IReadOnlyList<string> rows = RunCScreen("scr_nl.c");

        rows[0].Should().Be("A");
        rows[1].Should().Be("B");
        rows[2].Should().BeEmpty();
        rows[3].Should().Be("C");
        rows.Skip(4).Should().OnlyContain(static s => s.Length == 0);
    }

    [Fact]
    public void Demo_Newline_At_Bottom_Scrolls()
    {
        IReadOnlyList<string> rows = RunCScreen("scr_nlscroll.c");

        rows[23].Should().Be("A");
        rows[24].Should().Be("B");
        rows.Take(23).Should().OnlyContain(static s => s.Length == 0);
    }
}
