using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class CPreprocessorTests
{
    private static string Run(string source, Dictionary<string, string>? files = null, Dictionary<string, string>? defines = null) =>
        CPreprocessor.Process(source, files is null ? null : files.GetValueOrDefault, defines);

    [Fact]
    public void Object_Macros_Substitute_And_Chain()
    {
        Run("#define A B\n#define B 7\nint x = A;").Should().Contain("int x = 7;");
    }

    [Fact]
    public void Macros_Keep_Line_Numbers_And_Skip_Strings_And_Comments()
    {
        string[] lines = Run("#define X 1\nint a;\nchar *s = \"X X\"; // X\nint b = X; /* X */ int c = X;").Split('\n');

        lines[1].Should().Be("int a;");
        lines[2].Should().Be("char *s = \"X X\"; // X");
        lines[3].Should().Be("int b = 1; /* X */ int c = 1;");
    }

    [Fact]
    public void Function_Macros_Take_Arguments_With_Nesting_And_Commas_In_Parens()
    {
        const string Source = """
            #define MAX(a, b) ((a) > (b) ? (a) : (b))
            #define SQ(x) ((x) * (x))
            #define ADD3(a, b, c) (a + b + c)
            int m = MAX(f(1, 2), SQ(MAX(3, 4)));
            int z = ADD3(1, , 3);
            """;

        string text = Run(Source);

        text.Should().Contain("int m = ((f(1, 2)) > (((((3) > (4) ? (3) : (4))) * (((3) > (4) ? (3) : (4))))) ? (f(1, 2)) : (((((3) > (4) ? (3) : (4))) * (((3) > (4) ? (3) : (4))))));");
        text.Should().Contain("int z = (1 +  + 3);");
    }

    [Fact]
    public void Function_Macro_Without_Parentheses_Is_Left_Alone_And_Self_Reference_Is_Safe()
    {
        Run("#define F(x) x\n#define A A + 1\nint F;\nint y = A;").Should().Contain("int F;").And.Contain("int y = A + 1;");
    }

    [Fact]
    public void Undef_And_Redefinition()
    {
        Run("#define X 1\n#undef X\n#define X 2\nint v = X;").Should().Contain("int v = 2;");
        Run("#define X 1\n#define X 1\nint v = X;").Should().Contain("int v = 1;");
    }

    [Fact]
    public void Conditionals_Select_Branches_And_Nest()
    {
        const string Source = """
            #define LEVEL 2
            #ifdef LEVEL
            int a = LEVEL;
            #else
            int a = 0;
            #endif
            #ifndef LEVEL
            int b;
            #endif
            #if LEVEL > 1 && defined(LEVEL) && !defined MISSING
            int c1;
            #elif LEVEL == 1
            int c2;
            #else
            int c3;
            #endif
            #if 0
            #if 1
            int hidden;
            #endif
            #else
            int shown;
            #endif
            """;

        string text = Run(Source);

        text.Should().Contain("int a = 2;").And.Contain("int c1;").And.Contain("int shown;");
        text.Should().NotContain("int b;").And.NotContain("int c2;").And.NotContain("int c3;").And.NotContain("hidden");
        text.Split('\n').Should().HaveCount(Source.Split('\n').Length);
    }

    [Fact]
    public void If_Expressions_Support_Arithmetic_Hex_Char_And_Ternary()
    {
        Run("#if (1 << 4) + 0x10 == 32 && 'A' == 65 && (3 > 2 ? 1 : 0)\nint ok;\n#endif").Should().Contain("int ok;");
        Run("#if UNKNOWN\nint no;\n#endif").Should().NotContain("int no;");
    }

    [Fact]
    public void Include_Guards_And_Pragma_Once_And_System_Includes()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["a.h"] = "#ifndef A_H\n#define A_H\nint a;\n#endif\n",
            ["once.h"] = "#pragma once\nint once;\n",
            ["<sys.h>"] = "int sys;\n",
        };

        string text = Run("#include \"a.h\"\n#include \"a.h\"\n#include \"once.h\"\n#include \"once.h\"\n#include <sys.h>", files);

        text.Split('\n').Count(static l => l == "int a;").Should().Be(1);
        text.Split('\n').Count(static l => l == "int once;").Should().Be(1);
        text.Should().Contain("int sys;");
    }

    [Fact]
    public void Command_Line_Defines_Work_As_Macros()
    {
        Run("#ifdef DEBUG\nint d = LEVEL;\n#endif", defines: new() { ["DEBUG"] = "1", ["LEVEL"] = "9" }).Should().Contain("int d = 9;");
    }

    [Fact]
    public void Line_Continuation_Joins_Macro_Bodies()
    {
        string[] lines = Run("#define TWO(a) \\\n  (a + \\\n   a)\nint v = TWO(3);").Split('\n');

        lines[3].Should().Be("int v = (3 +    3);");
    }

    [Theory]
    [InlineData("#error stop here", "#error stop here")]
    [InlineData("#define int 5", "keyword")]
    [InlineData("#define X 1\n#define X 2", "redefined differently")]
    [InlineData("#include \"nope.h\"", "not found")]
    [InlineData("#include nope", "#include needs")]
    [InlineData("#ifdef X\nint a;", "unterminated")]
    [InlineData("#endif", "without #if")]
    [InlineData("#else", "without #if")]
    [InlineData("#ifdef A\n#else\n#else\n#endif", "duplicate #else")]
    [InlineData("#define F(a) a\nint x = F(1, 2);", "takes 1 arguments, got 2")]
    [InlineData("#define F(a) a\nint x = F(1;", "unterminated argument list")]
    [InlineData("#bogus", "unknown directive")]
    [InlineData("#if 1 / 0\n#endif", "division by zero")]
    [InlineData("#define 1x 2", "needs a name")]
    public void Bad_Input_Is_Rejected(string source, string message)
    {
        FluentActions.Invoking(() => Run(source, new Dictionary<string, string>()))
            .Should().Throw<CPreprocessException>().WithMessage($"*{message}*");
    }

    [Fact]
    public void Include_Cycles_Are_Rejected()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["a.h"] = "#include \"b.h\"\n",
            ["b.h"] = "#include \"a.h\"\n",
        };

        FluentActions.Invoking(() => Run("#include \"a.h\"", files))
            .Should().Throw<CPreprocessException>().WithMessage("*cycle*");
    }

    [Fact]
    public void Macro_Expansion_Depth_Is_Bounded()
    {
        FluentActions.Invoking(() => Run("#define A(x) B(x)\n#define B(x) A(x)\nint v = A(1);"))
            .Should().NotThrow();
    }
}
