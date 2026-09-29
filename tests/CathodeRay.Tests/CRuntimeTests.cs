using CathodeRay.Cli;
using CathodeRay.Stub;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 27 C: BSS ponad 256 B, wspólne helpery w wielu modułach, wydajność mnożenia.</summary>
public sealed class CRuntimeTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("cathode-rt-");

    public void Dispose() => _dir.Delete(recursive: true);

    private (int A, int X, long Steps) Link(params (string Name, string Source)[] files)
    {
        var args = new List<string> { "cc" };
        foreach ((string name, string source) in files)
        {
            string path = Path.Combine(_dir.FullName, name);
            File.WriteAllText(path, source);
            args.Add(path);
        }

        string bin = Path.Combine(_dir.FullName, "prog.bin");
        args.AddRange(["-o", bin]);
        var error = new StringWriter();
        int exit = CliApp.CreateRoot().Parse([.. args]).Invoke(new System.CommandLine.InvocationConfiguration { Output = new StringWriter(), Error = error });
        exit.Should().Be(0, error.ToString());
        byte[] image = File.ReadAllBytes(bin);
        var bus = new StubBus();
        for (int i = 0; i < image.Length; i++)
        {
            bus.Write((ushort)(0x1000 + i), image[i]);
        }

        var cpu = new StubCpu(StubIsa.FromJsonFile(Repo.IsaFile("mcp_stub_instructions.json")), bus);
        cpu.State.ProgramCounter = 0x1000;
        long steps = 0;
        while (!cpu.State.Halted)
        {
            (steps++).Should().BeLessThan(2_000_000, "program ma się zatrzymać");
            cpu.Step();
        }

        return (cpu.State.A, cpu.State.X, steps);
    }

    [Fact]
    public void Bss_Larger_Than_A_Page_Is_Cleared_And_Usable()
    {
        const string Source = """
            uchar big[600];
            int main() {
                big[599] = 7;
                big[300] = 5;
                return big[599] + big[300] + big[1] + big[299];
            }
            """;
        var (cpu, bus, _) = CCodegenTests.RunC(Source, poke: (bus, _) =>
        {
            bus.Write(0x7000 + 400, 0x55);
            bus.Write(0x7000 + 550, 0x66);
        });

        cpu.State.A.Should().Be(12);
        bus.Read(0x7000 + 400).Should().Be(0);
        bus.Read(0x7000 + 550).Should().Be(0);
    }

    [Fact]
    public void Linked_Program_Clears_Whole_Bss_Through_Linker_Symbol()
    {
        var (a, _, _) = Link(("a.c", "uchar big[700];\nint main() { big[650] = 4; return big[650] + big[10]; }\n"));

        a.Should().Be(4);
    }

    [Theory]
    [InlineData(200, 7)]
    [InlineData(255, 1)]
    [InlineData(255, 128)]
    [InlineData(255, 255)]
    [InlineData(128, 129)]
    [InlineData(250, 200)]
    [InlineData(0, 9)]
    [InlineData(17, 5)]
    public void Uchar_Divmod_Shift_Subtract_Matches_Reference(int a, int b)
    {
        var (cpu, _, _) = CCodegenTests.RunC($"int main() {{ uchar a = {a}; uchar b = {b}; return (a / b) * 3 + (a % b) * 5; }}");
        ((((a / b) * 3) + ((a % b) * 5)) & 0xFF).Should().Be(cpu.State.A);
    }

    [Theory]
    [InlineData(255, 255)]
    [InlineData(13, 17)]
    [InlineData(200, 2)]
    [InlineData(0, 77)]
    public void Uchar_Mul_Shift_Add_Is_Fast_And_Correct(int a, int b)
    {
        var (cpu, _, _) = CCodegenTests.RunC($"int main() {{ uchar a = {a}; uchar b = {b}; return a * b; }}");
        cpu.State.A.Should().Be((byte)((a * b) & 0xFF));
        cpu.CycleCount.Should().BeLessThan(3_000UL);
    }

    [Fact]
    public void Two_Modules_Can_Both_Use_Uchar_Mul_And_Div()
    {
        var (a, _, _) = Link(
            ("a.c", "uchar f(uchar x, uchar y) { return x / y + x * y; }\n"),
            ("b.c", "uchar f(uchar x, uchar y);\nint main() { uchar p = 9; uchar q = 4; return f(p, q) + p * q + p % q; }\n"));

        a.Should().Be((9 / 4) + (9 * 4) + (9 * 4) + (9 % 4));
    }

    [Fact]
    public void Deep_NonRecursive_Chain_Is_Rejected_At_Compile_Time()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("int f0(int a, int b) { int c = a + b; int d = c + 1; return d; }");
        for (int i = 1; i <= 130; i++)
        {
            sb.AppendLine($"int f{i}(int a, int b) {{ int c = a + b; int d = c + 1; return f{i - 1}(c, d); }}");
        }

        sb.AppendLine("int main() { return f130(1, 2); }");
        FluentActions.Invoking(() => CCodegenTests.RunC(sb.ToString()))
            .Should().Throw<CathodeRay.C.CCodegenException>().WithMessage("*of stack*");
    }

    [Fact]
    public void Recursion_Gets_A_Depth_Warning()
    {
        CathodeRay.C.CheckedProgram program = CathodeRay.C.TypeChecker.Check(CathodeRay.C.Parser.Parse(
            "int sum(int n) { if (n <= 0) return 0; return n + sum(n - 1); }\nint main() { return sum(5); }"));
        _ = CathodeRay.C.Codegen.Emit(program);

        program.Warnings.Should().Contain(w => w.Contains("'sum' is recursive") && w.Contains("nested calls"));
    }
}
