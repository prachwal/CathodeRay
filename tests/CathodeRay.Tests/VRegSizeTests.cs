using System.Globalization;
using System.Text;
using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 37, zadanie 8: tabela rozmiaru kodu ścieżki <c>--ir vreg</c> (osobny golden — bramka Cell w
/// <c>TargetSizeTests</c> nie może drgnąć). Odświeżenie: <c>UPDATE_VREG_SIZES=1 dotnet test --filter VRegSizeTests</c>
/// (zapisuje też <c>docs/vreg-sizes.md</c>).</summary>
public sealed class VRegSizeTests
{
    [Fact]
    public void Vreg_Code_Sizes_Match_The_Recorded_Table()
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
                int code = CcRun.Sizes(source, target, "--incdir", Repo.Path("samples", "minic"), "--ir", "vreg")["CODE"];
                lines.Add($"{file} {target} {code}");
                row.Add(code.ToString(CultureInfo.InvariantCulture));
            }

            table.AppendLine($"| {file} | {string.Join(" | ", row)} |");
        }

        string golden = Repo.Path("tests", "CathodeRay.Tests", "vreg-sizes.txt");
        string actual = string.Join('\n', lines) + "\n";
        if (Environment.GetEnvironmentVariable("UPDATE_VREG_SIZES") == "1")
        {
            File.WriteAllText(golden, actual);
            File.WriteAllText(Repo.Path("docs", "vreg-sizes.md"), "# Rozmiar kodu ścieżki VReg na celach\n\nSegment `CODE` w bajtach po linkowaniu z biblioteką standardową (`cathode cc --stats --ir vreg`); osobny golden obok bramki Cell (`TargetSizeTests`). Tabelę odświeża\n`UPDATE_VREG_SIZES=1 dotnet test --filter VRegSizeTests`.\n\nStan przy wprowadzeniu (plan 37): 48/192 pól różni się od Cell, zwykle w dół (np. `05_calls.c` 521→353 B na 6502 — druga runda CSE/DCE po podniesieniu); wyjątki w górę na Z80/8080 (`04_int_ops.c` +21/+27 B, `23_types.c` +44/+27 B — fazowanie z `RegisterAllocator`, do zbadania osobno). Domyślna ścieżka Cell nie drgnęła.\n\n" + table);
            return;
        }

        File.ReadAllText(golden).Should().Be(actual, "rozmiar kodu vreg zmienił się — jeśli zamierzenie, odśwież tabelę (UPDATE_VREG_SIZES=1)");
    }
}
