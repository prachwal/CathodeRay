using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Wywołanie ogonowe na Z80/8080: <c>call f</c> + <c>ret</c> zamieniane w skok.</summary>
public sealed class TailCallTests
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
    public void Direct_Tail_Call_Emits_Jump_On_Z80()
    {
        const string Source = "int target(int x) { return x + 1; }\nint wrap(int x) { return target(x); }\nint use(int (*f)(int)) { return f(0); }\nint main() { return wrap(7) + use(target); }\n";

        EmitZ80(Source).Should().Contain("jp target");
    }

    [Fact]
    public void Direct_Tail_Call_Emits_Jump_On_8080()
    {
        const string Source = "int target(int x) { return x + 1; }\nint wrap(int x) { return target(x); }\nint use(int (*f)(int)) { return f(0); }\nint main() { return wrap(7) + use(target); }\n";

        Emit8080(Source).Should().Contain("jmp target");
    }

    [Fact]
    public void Indirect_Tail_Call_Emits_Indirect_Jump()
    {
        const string Source = "int apply(int (*f)(int), int v) { return f(v); }\nint twice(int x) { return x + x; }\nint main() { return apply(twice, 5); }\n";

        EmitZ80(Source).Should().Contain("jp (hl)");
        Emit8080(Source).Should().Contain("pchl");
    }

    [Fact]
    public void Tail_Calls_Compute_Correct_Values()
    {
        const string Source = """
            int id(int x) { return x; }
            int twice(int x) { return x + x; }
            int apply(int (*f)(int), int v) { return f(v); }
            int glob;
            void setg(int v) { glob = v; }
            void wrap(int v) { setg(v); }
            int main() {
                wrap(9);
                return id(7) + apply(twice, 5) + glob;
            }
            """;

        CcRun.RunOn(Source, "z80").Value.Should().Be(7 + 10 + 9);
        CcRun.RunOn(Source, "8080").Value.Should().Be(7 + 10 + 9);
    }

    [Fact]
    public void Width_Mismatch_Keeps_Normal_Call()
    {
        const string Source = """
            int g(int x) { return x + 300; }
            uchar f() { return g(1); }
            int use(int (*h)(int)) { return h(2); }
            int main() { return f() + use(g); }
            """;
        string z80 = EmitZ80(Source);

        z80.Should().Contain("call g");
        z80.Should().NotContain("jp g");
        CcRun.RunOn(Source, "z80").Value.Should().Be(45 + 302);
        CcRun.RunOn(Source, "8080").Value.Should().Be(45 + 302);
    }

    [Fact]
    public void Mutual_Recursion_Tail_Calls_When_Frame_Empty()
    {
        const string Source = """
            int f(int n);
            int g(int n) {
                if (n <= 0) return 0;
                return f(n - 1);
            }
            int f(int n) {
                if (n <= 0) return 1;
                return g(n - 1);
            }
            int main() { return f(3) + g(3); }
            """;
        string z80 = EmitZ80(Source);

        // Parametr w rejestrze nie zostawia ramki, więc wzajemna rekurencja w pozycji ogonowej to skoki (stos nie rośnie).
        z80.Should().Contain("jp f");
        z80.Should().Contain("jp g");
        CcRun.RunOn(Source, "z80").Value.Should().Be(0 + 1);
        CcRun.RunOn(Source, "8080").Value.Should().Be(0 + 1);
        CcRun.RunOn(Source, "6502").Value.Should().Be(0 + 1);
    }

    [Fact]
    public void Terminal_Tail_Call_Skips_Dead_Footer()
    {
        const string Source = "int target(int x) { return x + 1; }\nint wrap(int x) { return target(x); }\nint use(int (*f)(int)) { return f(0); }\nint main() { return wrap(7) + use(target); }\n";

        EmitZ80(Source).Should().Contain("jp target").And.NotContain("wrap__ret:");
        Emit8080(Source).Should().Contain("jmp target").And.NotContain("wrap__ret:");
        CcRun.RunOn(Source, "z80").Value.Should().Be(9);
        CcRun.RunOn(Source, "8080").Value.Should().Be(9);
    }

    [Fact]
    public void Join_Tail_Call_In_Both_Arms()
    {
        const string Source = """
            int twice(int x) { return x + x; }
            int neg(int x) { return -x; }
            int apply(int (*f)(int), int v) { return f(v); }
            int pick(uchar k, int v) { return k ? apply(twice, v) : apply(neg, v); }
            int main() { return pick(1, 5) + pick(0, 5); }
            """;
        string z80 = EmitZ80(Source);

        z80.Should().Contain("jp apply");
        z80.Should().NotContain("pick__ret:");
        CcRun.RunOn(Source, "z80").Value.Should().Be((2 * 5) + (-5));
        CcRun.RunOn(Source, "8080").Value.Should().Be((2 * 5) + (-5));
        CcRun.RunOn(Source, "6502").Value.Should().Be((2 * 5) + (-5));
    }
}
