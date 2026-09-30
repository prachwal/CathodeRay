using CathodeRay.Assembler;
using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 33, krok 11: relaksacja <c>jp</c> → <c>jr</c> na Z80 na granicy zasięgu. <c>jr e</c> skacze do adresu skoku + 2 + e,
/// e w −128..127, więc cel − adres skoku musi leżeć w −126..+129. Wypełniacz: <c>ld a,(4660)</c> (3 B) i <c>or a</c> (1 B).</summary>
public sealed class Z80JumpRelaxTests
{
    /// <summary>Skok w przód nad <paramref name="gap"/> bajtami: po skróceniu cel leży o 2 + gap za skokiem.</summary>
    /// <param name="gap">Bajty między skokiem a celem.</param>
    /// <param name="displacement">Oczekiwane przesunięcie <c>jr</c> albo <see langword="null"/>, gdy zostaje <c>jp</c>.</param>
    [Theory]
    [InlineData(125, 125)] // cel − skok = 127
    [InlineData(126, 126)] // cel − skok = 128
    [InlineData(127, 127)] // cel − skok = 129: ostatni w zasięgu
    [InlineData(128, null)] // cel − skok = 130 (e = 128): poza zasięgiem
    public void Forward_Jump_Is_Shortened_Only_In_Range(int gap, int? displacement)
    {
        string[] lines = ["jp nz,T", .. Filler(gap), "T:", "ret"];
        Check(lines, 0, displacement);
    }

    /// <summary>Skok w tył nad <paramref name="gap"/> bajtami (etykieta, wypełniacz, skok): cel − skok = −gap.</summary>
    /// <param name="gap">Bajty między celem a skokiem.</param>
    /// <param name="displacement">Oczekiwane przesunięcie <c>jr</c> (−gap − 2) albo <see langword="null"/>.</param>
    [Theory]
    [InlineData(125, -127)] // cel − skok = −125
    [InlineData(126, -128)] // cel − skok = −126: ostatni w zasięgu
    [InlineData(127, null)] // cel − skok = −127 (e = −129): poza zasięgiem
    [InlineData(128, null)]
    public void Backward_Jump_Is_Shortened_Only_In_Range(int gap, int? displacement)
    {
        string[] lines = ["T:", .. Filler(gap), "jp c,T", "ret"];
        Check(lines, gap, displacement);
    }

    [Fact]
    public void Unconditional_Jump_Becomes_Jr_And_Parity_Sign_Conditions_Stay()
    {
        var isa = new Z80Isa();
        foreach (string line in new[] { "T:", "jp po,T", "jp pe,T", "jp m,T", "jp p,T", "jp T", "jp (hl)" })
        {
            isa.Raw(line);
        }

        isa.RelaxFrom(0);
        isa.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Should().Equal("T:", "jp po,T", "jp pe,T", "jp m,T", "jp p,T", "jr T", "jp (hl)");
    }

    private static IEnumerable<string> Filler(int bytes)
    {
        for (int i = 0; i < bytes / 3; i++)
        {
            yield return "ld a,(4660)";
        }

        for (int i = 0; i < bytes % 3; i++)
        {
            yield return "or a";
        }
    }

    /// <summary>Relaksuje tekst, składa go asemblerem Z80 i sprawdza bajty skoku pod <paramref name="at"/>.</summary>
    private static void Check(string[] lines, int at, int? displacement)
    {
        var isa = new Z80Isa();
        foreach (string line in lines)
        {
            isa.Raw(line);
        }

        isa.RelaxFrom(0);
        ICTarget target = CTargets.All.Single(static t => t.Name == "z80");
        AssemblerTarget assemblerTarget = AssemblerTargets.Find(target.AssemblerCpu)!;
        AssemblyResult image = new TwoPassAssembler(Repo.LoadTarget(assemblerTarget), assemblerTarget.DefaultSyntax)
            .Assemble("SEGMENT \"CODE\"\n" + isa.Text, "prog", _ => null, [], null, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["CODE"] = 0x1000 });
        byte[] code = image.Image;
        int offset = 0x1000 - image.Origin + at;
        if (displacement is { } e)
        {
            isa.Text.Should().Contain("jr ");
            code[offset + 1].Should().Be(unchecked((byte)e));
            code[offset].Should().BeOneOf((byte)0x20, (byte)0x38); // jr nz / jr c
        }
        else
        {
            isa.Text.Should().NotContain("jr ");
            code[offset].Should().BeOneOf((byte)0xC2, (byte)0xDA); // jp nz / jp c
        }
    }
}
