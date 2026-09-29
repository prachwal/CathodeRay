using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 30, kroki 1–2: rejestr celów, <c>cc --cpu</c> i kod pośredni pod spodem generatora.</summary>
public sealed class CTargetTests
{
    private const string Source = "int helper(int x) { return x + 1; }\nstatic int hidden() { return 2; }\nint main() { return helper(hidden()); }\n";

    private static CheckedProgram Checked() => TypeChecker.Check(Parser.Parse(Source));

    [Fact]
    public void Registry_Finds_Targets_Case_Insensitively_And_Exposes_Planned_Names()
    {
        CTargets.Find("STUB").Should().BeSameAs(CTargets.Default);
        CTargets.Find("6502").Should().BeNull();
        CTargets.All.Select(static t => t.Name).Should().Equal("stub");
        CTargets.Planned.Should().Contain(["6502", "65c02", "z80", "8080", "6800"]);
    }

    [Fact]
    public void Stub_Target_Describes_Itself_For_The_Driver()
    {
        ICTarget stub = CTargets.Default;

        stub.AssemblerCpu.Should().Be("stub");
        stub.ByteOrder.Should().Be(TargetByteOrder.Little);
        stub.StackLimit.Should().Be(256);
        stub.Crt0.Should().Be(Crt0.Source);
        stub.Layout.Areas.Select(static a => (a.Name, a.Start, a.Size)).Should().Equal(
            ("C_CODE", 0x1000, 0x5F00), ("C_INIT", 0x6F00, 0x100), ("C_BSS", 0x7000, 0x1000), ("C_DATA", 0x8000, 0x8000));
        stub.Layout.Segments.Select(static m => m.Name).Should().Equal("CODE", "INIT", "BSS", "DATA");
        stub.RuntimeModules.Should().OnlyContain(static m => m.IsAssembly).And.Contain(static m => m.Name == "io.s");
    }

    [Fact]
    public void Lowering_Groups_Functions_And_The_Target_Prints_Them_Unchanged()
    {
        IrModule module = Codegen.Lower(Checked(), "t.c", objectMode: true);

        module.Code.OfType<IrFunction>().Select(static f => (f.Name, f.IsStatic)).Should().Equal(("helper", false), ("hidden", true), ("main", false));
        module.Code.OfType<IrFunction>().Should().OnlyContain(static f => f.Body.All(static i => i is Raw));
        module.Data.Should().NotBeEmpty();
        module.Bss.Should().NotBeEmpty();

        foreach (bool optimize in new[] { false, true })
        {
            CTargets.Default.Emit(module, optimize).Should().Be(Codegen.Emit(Checked(), "t.c", objectMode: true, optimize: optimize));
        }
    }

    [Fact]
    public void Peephole_Belongs_To_The_Target_And_Runs_Only_When_Asked()
    {
        IrModule module = new([new Raw("STA x\nLDA x\nRET\n")], [], [], []);

        CTargets.Default.Emit(module, optimize: false).Should().Be("STA x\nLDA x\nRET\n");
        CTargets.Default.Emit(module, optimize: true).Should().Be("STA x\nRET\n");
    }

    [Fact]
    public void Unprintable_Items_Are_Rejected_By_A_Target()
    {
        FluentActions.Invoking(() => CTargets.Default.Emit(new IrModule([new Unknown()], [], [], []), optimize: false))
            .Should().Throw<InvalidOperationException>().WithMessage("*Unknown*");
    }

    [Fact]
    public void Driver_Accepts_Stub_And_Rejects_Unknown_And_Planned_Targets()
    {
        string dir = Directory.CreateTempSubdirectory("cathode-cpu-").FullName;
        try
        {
            string path = Path.Combine(dir, "a.c");
            File.WriteAllText(path, "int main() { return 7; }\n");
            string bin = Path.Combine(dir, "a.bin");

            (int okExit, string okError) = Cc("cc", path, "-o", bin, "--cpu", "stub");
            okExit.Should().Be(0, okError);

            (int plannedExit, string plannedError) = Cc("cc", path, "-o", bin, "--cpu", "6502");
            plannedExit.Should().NotBe(0);
            plannedError.Should().Contain("target not implemented yet '6502'").And.Contain("available: stub");

            (int unknownExit, string unknownError) = Cc("cc", path, "-o", bin, "--cpu", "pdp11");
            unknownExit.Should().NotBe(0);
            unknownError.Should().Contain("unknown target 'pdp11'");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static (int Exit, string Error) Cc(params string[] args)
    {
        var error = new StringWriter();
        int exit = CathodeRay.Cli.CliApp.CreateRoot().Parse(args).Invoke(new System.CommandLine.InvocationConfiguration { Output = new StringWriter(), Error = error });
        return (exit, error.ToString());
    }

    private sealed record Unknown : IrItem;
}
