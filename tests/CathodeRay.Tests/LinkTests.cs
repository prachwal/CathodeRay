using CathodeRay.Assembler;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Trwały test linkowania (.include): każdy katalog w <c>Link/</c> (wzorcowane przez
/// <c>tools/make_link_golden.py</c>) to main.s + partN.inc; nasz asembler z kontekstem pliku
/// ma dać bajty identyczne z expected.bin (złożonym referencją: ca65/z80asm).</summary>
public sealed class LinkTests
{
    private static readonly IReadOnlyDictionary<string, (string Cpu, string? Syntax)> Cases =
        new Dictionary<string, (string, string?)>(StringComparer.Ordinal)
        {
            ["6502"] = ("6502", null),
            ["6502-mos"] = ("6502", "mos"),
            ["6502x"] = ("6502x", null),
            ["65c02"] = ("65c02", null),
            ["8080"] = ("8080", null),
            ["z80"] = ("z80", null),
            ["z80u"] = ("z80u", null),
            ["6502-nested"] = ("6502", null),
            ["stub"] = ("stub", null),
        };

    public static TheoryData<string> Names()
    {
        var data = new TheoryData<string>();
        foreach (string dir in Directory.GetDirectories(Repo.Path("tests", "CathodeRay.Tests", "Link")).Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(dir));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Linked_Binary_Matches_Reference(string name)
    {
        (string cpu, string? syntax) = Cases[name];
        string dir = Repo.Path("tests", "CathodeRay.Tests", "Link", name);
        string main = Path.Combine(dir, "main.s");
        AssemblerTarget target = AssemblerTargets.Find(cpu)!;
        var assembler = new TwoPassAssembler(
            Repo.LoadTarget(target),
            syntax is null ? target.DefaultSyntax : target.FindSyntax(syntax)!);
        static string? Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

        byte[] expected = name == "stub"
            ? assembler.Assemble(File.ReadAllText(Path.Combine(dir, "flat.s"))).Image
            : File.ReadAllBytes(Path.Combine(dir, "expected.bin"));

        assembler.Assemble(File.ReadAllText(main), main, Read).Image
            .Should().Equal(expected, $"{name}/main.s ma dać te same bajty co referencja");
    }
}
