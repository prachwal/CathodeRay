using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 37, zadanie 5: cała matryca <c>samples/minic</c> ścieżką <c>--ir vreg</c> zwraca to samo
/// co ścieżka Cell na stubie (wołania i ABI idą adapterem bez zmian).</summary>
public sealed class VRegMatrixTests
{
    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        string[] files = [.. Directory.GetFiles(Repo.Path("samples", "minic"), "??_*.c").Select(Path.GetFileName).Order(StringComparer.Ordinal)!];
        foreach (ICTarget target in TargetHarness.Targets.Where(static t => t.Name != "stub"))
        {
            foreach (string file in files)
            {
                data.Add(target.Name, file);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Sample_Matches_The_Cell_Stub(string cpu, string file)
    {
        string source = File.ReadAllText(Repo.Path("samples", "minic", file));
        CcRun.Result expected = CcRun.RunOn(source, "stub", "--incdir", Repo.Path("samples", "minic"));
        CcRun.Result actual = CcRun.RunOn(source, cpu, "--incdir", Repo.Path("samples", "minic"), "--ir", "vreg");

        actual.Value.Should().Be(expected.Value, $"{file} na {cpu} --ir vreg");
        actual.Console.Should().Be(expected.Console, $"{file} na {cpu} --ir vreg");
    }
}
