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

    // wołane funkcje z wziętym adresem (bez inliningu); parametr martwy przed wołaniem tylko w inc
    private const string NonLeaf = """
        int g(int v) { return v * 3; }
        int sub(int x, int y) { return x - y; }
        int (*gp)(int);
        int (*sp)(int, int);
        int after(int x) { int r; r = g(1); return r + x; }
        int loop(int x) { int r; uchar c; r = 0; c = 3; while (c > 0) { r = r + g(x); c = c - 1; } return r; }
        int back(int x) { int r; r = 0; again: r = r + g(x); if (r < 20) goto again; return r; }
        int swap(int a, int b) { sp = sub; return sub(b, a); }
        int apply(int (*fp)(int), int x) { gp = g; return fp(x); }
        int inc(int x) { return g(x + 1); }
        int addr(int x) { int *p; p = &x; g(2); return *p; }
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

    [Theory]
    [MemberData(nameof(Targets))]
    public void Non_Leaf_Params_Keep_Their_Values_Around_Calls(string cpu)
    {
        const string Main = """
            int main() {
                putdec(after(10)); putchar(' '); putdec(loop(5)); putchar(' '); putdec(back(4)); putchar(' ');
                putdec(swap(10, 3)); putchar(' '); putdec(apply(g, 7)); putchar(' '); putdec(inc(4)); putchar(' '); putdec(addr(9));
                return 0;
            }
            """;

        // after(10) = g(1) + 10 = 13; loop(5) = 3 * 15 = 45; back(4): 12, 24 → 24; swap(10, 3) = 3 - 10 = -7;
        // apply(g, 7) = 21; inc(4) = g(5) = 15; addr(9) = 9
        CcRun.RunOn(Io + NonLeaf + Main, cpu).Console.Should().Be("13 45 24 -7 21 15 9");
    }

    [Fact]
    public void Non_Leaf_Param_Is_Aliased_Only_When_Dead_Before_Every_Call()
    {
        Ir.Module module = Codegen.Lower(TypeChecker.Check(Parser.Parse(NonLeaf)), "t.c", objectMode: true);
        string z80 = CTargets.Find("z80")!.Emit(module, optimize: true);

        z80.Should().Contain("call g").And.Contain("call sub").And.NotContain("inc__x");
        foreach (string kept in new[] { "after__x", "loop__x", "back__x", "swap__a", "apply__fp", "addr__x" })
        {
            z80.Should().Contain(kept, "parametr żywy za wołaniem, argument na dalszej pozycji, wskaźnik wołania albo wzięty adres");
        }
    }

    [Fact]
    public void Aliased_Parameter_Self_Copy_Is_Removed()
    {
        const string Source = """
            void putdec(int v);
            int id(int x) { return x; }
            int inc(int x) { return x + 1; }
            int apply(int (*f)(int), int v) { return f(v); }
            int pick(uchar k, int v) { return k ? apply(id, v) : apply(inc, v); }
            int main() { putdec(pick(1, 5) + pick(0, 5)); return 0; }
            """;

        Ir.Module module = Codegen.Lower(TypeChecker.Check(Parser.Parse(Source)), "t.c", objectMode: true);
        string z80 = CTargets.Find("z80")!.Emit(module, optimize: true);
        string i8080 = CTargets.Find("8080")!.Emit(module, optimize: true);

        z80.Should().NotMatchRegex(@"ld hl,\((\w+)\)\r?\n\s*ld \(\1\),hl", "Z80: samodzielna kopia komórki");
        i8080.Should().NotMatchRegex(@"lhld (\w+)\r?\n\s*shld \1", "8080: samodzielna kopia komórki");
        CcRun.RunOn(Source, "z80").Console.Should().Be("11", "pick(1,5)=5, pick(0,5)=6, suma=11");
        CcRun.RunOn(Source, "8080").Console.Should().Be("11", "pick(1,5)=5, pick(0,5)=6, suma=11");
    }
}
