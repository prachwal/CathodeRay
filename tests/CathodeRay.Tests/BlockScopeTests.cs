using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Zakresy nazw lokalnych: błędy, które muszą zostać zgłoszone, mimo że powtórzone nazwy w innych blokach są dozwolone.</summary>
public sealed class BlockScopeTests
{
    [Fact]
    public void Redeclaring_A_Parameter_In_The_Function_Body_Is_An_Error()
    {
        var (exit, error) = CcRun.Compile("int f(int a) { int a = 2; return a; }\nint main(void) { return f(1); }\n", "--cpu", "z80");

        exit.Should().Be(1);
        error.Should().Contain("already a parameter");
    }

    [Fact]
    public void Shadowing_A_Parameter_In_A_Nested_Block_Is_Allowed()
    {
        var (exit, error) = CcRun.Compile("int f(int a) { { int a = 2; a++; } return a; }\nint main(void) { return f(1); }\n", "--cpu", "z80");

        exit.Should().Be(0, error);
    }

    [Fact]
    public void Redeclaring_A_Local_In_The_Same_Block_Is_Still_An_Error()
    {
        var (exit, _) = CcRun.Compile("int main(void) { int x = 1; int x = 2; return x; }\n", "--cpu", "z80");

        exit.Should().Be(1);
    }
}
