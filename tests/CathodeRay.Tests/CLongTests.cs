using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 30, krok 18: <c>long</c>/<c>ulong</c> (32 bity) — stałe, arytmetyka, porównania, wołania, wskaźniki.</summary>
public sealed class CLongTests
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
    public void Arithmetic_And_Comparisons(string cpu)
    {
        const string Source = """
            long g = 100000L;
            ulong u = 4000000000;
            int main() {
                long a = 70000;
                long b = a + 5;
                long c = b * 3;
                int i = 0 - 2;
                long d = c + i;
                ulong e = u / 1000;
                return (int)(d / 7) + (int)(e / 1000) + (a < b) + (g > a);
            }
            """;

        CcRun.RunOn(Source, cpu, "-Werror").Value.Should().Be(34003);
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void Signed_And_Unsigned_Ordering_Differ(string cpu)
    {
        const string Source = """
            int main() {
                long neg = 0 - 5;
                long pos = 3;
                ulong big = 4000000000;
                ulong small = 5;
                int r = 0;
                if (neg < pos) r = r + 1;
                if (big > small) r = r + 2;
                if (neg / 2 == 0 - 2) r = r + 4;
                if (neg % 3 == 0 - 2) r = r + 8;
                if ((neg >> 1) == 0 - 3) r = r + 16;
                if ((big >> 28) == 14) r = r + 32;
                return r;
            }
            """;

        CcRun.RunOn(Source, cpu, "-Werror").Value.Should().Be(63);
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void Calls_Returns_Pointers_And_Arrays(string cpu)
    {
        const string Source = """
            long twice(long v) { return v + v; }
            long table[3] = {1000000L, 2, 300000L};
            long total(long *p, int n) {
                long s = 0;
                int i = 0;
                while (i < n) { s = s + p[i]; i++; }
                return s;
            }
            int main() {
                long v = twice(twice(30000L));
                long t = total(table, 3);
                long *q = &table[1];
                *q = 7;
                struct S { long a; uchar b; } s;
                s.a = t;
                s.b = 9;
                return (int)((v + s.a - 1300000L) & 0xFFFF) + s.b + (int)(total(table, 3) >> 16) + (int)sizeof(long);
            }
            """;

        // 120000 + 1300002 - 1300000 = 120002; & 0xFFFF = 54466; + 9 + (1300007 >> 16 = 19) + 4 = 54498
        CcRun.RunOn(Source, cpu).Value.Should().Be(54498);
    }

    [Fact]
    public void Literals_Get_Their_Types()
    {
        CcRun.Run("int main() { return sizeof(100000) * 100 + sizeof(7L) * 10 + sizeof(7U); }").Value.Should().Be(400 + 40 + 2);
    }

    [Fact]
    public void Narrowing_A_Long_Warns_And_A_Cast_Does_Not()
    {
        CcRun.Compile("int main() { long a = 70000; int b = a; return b; }", "-Werror").Exit.Should().NotBe(0);
        CcRun.Compile("int main() { long a = 70000; int b = (int)a; return b; }", "-Werror").Exit.Should().Be(0);
    }
}
