using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Luki mini-C: literały znakowe/napisowe, operatory int 16-bit,
/// ++/--/break/continue, więcej niż 2 argumenty.</summary>
public sealed class CGapsTests
{
    private static (int A, int X) Run(string source)
    {
        var (cpu, _, _) = CCodegenTests.RunC(source);
        return (cpu.State.A, cpu.State.X);
    }

    [Theory]
    [InlineData("'A'", 65)]
    [InlineData("'\\n'", 10)]
    [InlineData("'\\0'", 0)]
    [InlineData("'\\\\'", 92)]
    public void Char_Literal_Is_Its_Code(string literal, int expected)
    {
        Run($"int main() {{ uchar c = {literal}; return c; }}").A.Should().Be(expected);
    }

    [Fact]
    public void String_Literal_Is_Indexable_And_Terminated()
    {
        const string Source = """
            int main() {
                uchar *s = "hi";
                return s[1] + s[2];
            }
            """;
        Run(Source).A.Should().Be('i');
    }

    [Theory]
    [InlineData("a * b", 2100)]
    [InlineData("a / b", 42)]
    [InlineData("a % b", 6)]
    [InlineData("a << 1", 600)]
    [InlineData("a >> 2", 75)]
    [InlineData("a & 255", 44)]
    [InlineData("a | b", 303)]
    [InlineData("a ^ b", 299)]
    [InlineData("~a & 255", 211)]
    public void Int_Operators_Are_16_Bit(string expr, int expected)
    {
        var (a, x) = Run($"int main() {{ int a = 300; int b = 7; return {expr}; }}");
        ((x * 256) + a).Should().Be(expected);
    }

    [Fact]
    public void Increment_And_Decrement()
    {
        const string Source = """
            int main() {
                int i = 5;
                i++;
                i++;
                i--;
                return i;
            }
            """;
        Run(Source).A.Should().Be(6);
    }

    [Fact]
    public void For_With_Increment_Sums()
    {
        const string Source = """
            int main() {
                int s = 0;
                for (int i = 1; i <= 10; i++) s = s + i;
                return s;
            }
            """;
        Run(Source).A.Should().Be(55);
    }

    [Fact]
    public void Break_Leaves_Loop()
    {
        const string Source = """
            int main() {
                int i = 0;
                while (1) {
                    if (i == 7) break;
                    i = i + 1;
                }
                return i;
            }
            """;
        Run(Source).A.Should().Be(7);
    }

    [Fact]
    public void Continue_Skips_Iteration_In_For()
    {
        const string Source = """
            int main() {
                int s = 0;
                for (int i = 0; i < 10; i = i + 1) {
                    if (i == 5) continue;
                    s = s + i;
                }
                return s;
            }
            """;
        Run(Source).A.Should().Be(40);
    }

    [Fact]
    public void Three_Arguments()
    {
        const string Source = """
            int f(int a, int b, int c) { return a + b * 2 + c * 4; }
            int main() { return f(1, 2, 3); }
            """;
        Run(Source).A.Should().Be(17);
    }

    [Fact]
    public void Four_Arguments_Keep_Order()
    {
        const string Source = """
            int f(int a, int b, int c, int d) { return a + b * 2 + c * 4 + d * 8; }
            int main() { return f(1, 2, 3, 4); }
            """;
        Run(Source).A.Should().Be(49);
    }

    [Theory]
    [InlineData("50000 / 3", 16666)]
    [InlineData("50000 % 7", 6)]
    [InlineData("65535 / 65535", 1)]
    [InlineData("40000 / 40001", 0)]
    [InlineData("1000 / 0", 0)]
    [InlineData("300 * 200", 60000 & 0xFFFF)]
    [InlineData("n << 7", 32768)]
    [InlineData("65535 >> 9", 127)]
    [InlineData("258 << 8", 512)]
    public void Int_Operators_Edge_Cases(string expr, int expected)
    {
        var (a, x) = Run($"int main() {{ int n = 256; return {expr}; }}");
        ((x * 256) + a).Should().Be(expected);
    }

    [Fact]
    public void Uchar_Product_Widens_Into_Int()
    {
        var (a, x) = Run("int main() { uchar p = 20; uchar q = 5; int r = p * q; return r; }");
        a.Should().Be(100);
        x.Should().Be(0);
    }

    [Fact]
    public void Postfix_Yields_Old_Value_Prefix_The_New()
    {
        const string Source = """
            int main() {
                int i = 5;
                int a = i++;
                int b = ++i;
                return a * 100 + b;
            }
            """;
        var (a, x) = Run(Source);
        ((x * 256) + a).Should().Be(507);
    }

    [Fact]
    public void Pointer_Postfix_Walks_A_String()
    {
        const string Source = """
            int main() {
                uchar *s = "abc";
                int n = 0;
                while (*s++) n++;
                return n;
            }
            """;
        Run(Source).A.Should().Be(3);
    }

    [Fact]
    public void Break_And_Continue_Bind_To_Innermost_Loop()
    {
        const string Source = """
            int main() {
                int s = 0;
                for (int i = 0; i < 4; i++) {
                    for (int j = 0; j < 10; j++) {
                        if (j == 2) break;
                        s++;
                    }
                    if (i == 1) continue;
                    s = s + 10;
                }
                return s;
            }
            """;
        Run(Source).A.Should().Be(38);
    }

    [Fact]
    public void Break_Outside_Loop_Is_A_Type_Error()
    {
        Action act = () => Run("int main() { break; return 0; }");
        act.Should().Throw<CathodeRay.C.CTypeException>().WithMessage("*outside a loop*");
    }

    [Fact]
    public void Mixed_Width_Arguments_Are_Independent()
    {
        const string Source = """
            int f(int a, uchar b, int c) { return a - b - c; }
            int main() { return f(1000, 3, 7); }
            """;
        var (a, x) = Run(Source);
        ((x * 256) + a).Should().Be(990);
    }

    [Fact]
    public void Six_Arguments_Nest_Without_Clobbering()
    {
        const string Source = """
            int f(int a, int b, int c, int d, int e, int g) {
                return a + b + c - d - e - g;
            }
            int main() { return f(f(10, 0, 0, 0, 0, 0), 20, 30, 1, 2, f(4, 0, 0, 0, 0, 0)); }
            """;
        Run(Source).A.Should().Be(53);
    }

    [Fact]
    public void Seven_Parameters_Are_Rejected()
    {
        Action act = () => Run("int f(int a, int b, int c, int d, int e, int g, int h) { return a; } int main() { return 0; }");
        act.Should().Throw<CathodeRay.C.CTypeException>().WithMessage("*at most 6*");
    }
}
