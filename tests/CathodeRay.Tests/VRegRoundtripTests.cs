using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 37, zadanie 2: podniesienie Cell→VReg i opadnięcie VReg→Cell zachowują semantykę —
/// ten sam asembler po obu ścieżkach i ten sam wynik interpreterów.</summary>
public sealed class VRegRoundtripTests
{
    public static TheoryData<string, string, int> Programs()
    {
        var data = new TheoryData<string, string, int>
        {
            { "ret42", "int main() { return 42; }", 42 },
            { "fib", "int fib(int n) { if (n <= 1) return n; return fib(n - 1) + fib(n - 2); } int main() { return fib(10); }", 55 },
            { "mutual", "int is_even(int n); int is_odd(int n); int is_even(int n) { if (n == 0) return 1; return is_odd(n - 1); } int is_odd(int n) { if (n == 0) return 0; return is_even(n - 1); } int main() { return is_even(10) + (is_odd(10) ? 0 : 2) + (is_odd(11) ? 4 : 0) + (is_even(11) ? 0 : 8); }", 15 },
            { "goto", "int g(int n) { int x; x = n * 3; if (n == 0) return 0; g(n - 1); if (n & 1) goto skip; x = 100; skip: return x; } int main() { return g(1) * 10 + g(3); }", 39 },
            { "loop", "int h(int n) { int s; int i; if (n == 0) return 1; s = n; for (i = 0; i < 2; i = i + 1) { s = s + h(n - 1); } return s; } int main() { return h(3); }", 19 },
            { "struct", "struct P { int x; int y; }; int main() { struct P p; p.x = 3; p.y = 4; return p.x * 10 + p.y; }", 34 },
            { "funcptr", "int add(int a, int b) { return a + b; } int main() { int (*f)(int, int) = add; return f(20, 22); }", 42 },
            { "console", "void putchar(uchar c);\nint main() { putchar(65); putchar(66); return 7; }", 7 },
        };
        return data;
    }

    private static Ir.Module LowerCell(string source) =>
        Codegen.Lower(TypeChecker.Check(Parser.Parse(source, StdLib.HeaderReader)), "t.c");

    [Theory]
    [MemberData(nameof(Programs))]
    public void Roundtrip_Emits_Identical_Assembly(string name, string source, int expected)
    {
        _ = name;
        _ = expected;
        Ir.Module cell = LowerCell(source);
        VReg.Module vreg = VRegLift.Run(cell);
        string before = CTargets.Default.Emit(cell, true);
        string after = CTargets.Default.Emit(VRegToCell.Run(vreg), true);
        after.Should().Be(before, "lift + opadnięcie nie mogą zmienić kodu");
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public void Interpreters_Agree_On_Value_And_Console(string name, string source, int expected)
    {
        _ = name;
        Ir.Module cell = LowerCell(source);
        var left = IrInterpreter.Load([cell]);
        (int lv, int lw) = left.RunMain();
        var right = VRegInterpreter.Load([VRegLift.Run(cell)]);
        (int rv, int rw) = right.RunMain();
        rv.Should().Be(lv);
        rw.Should().Be(lw);
        lv.Should().Be(expected);
        right.Console.Should().Be(left.Console);
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public void Pipeline_Emits_Identical_Assembly(string name, string source, int expected)
    {
        _ = name;
        _ = expected;
        CheckedProgram program = TypeChecker.Check(Parser.Parse(source, StdLib.HeaderReader));
        string before = Codegen.Emit(program, CTargets.Default, "t.c", objectMode: true);
        string after = VRegPipeline.Emit(program, CTargets.Default, "t.c", objectMode: true);
        after.Should().Be(before, "potok VReg daje ten sam asembler co Cell");
    }
}
