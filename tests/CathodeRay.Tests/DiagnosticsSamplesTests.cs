using System.CommandLine;
using System.Text.RegularExpressions;
using CathodeRay.Cli;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Programy z <c>samples/diag</c>: każdy opisuje w komentarzach <c>// expect-…</c> wynik i diagnostykę
/// (zob. <c>samples/diag/README.md</c>); test kompiluje je z <c>-v --emit-ir --dump-regalloc</c> i uruchamia przez <c>run</c>.</summary>
public sealed partial class DiagnosticsSamplesTests : IDisposable
{
    private static readonly string[] Cpus = ["z80", "8080", "6502", "6800", "6809"];

    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("cathode-diag-samples-");

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (string file in Directory.GetFiles(Repo.Path("samples", "diag"), "*.c").Order(StringComparer.Ordinal))
        {
            foreach (string cpu in Cpus)
            {
                data.Add(System.IO.Path.GetFileName(file), cpu);
            }
        }

        return data;
    }

    public void Dispose() => _dir.Delete(recursive: true);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Sample_Compiles_Runs_And_Matches_Its_Expectations(string sample, string cpu)
    {
        string source = Repo.Path("samples", "diag", sample);
        ILookup<string, string> expect = Directive().Matches(File.ReadAllText(source))
            .ToLookup(static m => m.Groups[1].Value, static m => m.Groups[2].Value.Trim());
        string bin = System.IO.Path.Combine(_dir.FullName, "p.bin");
        string lst = System.IO.Path.Combine(_dir.FullName, "p.lst");
        string ir = System.IO.Path.Combine(_dir.FullName, "p.ir");
        string ra = System.IO.Path.Combine(_dir.FullName, "p.ra");

        var (compileExit, _, compileErr) = Cli("cc", source, "-o", bin, "-l", lst, "--cpu", cpu, "-v", "--emit-ir", ir, "--dump-regalloc", ra);

        compileExit.Should().Be(0, compileErr);
        foreach (string text in expect["warning"])
        {
            compileErr.Should().Contain(text);
        }

        foreach (string text in expect["verbose"])
        {
            compileErr.Should().Contain("cc: ").And.Contain(text);
        }

        string irText = File.ReadAllText(ir);
        foreach (string text in expect["ir"])
        {
            irText.Should().Contain(text);
        }

        foreach (string text in expect["ir-absent"])
        {
            irText.Should().NotContain(text);
        }

        if (cpu is "z80" or "8080")
        {
            foreach (string text in expect["regalloc"])
            {
                File.ReadAllText(ra).Should().Contain(text);
            }
        }

        string[] runArgs = expect["exit"].Any() ? ["--max-steps", "2000"] : [];
        var (runExit, runOut, runErr) = Cli(["run", bin, "--cpu", cpu, "-l", lst, .. runArgs]);

        runExit.Should().Be(expect["exit"].Any() ? int.Parse(expect["exit"].First(), System.Globalization.CultureInfo.InvariantCulture) : 0, runErr);
        foreach (string value in expect["result"])
        {
            runOut.Should().Contain($"result: {value} (");
        }

        foreach (string text in expect["result-text"])
        {
            runOut.Should().Contain(text);
        }

        foreach (string text in expect["stderr"])
        {
            runErr.Should().Contain(text);
        }
    }

    private static (int Exit, string Out, string Err) Cli(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exit = CliApp.CreateRoot().Parse(args).Invoke(new InvocationConfiguration { Output = output, Error = error });
        return (exit, output.ToString(), error.ToString());
    }

    [GeneratedRegex(@"^// expect-([a-z-]+):(.*)$", RegexOptions.Multiline)]
    private static partial Regex Directive();
}
