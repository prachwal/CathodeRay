using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 31, krok 17: <c>float</c> liczony programowo.</summary>
public sealed class CFloatTests
{
    [Theory]
    [InlineData("stub")]
    [InlineData("6502")]
    [InlineData("z80")]
    [InlineData("6800")]
    public void Float_Arithmetic_Conversions_And_Comparisons(string cpu)
    {
        const string Source = """
            float g = 1.5;
            float scale(float x, int n) { return x * n; }
            int main() {
                float a = 2.5f;
                float b = a + g;
                float c = b * 3 - 1.0;
                double d = c / 2;
                int r = 0;
                if (b == 4.0) r = r + 1;
                if (c == 11.0) r = r + 2;
                if (d == 5.5) r = r + 4;
                if (a < g) r = r + 100;
                if (g <= a && a > g && a >= 2.5 && a != g) r = r + 8;
                if (-a < 0) r = r + 16;
                r = r + (int)d * 100;
                long big = (long)(d * 1000);
                if (big == 5500) r = r + 32;
                if (scale(1.25, 4) == 5.0) r = r + 64;
                float z = 0;
                if (!z) r = r + 1000;
                a += 1;
                a++;
                if (a == 4.5) r = r + 2000;
                return r;
            }
            """;

        CcRun.RunOn(Source, cpu, "-Werror").Value.Should().Be(1 + 2 + 4 + 8 + 16 + 32 + 64 + 500 + 1000 + 2000);
    }

    [Fact]
    public void Float_Data_Ftoa_And_Errors()
    {
        const string Source = """
            #include <stdlib.h>
            #include <string.h>
            float table[3] = { 1.5, -2.25, 3 };
            struct P { float x; int n; };
            struct P p = { 0.5, 7 };
            static float half = 0.5f;
            float fromInt = 5;
            int main() {
                uchar buf[80];
                float sum = table[0] + table[1] + table[2] + p.x + half + fromInt;
                struct P q = { 2.5, 1 };
                q.x = q.x * 2;
                p.x += 1;
                uchar *at = buf;
                ftoa(sum, at); at = at + strlen(at); *at = '|'; at++;
                ftoa(q.x, at); at = at + strlen(at); *at = '|'; at++;
                ftoa(p.x, at); at = at + strlen(at); *at = '|'; at++;
                ftoa(0.1, at); at = at + strlen(at); *at = '|'; at++;
                ftoa(100000.75, at); at = at + strlen(at); *at = '|'; at++;
                itoa((int)-2.7, at, 10);
                return strcmp(buf, "8.250000|5.000000|1.500000|0.100000|100000.750000|-2");
            }
            """;

        CcRun.RunOn(Source, "6502", "-Werror").Value.Should().Be(0);
        Action modulo = () => TypeChecker.Check(Parser.Parse("int main() { float f = 1.5; return f % 2; }"));
        modulo.Should().Throw<CTypeException>().WithMessage("*float*");
        Action shift = () => TypeChecker.Check(Parser.Parse("int main() { float f = 1.5; return f << 1; }"));
        shift.Should().Throw<CTypeException>();
    }
}
