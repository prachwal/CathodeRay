using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 31, kroki 12-13: aliasy short/unsigned/signed i <c>signed char</c> (8 bitów ze znakiem).</summary>
public sealed class CIntegerTypesTests
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
    public void Type_Names_Map_To_The_Existing_Types(string cpu)
    {
        const string Source = """
            short a = 0 - 5;
            unsigned b = 65535;
            unsigned short c = 40000;
            unsigned int d = 7;
            signed e = 0 - 3;
            long int f = 100000L;
            unsigned long g = 4000000000;
            unsigned char h = 200;
            short int i = 300;
            int main() {
                return sizeof(a) * 10000 + sizeof(f) * 1000 + sizeof(h) * 100 + (a < 0) * 10 + (b > 60000) + (c > 30000) + (int)(g >> 30) + (e < 0) + (d == 7) + (h > 100) + (i == 300);
            }
            """;

        CcRun.RunOn(Source, cpu, "-Werror").Value.Should().Be((2 * 10000) + (4 * 1000) + (1 * 100) + 10 + 1 + 1 + 3 + 1 + 1 + 1 + 1);
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void Signed_Char_Compares_And_Extends_With_Sign(string cpu)
    {
        const string Source = """
            signed char neg = 0 - 5;
            signed char pos = 100;
            uchar big = 200;
            int main() {
                int r = 0;
                int wide = neg;
                long huge = neg;
                if (neg < 0) r = r + 1;
                if (neg < pos) r = r + 2;
                if (wide == 0 - 5) r = r + 4;
                if (huge == 0 - 5L) r = r + 8;
                if (neg / 2 == 0 - 2) r = r + 16;
                if ((neg >> 1) == 0 - 3) r = r + 32;
                if (neg % 3 == 0 - 2) r = r + 64;
                signed char sum = neg + pos;
                if (sum == 95) r = r + 128;
                signed char wrap = 100 + pos;
                if (wrap < 0) r = r + 256;
                if (big > 100) r = r + 512;
                return r;
            }
            """;

        CcRun.RunOn(Source, cpu, "-Werror").Value.Should().Be(1 + 2 + 4 + 8 + 16 + 32 + 64 + 128 + 256 + 512);
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void Signed_Char_Is_Passed_Returned_And_Stored_Through_Pointers(string cpu)
    {
        const string Source = """
            signed char table[4] = {0 - 1, 2, 0 - 3, 4};
            signed char twice(signed char v) { return v + v; }
            int main() {
                int total = 0;
                int i;
                signed char *p = table;
                for (i = 0; i < 4; i++) total = total + p[i];
                signed char t = twice(0 - 20);
                total = total + t;
                return total + 100;
            }
            """;

        CcRun.RunOn(Source, cpu, "-Werror").Value.Should().Be((-1 + 2 - 3 + 4 - 40 + 100) & 0xFFFF);
    }

    [Fact]
    public void Char_Stays_Unsigned_And_Narrowing_To_Signed_Char_Warns()
    {
        CcRun.Run("int main() { char c = 200; return c > 100; }").Value.Should().Be(1);
        CcRun.Compile("int main() { int v = 5; signed char s = v; return s; }", "-Werror").Exit.Should().NotBe(0);
    }
}
