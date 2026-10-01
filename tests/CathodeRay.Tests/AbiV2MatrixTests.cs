using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 39, zadanie 3: matryca różnicowa ABI v2 — każdy sample na <c>nes</c> (--ir vreg --abi v2)
/// daje wynik i konsolę takie same jak na stubie (ten sam oracle co <c>TargetMatrixTests</c>).
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
        CcRun.Result actual = CcRun.RunOn(source, "nes", "--incdir", Repo.Path("samples", "minic"), "--ir", "vreg", "--abi", "v2");

        actual.Value.Should().Be(expected.Value, $"{file} na nes/v2");
        actual.Console.Should().Be(expected.Console, $"{file} na nes/v2");
    }
}
