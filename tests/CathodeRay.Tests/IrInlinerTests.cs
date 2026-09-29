using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 31, krok 10: inlining małych funkcji liści na poziomie IR.</summary>
public sealed class IrInlinerTests
{
    private static Ir.Module Lower(string source) => Codegen.Lower(TypeChecker.Check(Parser.Parse(source)));

    [Fact]
    public void Small_Leaf_Is_Inlined_And_Unused_Static_Definition_Disappears()
    {
        Ir.Module module = Lower("static int twice(int x) { return x + x; }\nint main() { return twice(21); }");

        module.Functions.Select(static f => f.Name).Should().Equal("main");
        module.Functions[0].Body.OfType<Ir.Call>().Should().BeEmpty();
    }

    [Fact]
    public void Exported_Function_Stays_Defined_But_Calls_Are_Inlined()
    {
        Ir.Module module = Lower("int twice(int x) { return x + x; }\nint main() { return twice(21); }");

        module.Functions.Select(static f => f.Name).Should().Contain(["twice", "main"]);
        module.Functions.Single(static f => f.Name == "main").Body.OfType<Ir.Call>().Should().BeEmpty();
    }

    [Fact]
    public void Functions_With_Address_Taken_Or_Calls_Are_Not_Inlined()
    {
        Ir.Module module = Lower("int leaf(int x) { return x + 1; }\nint mid(int x) { return leaf(x) + 2; }\nint main() { int (*f)(int) = leaf; return f(1) + mid(2); }");

        module.Functions.Single(static f => f.Name == "main").Body.OfType<Ir.Call>().Should().Contain(static c => c.Direct == "mid");
        module.Functions.Select(static f => f.Name).Should().Contain("leaf");
    }

    [Theory]
    [InlineData("int abs2(int x) { if (x < 0) return 0 - x; return x; }\nint main() { return abs2(0 - 7) * 10 + abs2(3); }", 73)]
    [InlineData("static int sq(int x) { return x * x; }\nint main() { int i; int s = 0; for (i = 1; i < 6; i++) s = s + sq(i); return s; }", 55)]
    [InlineData("int clamp(int v, int lo, int hi) { if (v < lo) return lo; if (v > hi) return hi; return v; }\nint main() { return clamp(5, 1, 3) * 100 + clamp(0 - 4, 0, 9) * 10 + clamp(7, 0, 9); }", 307)]
    public void Inlined_Programs_Give_The_Same_Result_On_Every_Target(string source, int expected)
    {
        foreach (string cpu in TargetHarness.Targets.Select(static t => t.Name))
        {
            CcRun.RunOn(source, cpu).Value.Should().Be(expected, cpu);
        }
    }
}
