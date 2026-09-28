using CathodeRay.Assembler;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Porównanie z oryginalnymi asemblerami: wzorce w <c>Asm/&lt;cpu&gt;</c> generuje <c>tools/make_asm_golden.py</c>
/// (6502/6502x/65c02 z ca65, 8080 z z80asm). <c>X.s</c> + <c>X.bin</c> = binarka wzorcowa (dialekt domyślny celu);
/// <c>X_mos.s</c> = ten sam program w składni MOS, porównywany z <c>X.bin</c>; <c>rejected.txt</c> = linie odrzucane przez wzorzec.</summary>
public sealed class AsmGoldenTests
{
    public static TheoryData<string, string> Sources()
    {
        var data = new TheoryData<string, string>();
        foreach (string dir in Directory.GetDirectories(Repo.Path("tests", "CathodeRay.Tests", "Asm")).Order(StringComparer.Ordinal))
        {
            foreach (string file in Directory.GetFiles(dir, "*.s").Order(StringComparer.Ordinal))
            {
                data.Add(Path.GetFileName(dir), Path.GetFileName(file));
            }
        }

        return data;
    }

    public static TheoryData<string, string> Rejected()
    {
        var data = new TheoryData<string, string>();
        foreach (string dir in Directory.GetDirectories(Repo.Path("tests", "CathodeRay.Tests", "Asm")).Order(StringComparer.Ordinal))
        {
            string file = Path.Combine(dir, "rejected.txt");
            foreach (string line in File.Exists(file) ? File.ReadAllLines(file).Where(static l => l.Length > 0) : [])
            {
                data.Add(Path.GetFileName(dir), line);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void Matches_Reference_Binary(string cpu, string file)
    {
        string path = Repo.Path("tests", "CathodeRay.Tests", "Asm", cpu, file);
        bool mos = file.EndsWith("_mos.s", StringComparison.Ordinal);
        string expectedFile = mos ? path.Replace("_mos.s", ".bin", StringComparison.Ordinal) : Path.ChangeExtension(path, ".bin");

        AssemblyResult result = Repo.Assemble(cpu, File.ReadAllText(path), mos ? "mos" : null);

        result.Image.Should().Equal(File.ReadAllBytes(expectedFile), $"{cpu}/{file} ma dać te same bajty co oryginalny asembler");
    }

    [Theory]
    [MemberData(nameof(Rejected))]
    public void Rejects_What_Reference_Rejects(string cpu, string line)
    {
        FluentActions.Invoking(() => Repo.Assemble(cpu, $".org $0600\n{line}"))
            .Should().Throw<AssemblerException>($"wzorzec dla {cpu} odrzuca '{line}'");
    }
}
