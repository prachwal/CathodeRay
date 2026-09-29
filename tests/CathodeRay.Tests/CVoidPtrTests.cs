using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 30, krok 15: <c>void *</c>, <c>size_t</c>, <c>NULL</c>, <c>offsetof</c>, pamięciowe funkcje biblioteki na <c>void *</c>.</summary>
public sealed class CVoidPtrTests
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
    public void Sample_Runs_On_Every_Target(string cpu)
    {
        string source = File.ReadAllText(Repo.Path("samples", "minic", "18_voidptr.c"));

        CcRun.RunOn(source, cpu).Value.Should().Be(1103);
    }

    [Fact]
    public void Void_Pointer_Converts_Implicitly_Both_Ways()
    {
        CcRun.Run("int main() { int x = 5; void *p = &x; int *q = p; *q = *q + 2; return x; }", "-Werror").Value.Should().Be(7);
    }

    [Theory]
    [InlineData("int main() { int x = 1; void *p = &x; return *p; }", "void pointer")]
    [InlineData("int main() { int x = 1; void *p = &x; p = p + 1; return 0; }", "void pointers")]
    [InlineData("int main() { int x = 1; void *p = &x; return p[0]; }", "void pointer")]
    [InlineData("int main() { void v; return 0; }", "void type")]
    [InlineData("int f(void x) { return 0; }\nint main() { return 0; }", "cannot be void")]
    [InlineData("int main() { const int x = 1; void *p = &x; return 0; }", "discards const")]
    public void Void_Pointer_Misuse_Is_Rejected(string source, string message)
    {
        (int exit, string error) = CcRun.Compile(source);

        exit.Should().NotBe(0);
        error.Should().Contain(message);
    }

    [Fact]
    public void Offsetof_Is_A_Compile_Time_Constant()
    {
        const string Source = """
            #include <stddef.h>
            struct S { uchar a; int b; uchar c[5]; int d; };
            int main() {
                uchar table[offsetof(struct S, d)];
                return offsetof(struct S, b) * 100 + offsetof(struct S, d) + sizeof(table);
            }
            """;

        CcRun.Run(Source).Value.Should().Be((1 * 100) + 8 + 8);
    }

    [Fact]
    public void Size_T_And_Null_Come_From_Stddef()
    {
        const string Source = """
            #include <stddef.h>
            int main() {
                size_t n = 65535;
                uchar *p = NULL;
                n = n + 2;
                return n + (p == NULL);
            }
            """;

        CcRun.Run(Source, "-Werror").Value.Should().Be(2);
    }

    [Fact]
    public void Empty_Prototype_Accepts_Void()
    {
        CcRun.Run("int seven(void) { return 7; }\nint main() { return seven(); }").Value.Should().Be(7);
    }
}
