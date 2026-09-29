using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 32, krok 6: test regresji dla ramki rekurencji; weryfikuje, że skompilowane
/// programy z rekurencją działają poprawnie na różnych architekturach bez przechowywania
/// zbędnych czasowych zmiennych na stosie.</summary>
public sealed class RecursionFrameTests
{
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
    public void Fibonacci_Computes_Correctly(string cpu)
    {
        const string Source = """
            int fib(int n) {
                if (n <= 1) return n;
                return fib(n - 1) + fib(n - 2);
            }
            int main() {
                return fib(10);
            }
            """;

        // fib(10) = 55
        CcRun.RunOn(Source, cpu).Value.Should().Be(55);
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void Factorial_Computes_Correctly(string cpu)
    {
        const string Source = """
            int factorial(int n) {
                if (n <= 1) return 1;
                return n * factorial(n - 1);
            }
            int main() {
                return factorial(5);
            }
            """;

        // 5! = 120
        CcRun.RunOn(Source, cpu).Value.Should().Be(120);
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void Mutual_Recursion_Is_Even_And_Is_Odd(string cpu)
    {
        const string Source = """
            int is_even(int n);
            int is_odd(int n);

            int is_even(int n) {
                if (n == 0) return 1;
                return is_odd(n - 1);
            }

            int is_odd(int n) {
                if (n == 0) return 0;
                return is_even(n - 1);
            }

            int main() {
                int r = 0;
                if (is_even(10)) r = r + 1;
                if (!is_odd(10)) r = r + 2;
                if (is_odd(11)) r = r + 4;
                if (!is_even(11)) r = r + 8;
                return r;
            }
            """;

        // All four conditions are true: 1 + 2 + 4 + 8 = 15
        CcRun.RunOn(Source, cpu).Value.Should().Be(15);
    }
}
