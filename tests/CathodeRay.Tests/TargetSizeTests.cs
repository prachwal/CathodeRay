using System.Globalization;
using System.Text;
using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 30, krok 20: tabela rozmiaru kodu (segment CODE w bajtach) każdego programu z <c>samples/minic</c> na każdym celu.
/// Golden pilnuje tylko rozmiaru — poprawność sprawdza <see cref="TargetMatrixTests"/>. Odświeżenie po zamierzonej zmianie generatora:
/// <c>UPDATE_TARGET_SIZES=1 dotnet test --filter TargetSizeTests</c> (zapisuje też <c>docs/target-sizes.md</c>).</summary>
public sealed class TargetSizeTests
{
    [Fact]
    public void Code_Sizes_Match_The_Recorded_Table()
    {
        string[] files = [.. Directory.GetFiles(Repo.Path("samples", "minic"), "??_*.c").Select(Path.GetFileName).Order(StringComparer.Ordinal)!];
        string[] targets = [.. CTargets.All.Select(static t => t.Name)];
        var lines = new List<string>();
        var table = new StringBuilder();
        table.AppendLine($"| program | {string.Join(" | ", targets)} |");
        table.AppendLine($"| --- |{string.Concat(targets.Select(static _ => " ---: |"))}");
        foreach (string file in files)
        {
            string source = File.ReadAllText(Repo.Path("samples", "minic", file));
            var row = new List<string>();
            foreach (string target in targets)
            {
                int code = CcRun.Sizes(source, target, "--incdir", Repo.Path("samples", "minic"))["CODE"];
                lines.Add($"{file} {target} {code}");
                row.Add(code.ToString(CultureInfo.InvariantCulture));
            }

            table.AppendLine($"| {file} | {string.Join(" | ", row)} |");
        }

        string golden = Repo.Path("tests", "CathodeRay.Tests", "target-sizes.txt");
        string actual = string.Join('\n', lines) + "\n";
        if (Environment.GetEnvironmentVariable("UPDATE_TARGET_SIZES") == "1")
        {
            File.WriteAllText(golden, actual);
            File.WriteAllText(Repo.Path("docs", "target-sizes.md"), "# Rozmiar kodu na celach\n\nSegment `CODE` w bajtach po linkowaniu z biblioteką standardową (`cathode cc --stats`); tabelę odświeża\n`UPDATE_TARGET_SIZES=1 dotnet test --filter TargetSizeTests`. Poprawność wyników sprawdza `TargetMatrixTests`.\n\n" + table);
            return;
        }

        File.ReadAllText(golden).Should().Be(actual, "rozmiar kodu zmienił się — jeśli zamierzenie, odśwież tabelę (UPDATE_TARGET_SIZES=1)");
    }

    [Fact]
    public void Code_Size_Without_Optimization_Is_Greater_Or_Equal()
    {
        string source = File.ReadAllText(Repo.Path("samples", "minic", "01_types.c"));
        int optimized = CcRun.Sizes(source, "6502", "--incdir", Repo.Path("samples", "minic"))["CODE"];
        int unoptimized = CcRun.Sizes(source, "6502", "--incdir", Repo.Path("samples", "minic"), "-O0")["CODE"];
        unoptimized.Should().BeGreaterThanOrEqualTo(optimized, "kod bez optymalizacji powinien być >= zoptymalizowany");
    }
}
