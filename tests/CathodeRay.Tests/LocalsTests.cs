using CathodeRay.Assembler;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Trwały test tanich etykiet (@) i .incbin: każdy katalog w <c>Locals/</c>
/// (wzorcowany przez <c>tools/make_locals_golden.py</c>) składany naszym asemblerem
/// z kontekstem pliku, porównywany z expected.bin (złożonym referencją).</summary>
public sealed class LocalsTests
{
    public static TheoryData<string> Names()
    {
        var data = new TheoryData<string>();
        foreach (string dir in Directory.GetDirectories(Repo.Path("tests", "CathodeRay.Tests", "Locals")).Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(dir));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Locals_And_Incbin_Match_Reference(string cpu)
    {
        string dir = Repo.Path("tests", "CathodeRay.Tests", "Locals", cpu);
        string main = Path.Combine(dir, "main.s");
        AssemblerTarget target = AssemblerTargets.Find(cpu)!;
        var assembler = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax);
        static string? ReadText(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
        static byte[]? ReadBinary(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;

        assembler.Assemble(File.ReadAllText(main), main, ReadText, [], ReadBinary).Image
            .Should().Equal(
                File.ReadAllBytes(Path.Combine(dir, "expected.bin")),
                $"Locals/{cpu}/main.s ma dać te same bajty co referencja");
    }
}
