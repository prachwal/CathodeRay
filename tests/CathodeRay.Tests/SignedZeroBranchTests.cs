using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan opt/abi-tax, optymalizacja 6: porównanie ze znakiem z zerem bez odejmowania
/// (test bitu znaku i zera zamiast łańcucha sub/sbc z biasem).</summary>
public sealed class SignedZeroBranchTests
{
    private static string Emit(string source, string cpu)
    {
        Ir.Module module = Codegen.Lower(TypeChecker.Check(Parser.Parse(source)), "t.c", objectMode: true);
        return CTargets.Find(cpu)!.Emit(module, optimize: true);
    }

    [Theory]
    [InlineData("z80", "bit 7,h")]
    [InlineData("8080", "ani 128")]
    [InlineData("6502", "bmi")]
    [InlineData("6800", "bmi")]
    public void Signed_Less_Than_Zero_Uses_Sign_Bit(string cpu, string needle)
    {
        Emit("int f(int n) { if (n < 0) return 1; return 0; }\nint main() { return f(-5) + f(5); }\n", cpu).Should().Contain(needle);
    }

    [Theory]
    [InlineData("z80")]
    [InlineData("8080")]
    [InlineData("6502")]
    [InlineData("6800")]
    public void All_Signed_Zero_Compares_Compute_Correctly(string cpu)
    {
        const string Source = """
            int lt(int n) { return n < 0 ? 1 : 0; }
            int ge(int n) { return n >= 0 ? 1 : 0; }
            int le(int n) { return n <= 0 ? 1 : 0; }
            int gt(int n) { return n > 0 ? 1 : 0; }
            int main() {
                int r = 0;
                r = r * 2 + lt(-32768) * 8 + lt(-1) * 4 + lt(0) * 2 + lt(1);
                r = r * 16 + ge(-1) * 8 + ge(0) * 4 + ge(1) * 2 + ge(32767);
                r = r * 16 + le(-1) * 8 + le(0) * 4 + le(32767) * 2 + le(1);
                return r + gt(-1) * 100 + gt(0) * 10 + gt(1) + gt(32767) * 1000;
            }
            """;

        // lt: 8+4=12; ge: 4+2+1=7 → r=12*16+7=199; le: 8+4=12 → r=199*16+12=3196; gt: 0+0+1+1000=1001 → 4197
        CcRun.RunOn(Source, cpu).Value.Should().Be(4197);
    }

    [Theory]
    [InlineData("z80")]
    [InlineData("8080")]
    [InlineData("6502")]
    [InlineData("6800")]
    public void Sum_Still_Works(string cpu)
    {
        const string Source = "int sum(int n) { if (n <= 0) return 0; return n + sum(n - 1); }\nint main() { return sum(5); }\n";

        CcRun.RunOn(Source, cpu).Value.Should().Be(15);
    }
}
