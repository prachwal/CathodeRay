using System.Globalization;
using System.Text;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 39, zadanie 3: tabela rozmiaru kodu ABI v2 na <c>nes</c> (osobny golden, wzór <c>VRegSizeTests</c>).
/// Kolumny v1/v2 (CODE po linkowaniu, <c>--ir vreg</c>) + delta; bramka planu (-15% na wołaniowych) NIE przeszła —
/// tabela zamraża baseline pod rewizję D2 (bookends w stopce, ParamAlias rejestrowy). Odświeżenie:
/// <c>UPDATE_V2_SIZES=1 dotnet test --filter AbiV2SizeTests</c> (zapisuje też <c>docs/v2-sizes.md</c>).</summary>
public sealed class AbiV2SizeTests
{
    [Fact]
    public void V2_Code_Sizes_Match_The_Recorded_Table()
    {
        string[] files = [.. Directory.GetFiles(Repo.Path("samples", "minic"), "??_*.c").Select(Path.GetFileName).Order(StringComparer.Ordinal)!];
        var lines = new List<string>();
        var table = new StringBuilder();
        table.AppendLine("| program | v1 | v2 | delta |");
        table.AppendLine("| --- | ---: | ---: | ---: |");
        foreach (string file in files)
        {
            string source = File.ReadAllText(Repo.Path("samples", "minic", file));
            int v1 = CcRun.Sizes(source, "nes", "--incdir", Repo.Path("samples", "minic"), "--ir", "vreg")["CODE"];
            int v2 = CcRun.Sizes(source, "nes", "--incdir", Repo.Path("samples", "minic"), "--ir", "vreg", "--abi", "v2")["CODE"];
            lines.Add($"{file} v1 {v1}");
            lines.Add($"{file} v2 {v2}");
            table.AppendLine(CultureInfo.InvariantCulture, $"| {file} | {v1} | {v2} | {v2 - v1:+0;-0} |");
        }

        string golden = Repo.Path("tests", "CathodeRay.Tests", "v2-sizes.txt");
        string actual = string.Join('\n', lines) + "\n";
        if (Environment.GetEnvironmentVariable("UPDATE_V2_SIZES") == "1")
        {
            File.WriteAllText(golden, actual);
            File.WriteAllText(Repo.Path("docs", "v2-sizes.md"), "# Rozmiar kodu ABI v2 na nes\n\nSegment `CODE` w bajtach po linkowaniu z biblioteką standardową (`cathode cc --ir vreg [--abi v2]`); osobny golden pilota v2. Bramka -15% na wołaniowych (plan 39, zadanie 3) NIE przeszła: 05_calls 349→346 (-0.9%), średnia +1.7 B (8 mniejszych, 1 równy, 15 większych, max +23). Optymalizacje w cenie: ParamAlias pamięciowy na v2, stopka z jednym parkiem A, strażnik kolizji pha/pla, stdlib C zawsze v1. Strukturalny sufit: tylko arg0 w rejestrach (D1), helpery v1 (round-trip cc_arg/cc_ret), writeback crt0; dalsze w itemie 5 (ParamAlias rejestrowy).\n\n" + table);
            return;
        }

        File.ReadAllText(golden).Should().Be(actual, "rozmiar kodu v2 zmienił się — jeśli zamierzenie, odśwież tabelę (UPDATE_V2_SIZES=1)");
    }
}
