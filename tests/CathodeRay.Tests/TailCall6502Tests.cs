using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan opt/abi-tax, optymalizacja 4: wywołanie ogonowe na 6502 (<c>jmp</c> / <c>jmp __icall</c>).</summary>
public sealed class TailCall6502Tests
{
    private static string Emit6502(string source)
    {
        Ir.Module module = Codegen.Lower(TypeChecker.Check(Parser.Parse(source)), "t.c", objectMode: true);
        return CTargets.Find("6502")!.Emit(module, optimize: true);
    }

    [Fact]
    public void Direct_Tail_Call_Emits_Jump()
    {
        const string Source = "int target(int x) { return x + 1; }\nint wrap(int x) { return target(x); }\nint use(int (*f)(int)) { return f(0); }\nint main() { return wrap(7) + use(target); }\n";

        Emit6502(Source).Should().Contain("jmp target");
    }

    [Fact]
    public void Indirect_Tail_Call_Jumps_Through_Icall()
    {
        const string Source = "int apply(int (*f)(int), int v) { return f(v); }\nint twice(int x) { return x + x; }\nint main() { return apply(twice, 5); }\n";

        Emit6502(Source).Should().Contain("jmp __icall");
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

        CcRun.RunOn(Source, "6502").Value.Should().Be(7 + 10 + 9);
        CcRun.RunOn(Source, "65c02").Value.Should().Be(7 + 10 + 9);
    }

    [Fact]
    public void Frame_Blocks_Tail_Call()
    {
        // sum ma ramkę (rekurencja): zwykłe wołanie zostaje
        const string Source = "int sum(int n) { if (n <= 0) return 0; return n + sum(n - 1); }\nint main() { return sum(5); }\n";
        string asm = Emit6502(Source);

        asm.Should().Contain("jsr sum");
        CcRun.RunOn(Source, "6502").Value.Should().Be(15);
    }
}
