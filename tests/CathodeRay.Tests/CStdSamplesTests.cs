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
}
