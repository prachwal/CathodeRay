using System.CommandLine;
using CathodeRay.Cli;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Tryb <c>cc -S</c>: tylko C→asembler, bez asemblacji i linkowania.</summary>
public sealed class AsmOnlyCliTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("cathode-asm-only-");

    public void Dispose() => _dir.Delete(recursive: true);

    private static (int Exit, string Out, string Err) Cli(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exit = CliApp.CreateRoot().Parse(args).Invoke(new InvocationConfiguration { Output = output, Error = error });
        return (exit, output.ToString(), error.ToString());
    }

    private string Write(string name, string content)
    {
        string path = Path.Combine(_dir.FullName, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Theory]
    [InlineData("z80")]
    [InlineData("6502")]
    [InlineData("6809")]
    public void Dash_S_Writes_Assembly_Next_To_The_Source_And_Nothing_Else(string cpu)
    {
        string source = Write("p.c", "int add(int a, int b) { return a + b; }\nint main(void) { return add(2, 3); }\n");

        var (exit, output, err) = Cli("cc", source, "-S", "--cpu", cpu);

        exit.Should().Be(0, err);
        output.Should().Contain("p.c -> p.s").And.Contain($"lines of {cpu} assembly");
        string asm = File.ReadAllText(Path.Combine(_dir.FullName, "p.s"));
        asm.Should().Contain("add:").And.Contain("main:");
        File.Exists(Path.Combine(_dir.FullName, "p.bin")).Should().BeFalse();
    }

    [Fact]
    public void Dash_S_Succeeds_For_A_Program_Too_Big_To_Assemble()
    {
        // ~9000 niezależnych instrukcji: po zasemblowaniu przekraczają 64 KB, ale jako asembler się generują
        string body = string.Concat(Enumerable.Range(0, 4000).Select(static i => $"    total += table[{i % 64}] * {i + 3};\n"));
        string source = Write("big.c", $"int table[64];\nint total;\nint main(void)\n{{\n{body}    return total;\n}}\n");

        var (asmExit, _, asmErr) = Cli("cc", source, "-S", "--cpu", "z80");
        var (fullExit, _, fullErr) = Cli("cc", source, "-o", Path.Combine(_dir.FullName, "big.bin"), "--cpu", "z80");

        asmExit.Should().Be(0, asmErr);
        fullExit.Should().NotBe(0);
        fullErr.Should().Contain("$FFFF");
    }

    [Fact]
    public void Dash_S_Writes_Nothing_When_Werror_Fails_Or_A_Later_Input_Is_Broken()
    {
        // przypisanie w warunku daje ostrzeżenie kompilatora
        string warns = Write("warns.c", "int main(void) { int a = 0; if (a = 1) { return 1; } return 0; }\n");
        string good = Write("good.c", "int main(void) { return 2; }\n");
        string broken = Write("broken.c", "int f(void) { return nope; }\n");

        Cli("cc", warns, "-S", "-Werror", "--cpu", "z80").Exit.Should().Be(1);
        File.Exists(Path.Combine(_dir.FullName, "warns.s")).Should().BeFalse();

        Cli("cc", good, broken, "-S", "--cpu", "z80").Exit.Should().Be(1);
        File.Exists(Path.Combine(_dir.FullName, "good.s")).Should().BeFalse();
        Directory.GetFiles(_dir.FullName, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public void Dash_S_Reports_Type_Errors_And_Honors_Output_And_Werror()
    {
        string bad = Write("bad.c", "int main(void) { return nope; }\n");
        Cli("cc", bad, "-S", "--cpu", "z80").Exit.Should().Be(1);

        string ok = Write("ok.c", "int main(void) { return 1; }\n");
        string chosen = Path.Combine(_dir.FullName, "named.asm");
        Cli("cc", ok, "-S", "-o", chosen, "--cpu", "z80").Exit.Should().Be(0);
        File.Exists(chosen).Should().BeTrue();

        Cli("cc", ok, ok, "-S", "-o", chosen).Exit.Should().Be(1);
        Cli("cc", Write("x.s", "nop\n"), "-S").Exit.Should().Be(1);
    }
}
