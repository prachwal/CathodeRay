using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 40, item 9 (część): parametry funkcji bez aliasu (liściaste poza grafem) mogą trafić do rejestrów
/// Z80/8080 — prolog wstawia <c>cc_argN</c> do pary. Klika interferencji param–param chroni przed dzieleniem rejestru.</summary>
public sealed class ParamRegisterTests
{
    private static string EmitZ80(string source)
    {
        Ir.Module module = Codegen.Lower(TypeChecker.Check(Parser.Parse(source)), "t.c", objectMode: true);
        return CTargets.Find("z80")!.Emit(module, optimize: true);
    }

    [Fact]
    public void Two_Live_Params_Do_Not_Share_Register_And_Compute_Correctly()
    {
        // a i b żywe jednocześnie wokół wołań — muszą mieć różne pary; wartość sprawdza poprawność na 4 celach
        const string Source = "int id(int x) { return x; }\nint f(int a, int b) { return id(a) * 100 + id(b); }\nint main() { return f(3, 7); }";

        string z80 = EmitZ80(Source);
        (z80.Contains("ld bc,(cc_arg1)") || z80.Contains("ld de,(cc_arg1)")).Should().BeTrue("parametr trafia do pary");

        CcRun.RunOn(Source, "z80").Value.Should().Be(307);
        CcRun.RunOn(Source, "8080").Value.Should().Be(307);
        CcRun.RunOn(Source, "6502").Value.Should().Be(307);
    }

    [Fact]
    public void Param_In_Register_Survives_Recursive_Call()
    {
        // n w rejestrze: użyty po obu wołaniach rekurencyjnych (jeśli nie przeżyje, wynik zły)
        const string Source = "int fib(int n) { if (n < 2) return n; return fib(n - 1) + fib(n - 2); }\nint main() { return fib(11); }";

        EmitZ80(Source).Should().Contain("ld bc,(cc_arg1)", "parametr n trafia do pary BC");
        CcRun.RunOn(Source, "z80").Value.Should().Be(89);
        CcRun.RunOn(Source, "8080").Value.Should().Be(89);
    }

    [Fact]
    public void Accumulating_Param_In_Register()
    {
        const string Source = "int sum(int n) { int s = 0; while (n > 0) { s = s + n; n = n - 1; } return s; }\nint main() { return sum(10); }";

        CcRun.RunOn(Source, "z80").Value.Should().Be(55);
        CcRun.RunOn(Source, "8080").Value.Should().Be(55);
    }
}
