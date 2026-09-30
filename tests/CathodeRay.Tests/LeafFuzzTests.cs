using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Test różnicowy liści z parametrami z <c>tools/leaf_fuzz.py</c>: włączony zmienną <c>LEAF_FUZZ_DIR</c> (katalog z programami i <c>gcc.txt</c>);
/// wynik naszego kompilatora na każdym celu musi się zgadzać z gcc.</summary>
public sealed class LeafFuzzTests
{
    [Fact]
    public void Leaf_Programs_Match_Gcc()
    {
        string? directory = Environment.GetEnvironmentVariable("LEAF_FUZZ_DIR");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        Dictionary<string, int> expected = File.ReadAllLines(Path.Combine(directory, "gcc.txt"))
            .Where(static l => l.Length > 0)
            .Select(static l => l.Split(' '))
            .ToDictionary(static p => p[0], static p => int.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture));
        var mismatches = new List<string>();
        foreach (string cpu in TargetHarness.Targets.Select(static t => t.Name))
        {
            foreach ((string name, int value) in expected)
            {
                int actual = CcRun.RunOn(File.ReadAllText(Path.Combine(directory, name + "_ours.c")), cpu).Value;
                if (actual != value)
                {
                    mismatches.Add($"{cpu} {name}: {actual} zamiast {value}");
                }
            }
        }

        mismatches.Should().BeEmpty(string.Join("; ", mismatches));
    }
}
