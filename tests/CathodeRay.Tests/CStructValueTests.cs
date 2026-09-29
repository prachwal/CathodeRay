using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 30, krok 16: struktury przez wartość — argument (kopia) i wynik (ukryty parametr sret).</summary>
public sealed class CStructValueTests
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
        string source = File.ReadAllText(Repo.Path("samples", "minic", "19_struct_value.c"));

        CcRun.RunOn(source, cpu).Value.Should().Be(936);
    }

    [Fact]
    public void Argument_Is_A_Copy()
    {
        const string Source = """
            struct P { int a; int b; };
            int poke(struct P p) { p.a = 99; return p.a + p.b; }
            int main() {
                struct P p;
                p.a = 1;
                p.b = 2;
                int r = poke(p);
                return r * 10 + p.a;
            }
            """;

        CcRun.Run(Source).Value.Should().Be(1011);
    }

    [Fact]
    public void Result_Can_Be_Used_Directly_As_A_Member_Or_Discarded()
    {
        const string Source = """
            struct P { int a; uchar b; };
            struct P make(int a) { struct P p; p.a = a; p.b = 5; return p; }
            int main() {
                make(1);
                return make(40).a + make(2).b;
            }
            """;

        CcRun.Run(Source).Value.Should().Be(45);
    }

    [Fact]
    public void Function_Pointer_Can_Return_And_Take_A_Struct()
    {
        const string Source = """
            struct P { int a; int b; };
            struct P swap(struct P p) { struct P r; r.a = p.b; r.b = p.a; return r; }
            int main() {
                struct P (*f)(struct P) = swap;
                struct P p;
                p.a = 3;
                p.b = 8;
                struct P q = f(p);
                return q.a * 10 + q.b;
            }
            """;

        CcRun.Run(Source).Value.Should().Be(83);
    }

    [Fact]
    public void Large_Struct_Return_Is_Rejected()
    {
        (int exit, string error) = CcRun.Compile("struct P { uchar a[100]; };\nstruct P f() { struct P p; return p; }\nint main() { return 0; }");

        exit.Should().NotBe(0);
        error.Should().Contain("at most 64");
    }

    [Fact]
    public void Mismatched_Struct_Types_Are_Rejected()
    {
        (int exit, string error) = CcRun.Compile("struct A { int a; };\nstruct B { int a; };\nint f(struct A x) { return x.a; }\nint main() { struct B b; b.a = 1; return f(b); }");

        exit.Should().NotBe(0);
        error.Should().Contain("cannot convert");
    }
}
