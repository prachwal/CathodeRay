using CathodeRay.Assembler;
using CathodeRay.Assembler.Link;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Trwały test linkera: każdy katalog w <c>Modules/</c> (wzorcowany przez
/// <c>tools/make_modules_golden.py</c>) składany naszym asemblerem do obiektów
/// (<c>-f obj</c>) i łączony komendą linkera z <c>map.cfg</c>, porównywany
/// z expected.bin (złożonym referencją: ca65+ld65, z80asm).</summary>
public sealed class ModulesTests
{
    public static TheoryData<string> Names()
    {
        var data = new TheoryData<string>();
        foreach (string dir in Directory.GetDirectories(Repo.Path("tests", "CathodeRay.Tests", "Modules")).Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(dir));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Linked_Modules_Match_Reference(string cpu)
    {
        string dir = Repo.Path("tests", "CathodeRay.Tests", "Modules", cpu);
        AssemblerTarget target = AssemblerTargets.Find(cpu)!;
        var assembler = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax);
        static string? Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

        var modules = new List<(string File, ObjectModule Module)>();
        for (int i = 0; ; i++)
        {
            string mod = Path.Combine(dir, $"mod{i}.s");
            if (!File.Exists(mod))
            {
                break;
            }

            modules.Add(($"mod{i}.o", assembler.AssembleObject(cpu, File.ReadAllText(mod), mod, Read)));
        }

        modules.Should().NotBeEmpty($"Modules/{cpu} ma zawierać moduły");
        LinkerConfig config = LinkerConfig.Parse(File.ReadAllText(Path.Combine(dir, "map.cfg")));

        Linker.Link(modules, config).Image.Should().Equal(
            File.ReadAllBytes(Path.Combine(dir, "expected.bin")),
            $"Modules/{cpu} ma dać te same bajty co referencja");
    }
}
