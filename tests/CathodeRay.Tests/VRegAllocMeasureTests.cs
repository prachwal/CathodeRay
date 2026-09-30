using System.Globalization;
using System.Text;
using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 37, zadanie 7: pomiar alokatorów na samplach — trzymane vs spillowane rejestry.
/// Odświeżenie: <c>UPDATE_VREG_ALLOC=1 dotnet test --filter VRegAllocMeasureTests</c> (zapisuje też <c>docs/vreg-alloc.md</c>).</summary>
public sealed class VRegAllocMeasureTests
{
    private static string? ReadHeader(string path)
    {
        if (path.StartsWith('<'))
        {
            return StdLib.Header(path[1..^1]);
        }

        string full = Repo.Path("samples", "minic", path);
        return File.Exists(full) ? File.ReadAllText(full) : null;
    }

    [Fact]
    public void Allocator_Stats_Match_The_Recorded_Table()
    {
        string[] files = [.. Directory.GetFiles(Repo.Path("samples", "minic"), "??_*.c").Select(Path.GetFileName).Order(StringComparer.Ordinal)!];
        string[] targets = ["z80", "6502"];
        (string Name, IVRegAllocator Alloc)[] allocators = [("accumulator", new AccumulatorAllocator()), ("linear-scan", new LinearScanAllocator())];
        var lines = new List<string>();
        var table = new StringBuilder();
        table.AppendLine("| program | cel | alokator | trzymane | spill |");
        table.AppendLine("| --- | --- | --- | ---: | ---: |");
        int linearKept = 0;
        foreach (string file in files)
        {
            string source = File.ReadAllText(Repo.Path("samples", "minic", file));
            CheckedProgram program = TypeChecker.Check(Parser.Parse(source, ReadHeader));
            VReg.Module vreg = VRegLift.Run(Codegen.Lower(program, file));
            foreach (string target in targets)
            {
                VRegTargetInfo info = VRegTargetInfo.For(CTargets.Find(target)!);
                foreach ((string name, IVRegAllocator alloc) in allocators)
                {
                    int kept = 0;
                    int spilled = 0;
                    foreach (VReg.Function function in vreg.Functions)
                    {
                        foreach (string? phys in alloc.Allocate(function, info).Values)
                        {
                            if (phys is null)
                            {
                                spilled++;
                            }
                            else
                            {
                                kept++;
                            }
                        }
                    }

                    lines.Add($"{file} {target} {name} kept={kept} spilled={spilled}");
                    table.AppendLine(CultureInfo.InvariantCulture, $"| {file} | {target} | {name} | {kept} | {spilled} |");
                    if (name == "linear-scan")
                    {
                        linearKept += kept;
                    }
                }
            }
        }

        linearKept.Should().BeGreaterThan(0, "linear-scan ma trzymać coś w rejestrach na samplach");
        string golden = Repo.Path("tests", "CathodeRay.Tests", "vreg-alloc.txt");
        string actual = string.Join('\n', lines) + "\n";
        if (Environment.GetEnvironmentVariable("UPDATE_VREG_ALLOC") == "1")
        {
            File.WriteAllText(golden, actual);
            File.WriteAllText(Repo.Path("docs", "vreg-alloc.md"), "# Pomiar alokatorów VReg\n\nTrzymane vs spillowane rejestry wirtualne na samplach (`--ir vreg`, adapter do Cell: wynik jest doradczy — właściwy przydział robi `RegisterAllocator` po opadnięciu; pomiar służy przyszłemu emiterowi). Tabelę odświeża\n`UPDATE_VREG_ALLOC=1 dotnet test --filter VRegAllocMeasureTests`.\n\n" + table);
            return;
        }

        File.ReadAllText(golden).Should().Be(actual, "statystyki alokacji zmieniły się — jeśli zamierzenie, odśwież tabelę (UPDATE_VREG_ALLOC=1)");
    }
}
