using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Matryca różnicowa ABI v2 — każdy sample na <c>nes</c> i 6809 daje wynik i konsolę takie same jak na stubie
/// (ten sam oracle co <c>TargetMatrixTests</c>).
/// Lekcja: helpery C stdlib kompilowane są zawsze jako v1 (caller marszuje je przez pamięć), inaczej
/// prologi W1/W2 czytałyby rejestry, których nikt nie ustawił (12/24 padów przed fixem).</summary>
public sealed class AbiV2MatrixTests
{
    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        string[] files = [.. Directory.GetFiles(Repo.Path("samples", "minic"), "??_*.c").Select(Path.GetFileName).Order(StringComparer.Ordinal)!];
        foreach (string file in files)
        {
            data.Add(file);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Sample_Matches_The_Stub_On_V2(string file)
    {
        string source = File.ReadAllText(Repo.Path("samples", "minic", file));
        CcRun.Result expected = CcRun.RunOn(source, "stub", "--incdir", Repo.Path("samples", "minic"));
        (string Cpu, string[] Options)[] targets =
        [
            ("nes", ["--ir", "vreg", "--abi", "v2"]),
            ("6809", ["--abi", "v2"]),
            ("6809", ["--ir", "vreg", "--abi", "v2"]),
        ];

        foreach ((string cpu, string[] options) in targets)
        {
            CcRun.Result actual = CcRun.RunOn(source, cpu, ["--incdir", Repo.Path("samples", "minic"), .. options]);
            actual.Value.Should().Be(expected.Value, $"{file} na {cpu}/v2/{string.Join(' ', options)}");
            actual.Console.Should().Be(expected.Console, $"{file} na {cpu}/v2/{string.Join(' ', options)}");
        }
    }
}
