using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 30, krok 14: rzutowania <c>(T)x</c> między uchar/int/uint/wskaźnikami/wskaźnikami do funkcji.</summary>
public sealed class CCastTests
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
    public void Casts_Between_Scalars_And_Pointers(string cpu)
    {
        const string Source = """
            int add(int a, int b) { return a + b; }
            int main() {
                int big = 1000;
                uchar low = (uchar)big;
                int wide = (int)low + 300;
                uint u = (uint)65535 + 2;
                int (*fp)(int, int) = (int (*)(int, int))add;
                int r = fp(3, 4);
                uchar *p = (uchar *)&big;
                return low + wide + u + r + (int)(p != 0) + (int)((uchar)300);
            }
            """;

        CcRun.RunOn(Source, cpu, "-Werror").Value.Should().Be(817);
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void Cast_Truncates_Variables_And_Widens_Without_Sign(string cpu)
    {
        const string Source = """
            int main() {
                int v = 0x1234;
                uchar b = 200;
                int x = (uchar)v;
                int y = (int)b >> 1;
                return x * 256 / 256 + y + ((uint)b << 1);
            }
            """;

        CcRun.RunOn(Source, cpu).Value.Should().Be(0x34 + 100 + 400);
    }

    [Fact]
    public void Cast_Silences_The_Narrowing_Warning()
    {
        CcRun.Compile("int main() { int v = 300; uchar b = (uchar)v; return b; }", "-Werror").Exit.Should().Be(0);
        CcRun.Compile("int main() { int v = 300; uchar b = v; return b; }", "-Werror").Exit.Should().NotBe(0);
    }

    [Fact]
    public void Cast_To_Struct_Is_Rejected()
    {
        (int exit, string error) = CcRun.Compile("struct S { int a; };\nint main() { int v = 1; struct S s = (struct S)v; return 0; }");

        exit.Should().NotBe(0);
        error.Should().Contain("cast");
    }

    [Fact]
    public void Typedef_Names_Work_In_Casts()
    {
        CcRun.Run("typedef uchar byte;\nint main() { int v = 513; return (byte)v; }").Value.Should().Be(1);
    }
}
