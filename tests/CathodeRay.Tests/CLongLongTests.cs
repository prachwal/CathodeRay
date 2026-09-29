using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 31, krok 16: <c>long long</c> na połówkach 32-bitowych.</summary>
public sealed class CLongLongTests
{
    [Theory]
    [InlineData("stub")]
    [InlineData("6502")]
    [InlineData("z80")]
    [InlineData("6800")]
    public void LongLong_Arithmetic_Compare_And_Shifts(string cpu)
    {
        const string Source = """
            long long g = 5000000000;
            unsigned long long ug = 18446744073709551615ULL;
            long long twice(long long x) { return x + x; }
            int main() {
                long long a = 4000000000LL;
                long long b = a * 3 + g;
                long long c = b - 17000000000;
                long long d = 0 - c;
                int r = 0;
                if (b == 17000000000) r = r + 1;
                if (c == 0) r = r + 2;
                if (d == 0) r = r + 4;
                if (twice(a) == 8000000000) r = r + 8;
                if (a > g) r = r + 100; else r = r + 16;
                if ((a << 4) == 64000000000) r = r + 32;
                if ((b >> 3) == 2125000000) r = r + 64;
                if (ug + 1 == 0) r = r + 128;
                if ((ug >> 60) == 15) r = r + 256;
                long long n = 0 - 5;
                if (n < 0 && n > 0 - 6 && (n >> 1) == 0 - 3) r = r + 512;
                if (17000000000 / 1000000 == 17000 && 17000000001 % 1000 == 1) r = r + 1024;
                if ((int)(a >> 20) == 3814 && (long)g == 705032704) r = r + 2048;
                return r;
            }
            """;

        CcRun.RunOn(Source, cpu, "-Werror").Value.Should().Be(4095);
    }

    [Fact]
    public void LongLong_Struct_Array_Signed_Division_And_Lltoa()
    {
        const string Source = """
            #include <stdlib.h>
            #include <string.h>
            struct T { uchar tag; long long v; unsigned long long u; };
            long long table[3] = { 1, 0 - 2, 3000000000 };
            long long fact(int n) { long long r = 1; while (n > 1) { r = r * n; n--; } return r; }
            int main() {
                struct T t;
                uchar buf[40];
                long long s = 0;
                int i;
                long long q;
                long long m;
                t.tag = 7;
                t.v = 0 - 9000000000;
                t.u = 1;
                t.u = t.u << 63;
                for (i = 0; i < 3; i++) s += table[i];
                q = t.v / 7;
                m = t.v % 7;
                t.v++;
                t.v += 2;
                s = s + fact(20) / fact(18);
                lltoa(s, buf, 10);
                if (strcmp(buf, "3000000379") != 0) return 1;
                lltoa(t.v, buf, 10);
                if (strcmp(buf, "-8999999997") != 0) return 2;
                lltoa(q, buf, 10);
                if (strcmp(buf, "-1285714285") != 0) return 3;
                lltoa(m, buf, 10);
                if (strcmp(buf, "-5") != 0) return 4;
                ulltoa(t.u, buf, 16);
                if (strcmp(buf, "8000000000000000") != 0) return 5;
                if (sizeof(struct T) != 17 || sizeof(long long) != 8) return 6;
                return 0;
            }
            """;

        CcRun.RunOn(Source, "6502", "-Werror").Value.Should().Be(0);
        Action mixed = () => TypeChecker.Check(Parser.Parse("int main() { long long a = 1; float f = a; return 0; }"));
        mixed.Should().Throw<CTypeException>().WithMessage("*long long*");
    }
}
