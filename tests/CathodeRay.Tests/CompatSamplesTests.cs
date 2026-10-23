using System.Text.RegularExpressions;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Programy z <c>samples/compat</c>: jeden na funkcjonalność dodaną w planie 48. Oczekiwania są w komentarzach
/// <c>// expect-…</c> (zob. <c>samples/compat/README.md</c>); program jest kompilowany i wykonywany na emulatorze celu.</summary>
public sealed partial class CompatSamplesTests
{
    private static readonly string[] DefaultCpus = ["z80", "8080", "6502", "6800", "6809"];

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (string file in Directory.GetFiles(Repo.Path("samples", "compat"), "*.c").Order(StringComparer.Ordinal))
        {
            string text = File.ReadAllText(file);
            Match cpus = Cpus().Match(text);
            foreach (string cpu in cpus.Success ? cpus.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries) : DefaultCpus)
            {
                data.Add(Path.GetFileName(file), cpu);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Sample_Compiles_Runs_And_Matches_Its_Expectations(string sample, string cpu)
    {
        string source = File.ReadAllText(Repo.Path("samples", "compat", sample));
        ILookup<string, string> expect = Directive().Matches(source).ToLookup(static m => m.Groups[1].Value, static m => m.Groups[2].Value.Trim());
        string[] args = [.. expect["args"].SelectMany(static a => a.Split(' ', StringSplitOptions.RemoveEmptyEntries))];

        CcRun.Result result = CcRun.RunOn(source, cpu, args);

        foreach (string value in expect["result"])
        {
            int wanted = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            ((short)result.Value).Should().Be((short)wanted, $"{sample} on {cpu}");
        }

        foreach (string text in expect["console"])
        {
            result.Console.Should().Be(text.Replace("\\n", "\n", StringComparison.Ordinal), $"{sample} on {cpu}");
        }
    }

    [GeneratedRegex(@"^// expect-([a-z]+):(.*)$", RegexOptions.Multiline)]
    private static partial Regex Directive();

    [GeneratedRegex(@"^// cpus:(.*)$", RegexOptions.Multiline)]
    private static partial Regex Cpus();
}
