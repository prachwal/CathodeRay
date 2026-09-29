using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 29 C: uint (16-bit bez znaku) obok int ze znakiem.</summary>
public sealed class CUintTests
{
    private static int Run(string source)
    {
        var (cpu, _, _) = CCodegenTests.RunC(source);
        return (cpu.State.X * 256) + cpu.State.A;
    }

    [Theory]
    [InlineData("a < b", 0)]
    [InlineData("a > b", 1)]
    [InlineData("a >= 40000", 1)]
    [InlineData("b < a", 1)]
    public void Uint_Comparisons_Are_Unsigned(string expr, int expected)
    {
        Run($"int main() {{ uint a = 50000; uint b = 100; return {expr}; }}").Should().Be(expected);
    }

    [Fact]
    public void Same_Bits_Compare_Differently_As_Int_And_Uint()
    {
        const string Source = """
            int main() {
                int si = 0 - 1;
                uint ui = 65535;
                return (si < 0) * 1 + (ui < 0) * 2 + (ui > 1000) * 4 + (si > 1000) * 8;
            }
            """;
        Run(Source).Should().Be(1 + 0 + 4 + 0);
    }

    [Theory]
    [InlineData("a / b", 8333)]
    [InlineData("a % b", 2)]
    [InlineData("a >> 4", 3125)]
    [InlineData("a * 2", 34464)]
    [InlineData("a + 20000", 4464)]
    [InlineData("a - 60000", 65535 - 9999)]
    public void Uint_Arithmetic_Is_Unsigned(string expr, int expected)
    {
        Run($"int main() {{ uint a = 50000; uint b = 6; return {expr}; }}").Should().Be(expected & 0xFFFF);
    }

    [Fact]
    public void Mixed_Operands_Promote_To_Uint_And_Convert_On_Assignment()
    {
        const string Source = """
            uint big(uint x) { return x + 1; }
            int main() {
                int neg = 0 - 2;
                uint u = neg;
                uint sum = u + 3;
                uchar small = 200;
                uint widened = small + 1000;
                int back = u;
                return (u > 60000) + (sum == 1) * 2 + (widened == 1200) * 4 + (back == 0 - 2) * 8 + (big(65535) == 0) * 16;
            }
            """;
        Run(Source).Should().Be(1 + 2 + 4 + 8 + 16);
    }

    [Fact]
    public void Uint_Works_In_Pointers_Arrays_Structs_Switch_And_Globals()
    {
        const string Source = """
            struct S { uint n; uchar tag; };
            uint table[3] = {60000, 5, 65535};
            uint total = 40000 + 25000;
            int main() {
                struct S s;
                s.n = 65000;
                s.n += 500;
                uint *p = table + 2;
                uint big = *p;
                int k = 0;
                switch (big) {
                    case 65535: k = 1; break;
                    default: k = 2;
                }
                return (s.n == 65500) + (total == 65000) * 2 + k * 4 + (table[0] > table[1]) * 8 + sizeof(uint) * 16;
            }
            """;
        Run(Source).Should().Be(1 + 2 + 4 + 8 + 32);
    }

    [Fact]
    public void Mixed_Sign_Comparison_Warns_But_Constants_Do_Not()
    {
        CathodeRay.C.CheckedProgram warn = CathodeRay.C.TypeChecker.Check(CathodeRay.C.Parser.Parse("int main() { int a = 1; uint b = 2; return a < b; }"));
        warn.Warnings.Should().Contain(w => w.Contains("signed and unsigned"));
        CathodeRay.C.CheckedProgram quiet = CathodeRay.C.TypeChecker.Check(CathodeRay.C.Parser.Parse("int main() { uint b = 2; return b < 1000; }"));
        quiet.Warnings.Should().BeEmpty();
    }

    /// <summary>Plan 32, krok 11: optymalizacja dodawania 16-bitowej stałej z zerowym bajtem starszym na 6502 za pomocą inc zamiast adc #0.</summary>
    [Theory]
    [InlineData(0, 3)]
    [InlineData(0xFFFE, 1)]
    [InlineData(0x00FF, 0x0102)]
    [InlineData(0xFF00, 0xFF03)]
    public void Uint_Add_Constant_With_Zero_High_Byte_On_6502(uint input, uint expected)
    {
        // Test na 6502: x = y + 3, gdzie y to input, oczekiwany wynik to expected
        // Testujemy pełny zakres, w tym 0xFFFE+3 = 0x10001 → 0x0001 (overflow)
        string source = $$"""
            uint compute(uint y) { return y + 3; }
            int main(void) { return compute({{input}}); }
            """;
        var result = CcRun.RunOn(source, "6502");
        result.Value.Should().Be((int)(expected & 0xFFFF), $"for input {input:X4}");
        result.Stderr.Should().BeEmpty(result.Stderr);
    }

    /// <summary>Plan 32, krok 12: porównanie 16-bitowe z zerem przy użyciu `lda lo ; ora hi`.</summary>
    [Theory]
    [InlineData("6502")]
    [InlineData("z80")]
    public void Uint_Zero_Comparison_Works_On_Multiple_Targets(string cpu)
    {
        // Test porównania `x == 0` dla uint z różnymi wartościami
        // Zwracamy 1 jeśli x == 0, 0 w przeciwnym razie
        const string Source = """
            int check_zero(uint x) {
                return (x == 0) ? 1 : 0;
            }
            int main(void) {
                int result = 0;
                if (check_zero(0) == 1) result = result + 1;
                if (check_zero(0x0100) == 0) result = result + 2;
                if (check_zero(0x0001) == 0) result = result + 4;
                if (check_zero(0xFFFF) == 0) result = result + 8;
                return result;
            }
            """;
        var resultValue = CcRun.RunOn(Source, cpu);
        resultValue.Value.Should().Be(1 + 2 + 4 + 8, $"on {cpu}");
    }
}
