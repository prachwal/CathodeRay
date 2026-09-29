using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 27 B: do-while, switch, enum, sizeof, char, złożone przypisania na wskaźnikach,
/// inicjalizatory tablic i globalnych wskaźników.</summary>
public sealed class CSyntaxTests
{
    private static int Run(string source)
    {
        var (cpu, _, _) = CCodegenTests.RunC(source);
        return (cpu.State.X * 256) + cpu.State.A;
    }

    [Fact]
    public void DoWhile_Runs_Body_Once_Then_Tests()
    {
        const string Source = """
            int main() {
                int n = 0;
                do { n++; } while (n < 5);
                int m = 10;
                do { m++; } while (0);
                return n * 100 + m;
            }
            """;
        Run(Source).Should().Be(511);
    }

    [Fact]
    public void DoWhile_Continue_Jumps_To_Condition_And_Break_Leaves()
    {
        const string Source = """
            int main() {
                int i = 0;
                int s = 0;
                do {
                    i++;
                    if (i == 3) continue;
                    if (i == 6) break;
                    s = s + i;
                } while (i < 10);
                return s;
            }
            """;
        Run(Source).Should().Be(1 + 2 + 4 + 5);
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 22)]
    [InlineData(3, 30)]
    [InlineData(4, 99)]
    public void Switch_Dispatches_Falls_Through_And_Defaults(int value, int expected)
    {
        string source = $$"""
            int f(uchar v) {
                int r = 0;
                switch (v) {
                    case 1: r = 10; break;
                    case 2: r = 20;
                    case 3: r = r + 2; break;
                    case 30: r = 30; break;
                    default: r = 99;
                }
                if (v == 3) r = 30;
                return r;
            }
            int main() { return f({{value}}); }
            """;
        Run(source).Should().Be(expected);
    }

    [Fact]
    public void Switch_On_Int_With_Negative_And_Wide_Cases_No_Match_Skips()
    {
        const string Source = """
            int f(int v) {
                switch (v) {
                    case -1: return 1;
                    case 300: return 2;
                }
                return 3;
            }
            int main() { return f(0 - 1) * 100 + f(300) * 10 + f(44); }
            """;
        Run(Source).Should().Be(123);
    }

    [Fact]
    public void Switch_Inside_Loop_Passes_Continue_Outward()
    {
        const string Source = """
            int main() {
                int s = 0;
                for (int i = 0; i < 6; i++) {
                    switch (i) {
                        case 2: continue;
                        case 4: break;
                        default: s = s + i;
                    }
                    s = s + 100;
                }
                return s;
            }
            """;
        Run(Source).Should().Be(0 + 100 + 1 + 100 + 3 + 100 + 100 + 5 + 100);
    }

    [Fact]
    public void Switch_Rejects_Duplicates_And_NonConstants()
    {
        FluentActions.Invoking(() => Run("int main() { int a = 1; switch (a) { case 1: break; case 1: break; } return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*duplicate 'case'*");
        FluentActions.Invoking(() => Run("int main() { int a = 1; int b = 2; switch (a) { case b: break; } return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*constant*");
    }

    [Fact]
    public void Enum_Sizeof_And_Char_Alias()
    {
        const string Source = """
            enum { A, B = 5, C, D = C + 10 };
            int g[7];
            int main() {
                char c = 'x';
                uchar buf[C];
                return A + B * 10 + C * 100 + D + sizeof(int) + sizeof(char) + sizeof(g) + sizeof buf + sizeof(uchar*) + c;
            }
            """;
        Run(Source).Should().Be(0 + 50 + 600 + 16 + 2 + 1 + 14 + 6 + 2 + 120);
    }

    [Fact]
    public void Compound_Assign_On_Pointer_Targets_And_Pointer_Plus_Equals()
    {
        const string Source = """
            int main() {
                uchar a[4];
                a[0] = 5; a[1] = 6; a[2] = 7; a[3] = 8;
                uchar *p = a;
                *p += 10;
                p[1] -= 1;
                p[2] *= 2;
                p += 3;
                *p ^= 1;
                return a[0] + 2 * a[1] + 3 * a[2] + 4 * a[3];
            }
            """;
        Run(Source).Should().Be(15 + 10 + 42 + 36);
    }

    [Fact]
    public void Compound_Assign_Rejects_Side_Effects_In_Target()
    {
        FluentActions.Invoking(() => Run("int main() { uchar a[4]; int i = 0; a[i++] += 1; return 0; }"))
            .Should().Throw<CParseException>().WithMessage("*side effects*");
    }

    [Fact]
    public void Local_Array_Initializers()
    {
        const string Source = """
            int main() {
                uchar a[] = {1, 2, 3};
                int w[4] = {1000, 2};
                uchar s[] = "hi";
                uchar z[6] = {9};
                return a[0] + a[1] + a[2] + sizeof a * 10 + w[0] + w[1] + w[2] + w[3] + s[1] + s[2] + sizeof s + z[0] + z[5];
            }
            """;
        Run(Source).Should().Be(6 + 30 + 1000 + 2 + 0 + 0 + 105 + 0 + 3 + 9 + 0);
    }

    [Fact]
    public void Local_Array_Reinitializes_On_Every_Entry()
    {
        const string Source = """
            int f() {
                uchar a[3] = {1};
                a[0] = a[0] + 5;
                a[2] = 7;
                return a[0] + a[2];
            }
            int main() { f(); return f(); }
            """;
        Run(Source).Should().Be(13);
    }

    [Fact]
    public void Global_Initializers_Arrays_Strings_And_Pointers()
    {
        const string Source = """
            int base = 0x1234;
            uchar tab[] = {3, 0x10, 5};
            int wide[2] = {300, 2};
            uchar two[2] = {7, 8};
            uchar *msg = "abc";
            uchar *tp = tab;
            int *bp = &base;
            uchar name[] = "xy";
            int main() {
                return (base >> 8) + tab[1] + wide[0] + wide[1] + two[0] + two[1] + msg[2] + tp[2] + *bp + name[1] + sizeof name;
            }
            """;
        Run(Source).Should().Be(0x12 + 16 + 300 + 2 + 7 + 8 + 99 + 5 + 0x1234 + 121 + 3);
    }

    [Fact]
    public void Global_Pointer_Can_Be_Reassigned()
    {
        const string Source = """
            uchar *p = "ab";
            int main() {
                p = p + 1;
                return *p;
            }
            """;
        Run(Source).Should().Be('b');
    }
}
