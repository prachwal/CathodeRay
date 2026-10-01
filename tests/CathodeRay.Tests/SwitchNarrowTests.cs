using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 40: switch na <c>uchar</c> ze stałymi 0..255 porównuje się bajtem (bez testu starszego bajtu);
/// poza zakresem bajtu wraca do 16 bitów.</summary>
public sealed class SwitchNarrowTests
{
    private const string ByteSwitch =
        "uchar sw(uchar c) { switch (c) { case 0: return 10; case 1: return 20; case 2: return 35; case 3: return 41; case 4: return 57; default: return 0; } }\n";

    private static Ir.Module Lower(string source) => Codegen.Lower(TypeChecker.Check(Parser.Parse(source)), "t.c", objectMode: true);

    private static Ir.BrCmp FirstBranch(Ir.Module module) =>
        module.Functions.SelectMany(static f => f.Body).OfType<Ir.BrCmp>().First();

    [Fact]
    public void Byte_Switch_Is_Narrowed()
    {
        Ir.BrCmp branch = FirstBranch(Lower(ByteSwitch + "int main() { return 0; }"));

        (branch.A as Ir.Cell)!.W.Should().Be(1, "wartość switcha uchar jest bajtem");
        (branch.B as Ir.Imm)!.W.Should().Be(1, "stała case mieści się w bajcie");
    }

    [Fact]
    public void Byte_Switch_Computes_Correctly_On_All_Targets()
    {
        const string Source = ByteSwitch + "int main() { return sw(0) + sw(1) + sw(2) + sw(3) + sw(4) + sw(9); }";

        // 10+20+35+41+57+0 = 163
        CcRun.RunOn(Source, "z80").Value.Should().Be(163);
        CcRun.RunOn(Source, "8080").Value.Should().Be(163);
        CcRun.RunOn(Source, "6502").Value.Should().Be(163);
        CcRun.RunOn(Source, "6800").Value.Should().Be(163);
    }

    [Fact]
    public void Out_Of_Range_Case_Keeps_Switch_Wide()
    {
        const string Source = "uchar f(uchar c) { switch (c) { case 0: return 1; case 256: return 2; default: return 3; } }\n";
        Ir.BrCmp branch = FirstBranch(Lower(Source + "int main() { return 0; }"));

        (branch.A as Ir.Cell)!.W.Should().Be(2, "stała 256 nie mieści się w bajcie — porównanie 16-bitowe");
    }

    [Fact]
    public void Out_Of_Range_Case_Never_Matches_Byte_Value()
    {
        const string Source = "uchar f(uchar c) { switch (c) { case 0: return 1; case 256: return 2; default: return 3; } }\nint main() { return f(0) * 10 + f(5); }";

        // f(0)=1, f(5)=3 (256 nieosiągalne dla uchar) -> 13
        CcRun.RunOn(Source, "z80").Value.Should().Be(13);
        CcRun.RunOn(Source, "6502").Value.Should().Be(13);
    }

    [Fact]
    public void Case_255_Matches()
    {
        const string Source = "int f(uchar c) { switch (c) { case 255: return 7; default: return 1; } }\nint main() { return f(255) * 10 + f(254); }";

        CcRun.RunOn(Source, "z80").Value.Should().Be(71);
        CcRun.RunOn(Source, "8080").Value.Should().Be(71);
    }
}
