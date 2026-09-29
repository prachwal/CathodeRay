using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 31, krok 8: mnożenie przez stałą przesunięciami, 8-bitowe mnożenie i dzielenie.</summary>
public sealed class CMulDivTests
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
    public void Multiplication_By_Constants_Matches_The_Reference(string cpu)
    {
        const string Source = """
            int f3(int x) { return x * 3; }
            int f5(int x) { return x * 5; }
            int f7(int x) { return x * 7; }
            int f10(int x) { return x * 10; }
            int f15(int x) { return x * 15; }
            int f100(int x) { return x * 100; }
            uint f255(uint x) { return x * 255; }
            uchar b6(uchar x) { return x * 6; }
            ulong l9(ulong x) { return x * 9; }
            int main() {
                int r = f3(1234) + f5(-77) + f7(300) + f10(-3) + f15(7) + f100(11);
                r = r + (int)f255(3) + b6(50) + (int)(l9(123456789L) >> 8);
                return r;
            }
            """;

        int expected = (1234 * 3) + (-77 * 5) + (300 * 7) + (-3 * 10) + (7 * 15) + (11 * 100) + (3 * 255) + ((50 * 6) & 255) + (int)((123456789L * 9) >> 8);
        CcRun.RunOn(Source, cpu).Value.Should().Be(expected & 0xFFFF);
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void Byte_Multiplication_And_Division_Wrap_Like_Uchar(string cpu)
    {
        const string Source = """
            uchar mul(uchar a, uchar b) { return a * b; }
            uchar div(uchar a, uchar b) { return a / b; }
            uchar mod(uchar a, uchar b) { return a % b; }
            int main() {
                return mul(200, 3) * 1000 + div(250, 7) * 100 + mod(250, 7) * 10 + div(9, 0) + mod(9, 0) + mod(255, 255) + div(255, 128);
            }
            """;

        // uchar * stała <= 255 to uchar (zawija się na 8 bitach), uchar * 1000 to int
        int expected = (((200 * 3) & 255) * 1000) + (((250 / 7) * 100) & 255) + (((250 % 7) * 10) & 255) + 0 + 0 + 0 + (255 / 128);
        CcRun.RunOn(Source, cpu).Value.Should().Be(expected & 0xFFFF);
    }
}
