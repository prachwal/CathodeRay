using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 41, poz. 7: typowany <see cref="AsmInsn"/> czyta 100% korpusu (próbki × cele z siatki,
/// w tym nes --abi v2), a złożenie wraca do identycznego tekstu. Podkład pod migrację Size/Tidy/Relax (poz. 8).</summary>
public sealed class AsmInsnTests
{
    public static TheoryData<string, string, bool> Cases()
    {
        var data = new TheoryData<string, string, bool>();
        string[] files = [.. Directory.GetFiles(Repo.Path("samples", "minic"), "??_*.c").Select(Path.GetFileName).Order(StringComparer.Ordinal)!];
        foreach (string cpu in new[] { "stub", "6502", "65c02", "nes", "6510", "z80", "8080", "6800" })
        {
            foreach (string file in files)
            {
                data.Add(file, cpu, false);
            }
        }

        foreach (string file in files)
        {
            data.Add(file, "nes", true);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Round_Trip_Is_Identity_On_Grid_Corpus(string file, string cpu, bool abiV2)
    {
        string dir = Repo.Path("samples", "minic");
        string source = File.ReadAllText(Path.Combine(dir, file));
        string? Reader(string path) => File.Exists(Path.Combine(dir, path)) ? File.ReadAllText(Path.Combine(dir, path)) : StdLib.HeaderReader(path);
        CheckedProgram program = TypeChecker.Check(Parser.Parse(source, Reader));
        string asm = Codegen.Emit(program, CTargets.Find(cpu)!, file, objectMode: true, abiV2: abiV2);

        AsmInsn[] lines = AsmInsn.ParseAll(asm);

        lines.Should().NotBeEmpty($"korpus {file}/{cpu} nie jest pusty");
        AsmInsn.EmitAll(lines).Should().Be(asm, $"round-trip {file}/{cpu}/v2={abiV2}");
    }

    [Fact]
    public void Comment_Inside_Quotes_Does_Not_Split()
    {
        AsmInsn[] lines = AsmInsn.ParseAll("SEGMENT \"A;B\"\n");

        lines.Should().ContainSingle();
        lines[0].Mnemonic.Should().Be("SEGMENT");
        lines[0].Operands.Should().Be("\"A;B\"");
        lines[0].Comment.Should().BeNull();
        AsmInsn.EmitAll(lines).Should().Be("SEGMENT \"A;B\"\n");
    }

    [Fact]
    public void Label_With_Instruction_And_Comment_Splits()
    {
        AsmInsn[] lines = AsmInsn.ParseAll("L: ld a,0 ; zero\n");

        lines.Should().ContainSingle();
        lines[0].Label.Should().Be("L:");
        lines[0].Mnemonic.Should().Be("ld");
        lines[0].Operands.Should().Be("a,0 ");
        lines[0].Comment.Should().Be("; zero");
        AsmInsn.EmitAll(lines).Should().Be("L: ld a,0 ; zero\n");
    }
}
