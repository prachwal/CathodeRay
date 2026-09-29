using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 29 E: ostrzeżenia checkera i cc -Werror.</summary>
public sealed class CWarningTests
{
    private static IReadOnlyList<string> Warnings(string source) =>
        TypeChecker.Check(Parser.Parse(source)).Warnings;

    [Fact]
    public void Unused_Variables_And_Parameters_Are_Reported_With_Lines()
    {
        IReadOnlyList<string> warnings = Warnings("int f(int used, int spare) {\n    int dead = 1;\n    int alive = used;\n    return alive;\n}\nint main() { return f(1, 2); }");

        warnings.Should().Contain("line 1: unused parameter 'spare'.");
        warnings.Should().Contain("line 2: unused variable 'dead'.");
        warnings.Should().NotContain(w => w.Contains("'used'") || w.Contains("'alive'"));
    }

    [Fact]
    public void Variables_Used_Through_Address_Sizeof_Call_And_Underscore_Are_Quiet()
    {
        const string Source = """
            int f(int _ignored, int x) {
                int a = 0;
                int b = 0;
                int c[2];
                int (*fn)(int, int) = f;
                int *p = &a;
                *p = sizeof(b);
                c[0] = 1;
                return fn(0, 1) + c[0];
            }
            int main() { return f(0, 1); }
            """;

        Warnings(Source).Where(static w => w.Contains("unused")).Should().Equal("line 1: unused parameter 'x'.");
    }

    [Fact]
    public void Missing_Return_Is_Reported_Only_When_Flow_Can_Fall_Off()
    {
        Warnings("int f(int x) { if (x) return 1; }\nint main() { return f(0); }").Should().Contain(w => w.Contains("'f' may reach its end"));
        Warnings("int f(int x) { if (x) return 1; else return 2; }\nint main() { return f(0); }").Should().NotContain(w => w.Contains("may reach"));
        Warnings("int f() { while (1) { } }\nint main() { return f(); }").Should().NotContain(w => w.Contains("may reach"));
        Warnings("int f() { while (1) { break; } }\nint main() { return f(); }").Should().Contain(w => w.Contains("may reach"));
        Warnings("int f(int x) { switch (x) { case 1: return 1; default: return 2; } }\nint main() { return f(0); }").Should().NotContain(w => w.Contains("may reach"));
        Warnings("int f(int x) { switch (x) { case 1: return 1; } }\nint main() { return f(0); }").Should().Contain(w => w.Contains("may reach"));
        Warnings("void v() { }\nint main() { v(); return 0; }").Should().NotContain(w => w.Contains("may reach"));
        Warnings("int f() { goto out; out: return 1; }\nint main() { return f(); }").Should().NotContain(w => w.Contains("may reach"));
    }

    [Fact]
    public void Unreachable_Code_After_Return_Break_Continue_And_Goto()
    {
        Warnings("int main() {\n    return 1;\n    return 2;\n}").Should().Contain("line 3: unreachable code.");
        Warnings("int main() {\n    while (1) {\n        break;\n        return 3;\n    }\n    return 0;\n}").Should().Contain("line 4: unreachable code.");
        Warnings("int main() {\n    goto end;\n    end:\n    return 0;\n}").Should().NotContain(w => w.Contains("unreachable"));
        Warnings("int main() { return 0; ; }").Should().NotContain(w => w.Contains("unreachable"));
    }

    [Fact]
    public void Assignment_As_Condition_Warns()
    {
        Warnings("int main() {\n    int a = 0;\n    int b = 2;\n    if (a = b) return 1;\n    return a;\n}").Should().Contain("line 4: assignment used as a condition (did you mean '=='?).");
        Warnings("int main() { int a = 0; int b = 2; if (a == b) return 1; return 0; }").Should().NotContain(w => w.Contains("assignment"));
    }

    [Fact]
    public void Cc_Prints_Warnings_And_Werror_Fails_The_Build()
    {
        string dir = Directory.CreateTempSubdirectory("cathode-werror-").FullName;
        try
        {
            string path = Path.Combine(dir, "w.c");
            File.WriteAllText(path, "int main() {\n    int unused = 1;\n    return 0;\n}\n");
            string bin = Path.Combine(dir, "w.bin");

            var ok = new StringWriter();
            int exit = CathodeRay.Cli.CliApp.CreateRoot().Parse(["cc", path, "-o", bin]).Invoke(new System.CommandLine.InvocationConfiguration { Output = new StringWriter(), Error = ok });
            exit.Should().Be(0, ok.ToString());
            ok.ToString().Should().Contain("w.c: warning: line 2: unused variable 'unused'.");

            var strict = new StringWriter();
            exit = CathodeRay.Cli.CliApp.CreateRoot().Parse(["cc", path, "-o", bin, "-Werror"]).Invoke(new System.CommandLine.InvocationConfiguration { Output = new StringWriter(), Error = strict });
            exit.Should().NotBe(0);
            strict.ToString().Should().Contain("treated as errors");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Standard_Library_Modules_Do_Not_Spam_User_Warnings()
    {
        CcRun.Result result = CcRun.Run("#include <stdio.h>\nint main() { printf(\"%d\", 5); return 0; }");

        result.Stderr.Should().BeEmpty();
    }
}
