using CathodeRay;
using CathodeRay.Assembler;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Trwały test segmentów: każdy katalog w <c>Segments/</c> (wzorcowany przez
/// <c>tools/make_segments_golden.py</c>) składany naszym asemblerem z mapą z <c>map.txt</c>,
/// porównywany z expected.bin (złożonym referencją).</summary>
public sealed class SegmentsTests
{
    public static TheoryData<string> Names()
    {
        var data = new TheoryData<string>();
        foreach (string dir in Directory.GetDirectories(Repo.Path("tests", "CathodeRay.Tests", "Segments")).Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(dir));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Segments_Match_Reference(string cpu)
    {
        string dir = Repo.Path("tests", "CathodeRay.Tests", "Segments", cpu);
        string main = Path.Combine(dir, "main.s");
        var origins = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in File.ReadAllLines(Path.Combine(dir, "map.txt")))
        {
            int at = line.LastIndexOf('@');
            NumberLiteral.TryParse(line[(at + 1)..], out int address).Should().BeTrue($"map.txt: {line}");
            origins[line[..at]] = address;
        }

        AssemblerTarget target = AssemblerTargets.Find(cpu)!;
        var assembler = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax);
        static string? Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

        assembler.Assemble(File.ReadAllText(main), main, Read, [], null, origins).Image
            .Should().Equal(
                File.ReadAllBytes(Path.Combine(dir, "expected.bin")),
                $"Segments/{cpu}/main.s ma dać te same bajty co referencja");
    }
}
