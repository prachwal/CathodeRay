using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan opt/abi-tax, optymalizacja 3: świeży wynik słowa w rejestrze wyniku nie wraca
/// do pamięci przed <c>Ret</c> (Z80/8080: <c>add hl,de; ld (t),hl; ld hl,(t); ret</c> → bez ładowania).</summary>
public sealed class ResultRegFreshTests
{
    private static string EmitSample(string file, string cpu)
    {
        string source = File.ReadAllText(Repo.Path("samples", "minic", file));
        CheckedProgram program = TypeChecker.Check(Parser.Parse(source, StdLib.HeaderReader));
        return Codegen.Emit(program, CTargets.Find(cpu)!, file, objectMode: true);
    }

    /// <summary>Blok asemblera jednej funkcji: od etykiety <c>name:</c> do następnej dyrektywy <c>GLOBAL</c>.</summary>
    private static string FunctionBlock(string asm, string name)
    {
        string[] lines = asm.Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == name + ":");
        int end = Array.FindIndex(lines, start + 1, l => l.TrimStart().StartsWith("GLOBAL", StringComparison.Ordinal));
        return string.Join('\n', lines[start..(end < 0 ? lines.Length : end)]);
    }

    [Fact]
    public void Bin_To_Ret_Skips_Reload()
    {
        string add = FunctionBlock(EmitSample("05_calls.c", "z80"), "add");
        add.Should().Contain("ld (add__t@0),hl", "wynik ląduje w pamięci (komórka żyje dalej w modelu)");
        add.Should().NotContain("ld hl,(add__t@0)", "HL już trzyma świeży wynik");
    }

    [Fact]
    public void Nonadjacent_Ret_Still_Reloads()
    {
        // wołanie między Bin a Ret niszczy HL: ładowanie musi zostać (sum ma ramkę i wołanie rekurencyjne)
        string asm = EmitSample("05_calls.c", "z80");
        asm.Should().Contain("call sum");
        asm.Should().Contain("ld hl,(sum__n)", "po wołaniu HL trzeba odtworzyć z pamięci");
    }

    [Theory]
    [InlineData("stub")]
    [InlineData("6502")]
    [InlineData("z80")]
    [InlineData("8080")]
    [InlineData("6800")]
    public void Samples_Still_Run(string cpu)
    {
        CcRun.RunOn(File.ReadAllText(Repo.Path("samples", "minic", "05_calls.c")), cpu, "--incdir", Repo.Path("samples", "minic")).Value.Should().Be(400);
    }
}
