using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class CPreprocessorTests
{
    [Fact]
    public void MapDirectives_Rewrites_One_To_One()
    {
        CPreprocessor.MapDirectives("#include \"hw.inc\"\nint x;")
            .Should().Be(".include \"hw.inc\"\nint x;");
        CPreprocessor.MapDirectives("  #define LED 5\nint x = LED;")
            .Should().Be("  .define LED 5\nint x = LED;");
    }

    [Theory]
    [InlineData("#define F(x) x", "function-like")]
    [InlineData("#ifdef X", "unknown directive")]
    [InlineData("#include <hw>", "#include needs")]
    [InlineData("#define 1x 2", "needs a name")]
    [InlineData("#define X", "needs a value")]
    public void MapDirectives_Rejects_Bad_Lines(string source, string message)
    {
        FluentActions.Invoking(() => CPreprocessor.MapDirectives(source))
            .Should().Throw<CPreprocessException>().WithMessage($"*{message}*");
    }

    [Fact]
    public void Expand_Substitutes_And_Chains()
    {
        CPreprocessor.Expand(".define A B\n.define B 7\nint x = A;", null)
            .Should().Contain("int x = 7;");
    }

    [Fact]
    public void Expand_Splices_Includes_With_Reader()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["a.inc"] = "int a;\n",
        };

        CPreprocessor.Expand(".include \"a.inc\"\nint main() { return 0; }", files.GetValueOrDefault)
            .Should().Contain("int a;");
    }

    [Theory]
    [InlineData(".define A A\nint x = A;", "recursive macro")]
    [InlineData(".define X 1\n.define X 2", "already defined")]
    [InlineData(".define int 5", "keyword")]
    [InlineData(".define F(x) x", "function-like")]
    [InlineData(".include \"nope.inc\"", "not found")]
    public void Expand_Rejects_Bad_Macros_And_Includes(string source, string message)
    {
        FluentActions.Invoking(() => CPreprocessor.Expand(source, _ => null))
            .Should().Throw<CPreprocessException>().WithMessage($"*{message}*");
    }

    [Fact]
    public void Expand_Rejects_Include_Cycles()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["a.inc"] = ".include \"b.inc\"\n",
            ["b.inc"] = ".include \"a.inc\"\n",
        };

        FluentActions.Invoking(() => CPreprocessor.Expand(".include \"a.inc\"", files.GetValueOrDefault))
            .Should().Throw<CPreprocessException>().WithMessage("*cycle*");
    }

    [Fact]
    public void Expand_Keeps_Line_Numbers_Without_Includes()
    {
        string expanded = CPreprocessor.Expand(".define X 1\nint a;\nint b = X;", null);
        string[] lines = expanded.Split('\n');

        lines[1].Should().Be("int a;");
        lines[2].Should().Be("int b = 1;");
    }
}
