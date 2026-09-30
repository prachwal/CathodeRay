using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 37, zadanie 5: regresja współdzielonych komórek — globale (<c>cc_g_*</c>) i kod wstawiony
/// nie są rejestrami lokalnymi; przebiegi VReg nie mogą wycinać zapisów do nich
/// (inicjalizatory <c>12_globals_init.c</c> dawały 113 zamiast 143).</summary>
public sealed class VRegGlobalsTests
{
    [Theory]
    [InlineData("12_globals_init.c")]
    [InlineData("09_init.c")]
    [InlineData("13_strings.c")]
    [InlineData("20_matrix.c")]
    public void Stub_Vreg_Matches_Cell(string file)
    {
        string source = File.ReadAllText(Repo.Path("samples", "minic", file));
        CcRun.Result cell = CcRun.RunOn(source, "stub", "--incdir", Repo.Path("samples", "minic"));
        CcRun.Result vreg = CcRun.RunOn(source, "stub", "--incdir", Repo.Path("samples", "minic"), "--ir", "vreg");
        vreg.Value.Should().Be(cell.Value, $"{file} stub vreg vs cell");
        vreg.Console.Should().Be(cell.Console, $"{file} stub vreg vs cell");
    }
}
