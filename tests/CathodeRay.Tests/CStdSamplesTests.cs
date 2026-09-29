using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 29: sample'e mini-C korzystające z preprocesora, wskaźników do funkcji i biblioteki standardowej
/// (kompilowane sterownikiem <c>cc</c> jak zwykły program).</summary>
public sealed class CStdSamplesTests
{
    private static string Sample(string name) => File.ReadAllText(Repo.Path("samples", "minic", name));

    [Theory]
    [InlineData("13_strings.c", 49)]
    [InlineData("14_macros.c", 116)]
    [InlineData("15_funcptr.c", 1037)]
    [InlineData("17_casts.c", 817)]
    [InlineData("18_voidptr.c", 1103)]
    [InlineData("19_struct_value.c", 936)]
    [InlineData("20_matrix.c", 591)]
    [InlineData("21_long.c", 6360)]
    [InlineData("22_misc.c", 1261)]
    [InlineData("23_types.c", 4677)]
    [InlineData("24_float.c", 1078)]
    [InlineData("25_longlong.c", 2020)]
    public void Sample_Returns_Expected_Value(string file, int expected)
    {
        CcRun.Run(Sample(file)).Value.Should().Be(expected);
    }

    [Fact]
    public void Printf_Sample_Writes_To_The_Console()
    {
        CcRun.Result result = CcRun.Run(Sample("16_printf.c"));

        result.Console.Should().Be("sum=15 hex=ff x name\ndone\n");
        result.Value.Should().Be(20);
    }

    [Fact]
    public void Long_Sample_Prints_Thirty_Two_Bit_Numbers()
    {
        CcRun.Result result = CcRun.Run(Sample("21_long.c"));

        result.Console.Should().Be("fib=102334155 fact=1c8cfc00\nsum=2000100004 big=4000000000\n");
        result.Value.Should().Be(6360);
    }

    [Fact]
    public void Float_Sample_Prints_Decimal_Text()
    {
        CcRun.Run(Sample("24_float.c")).Console.Should().Be("19.634937\n16.711645\n-0.125000\n");
    }

    [Fact]
    public void LongLong_Sample_Prints_Sixty_Four_Bit_Numbers()
    {
        CcRun.Run(Sample("25_longlong.c")).Console.Should().Be("2880067194370816120\n2432902008176640000\n-2743766045621\n");
    }

    [Fact]
    public void Types_Sample_Reports_Flags()
    {
        CcRun.Run(Sample("23_types.c")).Console.Should().Be("flags ok\n");
    }
}
