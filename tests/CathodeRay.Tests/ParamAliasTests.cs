using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 33, krok 5: parametry liści w <c>cc_argN</c> (cele little-endian). Kontrprzykłady z oczekiwaną konsolą liczoną ręcznie.</summary>
public sealed class ParamAliasTests
{
    private const string Io = "void putchar(uchar c);\nvoid putdec(int v);\n";

    // liść: parametry 2, 1 i 4 bajty, wszystkie zmieniane w pętli
    private const string Count = """
        uchar lo;
        int count(int n, uchar k, long acc) {
            while (n > 0) { acc = acc + k; k = k + 2; n = n - 1; }
            lo = k;
            return (int)(acc - 65536L);
        }
        """;

    public static TheoryData<string> Targets()
    {
        var data = new TheoryData<string>();
        foreach (string name in TargetHarness.Targets.Select(static t => t.Name))
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void Params_Modified_In_A_Loop_Keep_Their_Values(string cpu)
    {
        const string Main = """
            int main() {
                int n; long base;
                n = 3; base = 70000L;
                putdec(count(n, 1, base)); putchar(' '); putdec(lo); putchar(' '); putdec(n); putchar(' ');
                putdec(count(n - 1, 250, base - 4464L)); putchar(' '); putdec(lo);
                return 0;
            }
            """;

        // count(3, 1, 70000): acc = 70000 + 1 + 3 + 5 = 70009, k = 7 → 70009 - 65536 = 4473
        // count(2, 250, 65536): acc = 65536 + 250 + 252 = 66038, k = 254 → 502; n wołającego zostaje 3
        CcRun.RunOn(Io + Count + Main, cpu).Console.Should().Be("4473 7 3 502 254");
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void Inlined_And_Exported_Leaf_Keeps_Both_Copies_Correct(string cpu)
    {
        const string Source = """
            int bump(int x, int y) { x = x + y; y = x + x; return x + y; }
            int (*fp)(int, int);
            int main() {
                int a; int b;
                a = bump(2, 3);
                fp = bump;
                b = fp(a, 1);
                putdec(a); putchar(' '); putdec(b); putchar(' '); putdec(bump(b, a));
                return 0;
            }
            """;

        // bump(2, 3): x = 5, y = 10 → 15; fp(15, 1): x = 16, y = 32 → 48; bump(48, 15): x = 63, y = 126 → 189
        CcRun.RunOn(Io + Source, cpu).Console.Should().Be("15 48 189");
    }

    [Fact]
    public void Leaf_Params_Live_In_Argument_Cells_Only_When_Optimizing()
    {
        Ir.Module module = Codegen.Lower(TypeChecker.Check(Parser.Parse(Count)), "t.c", objectMode: true);
        ICTarget z80 = CTargets.Find("z80")!;

        z80.Emit(module, optimize: true).Should().NotContain("count__n").And.NotContain("count__k").And.NotContain("count__acc");
        z80.Emit(module, optimize: false).Should().Contain("count__n");
        CTargets.Find("6800")!.Emit(module, optimize: true).Should().Contain("count__n");
    }
}
