using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Regresja: rozszerzenie znakiem lewego operandu rzutowania <c>(long)a + (long)b</c> nie może leżeć w komórce tymczasowej prawego operandu.</summary>
public sealed class CCastDepthTests
{
    [Theory]
    [InlineData("stub")]
    [InlineData("6502")]
    [InlineData("z80")]
    [InlineData("6800")]
    public void Sum_Of_Two_Widened_Casts_Keeps_The_Left_Operand(string cpu)
    {
        const string Source = """
            long g(int a, uint b) { return (long)a + (long)b; }
            int main() { return (int)g(0, 17337) + (int)g(0 - 1, 5); }
            """;

        // g(0, 17337) = 17337; g(-1, 5) = 4
        CcRun.RunOn(Source, cpu).Value.Should().Be(17341);
    }
}
