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
        var mismatches = new System.Collections.Concurrent.ConcurrentBag<string>();
        var work = TargetHarness.Targets.Select(static t => t.Name).SelectMany(cpu => expected.Select(item => (Cpu: cpu, item.Key, item.Value))).ToList();
        Parallel.ForEach(work, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, item =>
        {
            try
            {
                int actual = CcRun.RunOn(File.ReadAllText(Path.Combine(directory, item.Key + "_ours.c")), item.Cpu).Value;
                if (actual != item.Value)
                {
                    mismatches.Add($"{item.Cpu} {item.Key}: {actual} zamiast {item.Value}");
                }
            }
            catch (Exception e)
            {
                mismatches.Add($"{item.Cpu} {item.Key}: wyjątek {e.GetType().Name}: {e.Message.Split('\n')[0]}");
            }
        });

        mismatches.Order().Should().BeEmpty();
    }
}
