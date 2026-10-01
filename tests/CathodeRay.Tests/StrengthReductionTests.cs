using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 40, zadanie 4: strength reduction — <c>x + x</c> to <c>x &lt;&lt; 1</c> wprost przez HL
/// (<c>add hl,hl</c> / <c>dad h</c>), mnożenie przez małą stałą bez helpera.</summary>
public sealed class StrengthReductionTests
{
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
    public void Twice_Doubles_With_AddHl()
    {
        const string Source = "int twice(int x) { return x + x; } int main() { return twice(21); }";

        EmitZ80(Source).Should().Contain("add hl,hl");
        Emit8080(Source).Should().Contain("dad h");
        CcRun.RunOn(Source, "z80").Value.Should().Be(42);
        CcRun.RunOn(Source, "8080").Value.Should().Be(42);
    }

    [Fact]
    public void ShiftLeft1_Uses_AddHl()
    {
        const string Source = "int sh(int x) { return x << 1; } int main() { return sh(21); }";

        EmitZ80(Source).Should().Contain("add hl,hl");
        Emit8080(Source).Should().Contain("dad h");
        CcRun.RunOn(Source, "z80").Value.Should().Be(42);
        CcRun.RunOn(Source, "8080").Value.Should().Be(42);
    }

    [Fact]
    public void MulBy3_Expands_Without_Helper()
    {
        const string Source = "int f(int x) { return x * 3; } int main() { return f(7); }";

        EmitZ80(Source).Should().Contain("add hl,hl").And.NotContain("__cc_mul");
        Emit8080(Source).Should().Contain("dad h").And.NotContain("__cc_mul");
        CcRun.RunOn(Source, "z80").Value.Should().Be(21);
        CcRun.RunOn(Source, "8080").Value.Should().Be(21);
    }
}
