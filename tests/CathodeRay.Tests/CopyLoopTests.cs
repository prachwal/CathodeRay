using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 40, zadanie 5: pętla kopiująca (<c>while (n) { *d = *s; d++; s++; n--; }</c>) jako
/// <c>ldir</c> na Z80 (8080 zostaje na pętli).</summary>
public sealed class CopyLoopTests
{
    private const string Copy =
        "uchar src[8] = {10, 20, 30, 40, 50, 60, 70, 80};\nuchar dst[8] = {0};\nvoid copy(uchar *d, uchar *s, uint n) { while (n) { *d = *s; d++; s++; n--; } }\n";

    private static string EmitZ80(string source)
    {
        Ir.Module module = Codegen.Lower(TypeChecker.Check(Parser.Parse(source)), "t.c", objectMode: true);
        return CTargets.Find("z80")!.Emit(module, optimize: true);
    }

    private static string Emit8080(string source)
    {
        Ir.Module module = Codegen.Lower(TypeChecker.Check(Parser.Parse(source)), "t.c", objectMode: true);
        return CTargets.Find("8080")!.Emit(module, optimize: true);
    }

    [Fact]
    public void Copy_Loop_Emits_Ldir_On_Z80()
    {
        EmitZ80(Copy + "int main() { return 0; }").Should().Contain("ldir");
        Emit8080(Copy + "int main() { return 0; }").Should().NotContain("ldir");
    }

    [Fact]
    public void Copy_Loop_Copies_Correctly_Including_Zero_Count()
    {
        const string Source = Copy + "int main() { copy(dst, src, 8); copy(dst, src, 0); return dst[0] + dst[7] + dst[3]; }";

        CcRun.RunOn(Source, "z80").Value.Should().Be(10 + 80 + 40);
        CcRun.RunOn(Source, "8080").Value.Should().Be(10 + 80 + 40);
    }

    [Fact]
    public void Copy_Loop_With_Le_Condition_Emits_Ldir()
    {
        const string Source = "void copy(uchar *d, uchar *s, int n) { while (n > 0) { *d = *s; d++; s++; n--; } }\nint main() { return 0; }";

        EmitZ80(Source).Should().Contain("ldir");
    }

    [Fact]
    public void Wider_Elements_Stay_Loop()
    {
        const string Source = "void copy(int *d, int *s, int n) { while (n) { *d = *s; d++; s++; n--; } }\nint main() { return 0; }";

        EmitZ80(Source).Should().NotContain("ldir");
    }
}
