using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 37, zadanie 4: programy skompilowane ścieżką <c>--ir vreg</c> zwracają to samo
/// co ścieżką Cell na każdym celu z runnerem.</summary>
public sealed class VRegConformanceTests
{
    private static readonly (string Name, string Source, int Expected)[] AllPrograms =
    [
        ("fib", "int fib(int n) { if (n <= 1) return n; return fib(n - 1) + fib(n - 2); } int main() { return fib(10); }", 55),
        ("fact", "int f(int n) { if (n <= 1) return 1; return n * f(n - 1); } int main() { return f(5); }", 120),
        ("mutual", "int is_even(int n); int is_odd(int n); int is_even(int n) { if (n == 0) return 1; return is_odd(n - 1); } int is_odd(int n) { if (n == 0) return 0; return is_even(n - 1); } int main() { return is_even(10) + (is_odd(10) ? 0 : 2) + (is_odd(11) ? 4 : 0) + (is_even(11) ? 0 : 8); }", 15),
        ("goto", "int g(int n) { int x; x = n * 3; if (n == 0) return 0; g(n - 1); if (n & 1) goto skip; x = 100; skip: return x; } int main() { return g(1) * 10 + g(3); }", 39),
        ("loop", "int h(int n) { int s; int i; if (n == 0) return 1; s = n; for (i = 0; i < 2; i = i + 1) { s = s + h(n - 1); } return s; } int main() { return h(3); }", 19),
        ("struct", "struct P { int x; int y; }; int sq(struct P p) { return p.x * p.x + p.y * p.y; } int main() { struct P p; p.x = 3; p.y = 4; return sq(p); }", 25),
        ("calls", "int add(int a, int b, int c) { return a + b + c; } int mul(int a, int b) { return a * b; } int apply(int (*f)(int, int), int x) { return f(x, 3); } int main() { return add(1, 2, 3) + mul(4, 5) + apply(mul, 7); }", 47),
    ];

    public static TheoryData<string, string, int> Cases()
    {
        var data = new TheoryData<string, string, int>();
        foreach (ICTarget target in TargetHarness.Targets)
        {
            foreach ((string name, _, int expected) in AllPrograms)
            {
                data.Add(target.Name, name, expected);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void VReg_Matches_Cell_On_Target(string cpu, string name, int expected)
    {
        string source = AllPrograms.Single(p => p.Name == name).Source;
        CcRun.Result vreg = CcRun.RunOn(source, cpu, "--ir", "vreg");
        vreg.Value.Should().Be(expected, $"{name} na {cpu} --ir vreg");
    }
}
