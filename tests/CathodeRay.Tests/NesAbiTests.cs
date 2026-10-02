using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 39, zadanie 2: pilot ABI v2 na <c>nes</c> (pierwszy argument w A/X, wynik w A/X).</summary>
public sealed class NesAbiTests
{
    private static string EmitV2(string source)
    {
        CheckedProgram program = TypeChecker.Check(Parser.Parse(source, StdLib.HeaderReader));
        return VRegPipeline.Emit(program, CTargets.Find("nes")!, "t.c", objectMode: true, optimize: true, abiV2: true);
    }

    private static CcRun.Result RunV2(string source) => CcRun.RunOn(source, "nes", "--ir", "vreg", "--abi", "v2");

    [Theory]
    [InlineData("int fib(int n) { if (n <= 1) return n; return fib(n - 1) + fib(n - 2); } int main() { return fib(10); }", 55)]
    [InlineData("int f(int n) { if (n <= 1) return 1; return n * f(n - 1); } int main() { return f(5); }", 120)]
    [InlineData("int add(int a, int b) { return a + b; } int main() { return add(20, 22); }", 42)]
    [InlineData("int id(int x) { return x; } int twice(int x) { return x + x; } int apply(int (*f)(int), int v) { return f(v); } int main() { return id(7) + apply(twice, 5); }", 17)]
    [InlineData("struct P { int x; int y; }; int sq(struct P p) { return p.x * p.x + p.y * p.y; } int main() { struct P p; p.x = 3; p.y = 4; return sq(p); }", 25)]
    [InlineData("int sw(int x) { switch (x) { case 1: return 10; case 2: return 20; default: return 99; } } int main() { return sw(2) + sw(1); }", 30)]
    [InlineData("int f8(uchar a, uchar b) { return a + b; } int main() { return f8(200, 100); }", 44)]
    public void V2_Computes_Correctly(string source, int expected)
    {
        RunV2(source).Value.Should().Be(expected);
    }

    [Fact]
    public void First_Arg_Arrives_In_Registers()
    {
        string asm = EmitV2("int add(int a, int b) { return a + b; }");
        asm.Should().Contain("tax", "starsza połowa pierwszego argumentu idzie przez X");
        asm.Should().NotContain("z:cc_arg1", "pierwszy argument nie dotyka pamięci (.extern zostają)");
    }

    [Fact]
    public void Result_Stays_In_Registers()
    {
        string add = EmitV2("int add(int a, int b) { return a + b; }").Split("add__ret:")[0];
        add.Should().NotContain("z:cc_ret", "wynik liścia nie wraca do pamięci (.extern zostają)");
    }

    [Fact]
    public void Second_Arg_Stays_In_Memory()
    {
        string asm = EmitV2("int add(int a, int b) { return a + b; }");
        asm.Should().Contain("cc_arg2", "drugi argument starą drogą (hybryda D1)");
    }

    [Fact]
    public void Indirect_Calls_Work()
    {
        const string Source = "int twice(int x) { return x + x; } int apply(int (*f)(int), int v) { return f(v); } int main() { return apply(twice, 21); }";

        RunV2(Source).Value.Should().Be(42);
    }
}
