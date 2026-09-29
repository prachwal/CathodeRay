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
        CTargets.Find("z80").Should().BeNull();
        CTargets.Find("6502")!.Name.Should().Be("6502");
        CTargets.All.Select(static t => t.Name).Should().StartWith("stub");
        CTargets.Planned.Should().Contain(["z80", "8080", "6800"]);
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
    public void Lowering_Produces_Typed_Functions_And_Data()
    {
        Ir.Module module = Codegen.Lower(Checked(), "t.c", objectMode: true);

        module.Functions.Select(static f => (f.Name, f.IsStatic, f.RetW)).Should().Equal(("helper", false, 2), ("hidden", true, 2), ("main", false, 2));
        module.Functions[0].Params.Should().Equal([new Ir.Cell("helper__x", 2)]);
        module.Functions[0].Body.OfType<Ir.Bin>().Select(static b => b.Kind).Should().Equal(Ir.BinOp.Add);
        module.Functions[0].Body.OfType<Ir.Ret>().Should().HaveCount(1);
        module.Functions[2].Body.OfType<Ir.Call>().Select(static c => c.Direct).Should().Equal("hidden", "helper");
        module.Data.Should().OnlyContain(static d => d.Segment == "BSS").And.Contain(static d => d.Sym == "helper__x" && d.Size == 2);
        module.ObjectMode.Should().BeTrue();
    }

    [Fact]
    public void Target_Prints_The_Module_And_Optimization_Is_The_Targets_Choice()
    {
        Ir.Module module = Codegen.Lower(Checked(), "t.c", objectMode: true);
        string plain = CTargets.Default.Emit(module, optimize: false);
        string tuned = CTargets.Default.Emit(module, optimize: true);

        plain.Should().Contain(".proc helper").And.Contain(".proc main").And.Contain("CALL helper");
        tuned.Length.Should().BeLessThan(plain.Length);
        plain.Should().Be(Codegen.Emit(Checked(), "t.c", objectMode: true, optimize: false));
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

            (int plannedExit, string plannedError) = Cc("cc", path, "-o", bin, "--cpu", "z80");
            plannedExit.Should().NotBe(0);
            plannedError.Should().Contain("target not implemented yet 'z80'").And.Contain("available: stub");

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
}
