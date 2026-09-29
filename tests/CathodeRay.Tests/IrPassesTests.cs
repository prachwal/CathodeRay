using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 30, krok 5: przebiegi na kodzie pośrednim (ramki tylko na cyklach grafu wołań, redukcja siły,
/// przekazywanie tymczasowych) — poprawność sprawdza wyrocznia różnicowa, tu sprawdzamy kształt wyniku.</summary>
public sealed class IrPassesTests
{
    private static Ir.Module Lower(string source, bool objectMode = false) =>
        Codegen.Lower(TypeChecker.Check(Parser.Parse(source, StdLib.HeaderReader)), "t.c", objectMode);

    private static Ir.Function Fn(Ir.Module module, string name) => module.Functions.Single(f => f.Name == name);

    private static int Result(string source)
    {
        var (cpu, _, _) = CCodegenTests.RunC(source);
        return (cpu.State.X * 256) + cpu.State.A;
    }

    [Fact]
    public void Only_Functions_On_A_Call_Cycle_Get_A_Frame()
    {
        const string Source = """
            int leaf(int a) { int t = a + 1; return t; }
            int fact(int n) { if (n <= 1) return 1; return n * fact(n - 1); }
            int even(int n);
            int odd(int n) { if (n == 0) return 0; return even(n - 1); }
            int even(int n) { if (n == 0) return 1; return odd(n - 1); }
            int main() { return leaf(1) + fact(4) + even(6); }
            """;
        Ir.Module module = Lower(Source);

        Fn(module, "leaf").Saved.Should().BeEmpty();
        Fn(module, "main").Saved.Should().BeEmpty();
        Fn(module, "fact").Saved.Select(static o => o.Sym).Should().Contain("fact__n");
        Fn(module, "odd").Saved.Should().NotBeEmpty();
        Fn(module, "even").Saved.Should().NotBeEmpty();
        Result(Source).Should().Be(2 + 24 + 1);
    }

    [Fact]
    public void Indirect_Calls_And_Address_Taken_Functions_Form_Cycles()
    {
        const string Source = """
            typedef int (*step)(int);
            int down(int n);
            step next = down;
            int down(int n) { if (n <= 0) return 0; return 1 + next(n - 1); }
            int plain(int n) { return n + 1; }
            int main() { return down(5) + plain(1); }
            """;
        Ir.Module module = Lower(Source);

        Fn(module, "down").Saved.Should().NotBeEmpty();
        Fn(module, "plain").Saved.Should().BeEmpty();
        Result(Source).Should().Be(5 + 2);
    }

    [Fact]
    public void Calls_To_Unknown_Externals_Can_Come_Back_But_Stdlib_And_Console_Cannot()
    {
        const string Source = """
            #include <string.h>
            void putchar(uchar c);
            int callback(int n);
            int quiet(const uchar *s) { putchar('x'); return strlen(s); }
            int loud(int n) { return callback(n) + 1; }
            int main() { return quiet("ab") + loud(1); }
            """;
        Ir.Module module = Lower(Source, objectMode: true);

        Fn(module, "quiet").Saved.Should().BeEmpty();
        Fn(module, "loud").Saved.Should().NotBeEmpty();
        Fn(module, "main").Saved.Should().NotBeEmpty();
    }

    [Fact]
    public void Static_Functions_Without_Unknown_Callers_Stay_Frameless()
    {
        const string Source = "static int hidden(int n) { int t = n + 2; t = t ^ n; t = t + 5; t = t ^ 9; t = t + n; t = t ^ 3; t = t + 7; t = t ^ n; t = t + 1; t = t ^ 6; t = t + n; t = t ^ 2; return t; }\nint callback(int n);\nint main() { return hidden(1) + hidden(2) + callback(3); }";
        Ir.Module module = Lower(Source, objectMode: true);

        Fn(module, "hidden").Saved.Should().BeEmpty();
        Fn(module, "main").Saved.Should().NotBeEmpty();
    }

    [Fact]
    public void Local_Aggregates_Are_Saved_Only_In_Framed_Functions_And_Limited_To_64_Bytes()
    {
        Ir.Module module = Lower("int flat() { uchar buf[100]; buf[0] = 1; return buf[0]; }\nint rec(int n) { uchar tag[3] = {1, 2, 3}; if (n > 0) rec(n - 1); return tag[1]; }\nint main() { return flat() + rec(2); }");

        Fn(module, "flat").Saved.Should().BeEmpty();
        Fn(module, "rec").Saved.Should().Contain(static o => o.Aggregate && o.Size == 3);
    }

    [Theory]
    [InlineData("x * 8", Ir.BinOp.Shl)]
    [InlineData("8 * x", Ir.BinOp.Shl)]
    [InlineData("x / 4", Ir.BinOp.Shr)]
    [InlineData("x % 16", Ir.BinOp.And)]
    public void Multiplication_Division_And_Modulo_By_Powers_Of_Two_Become_Shifts_And_Masks(string expression, Ir.BinOp expected)
    {
        Ir.Module module = Lower($"uint x = 100;\nuint main() {{ return {expression}; }}");

        Fn(module, "main").Body.OfType<Ir.Bin>().Select(static b => b.Kind).Should().Contain(expected)
            .And.NotContain([Ir.BinOp.Mul, Ir.BinOp.Div, Ir.BinOp.Mod]);
    }

    [Theory]
    [InlineData("x * 8", 800)]
    [InlineData("x / 4", 25)]
    [InlineData("x % 16", 4)]
    [InlineData("x * 1", 100)]
    [InlineData("x / 1", 100)]
    [InlineData("x * 3", 300)]
    [InlineData("x / 3", 33)]
    public void Reduced_And_Unreduced_Forms_Compute_The_Same_Values(string expression, int expected)
    {
        Result($"uint x = 100;\nint main() {{ return {expression}; }}").Should().Be(expected);
    }

    [Fact]
    public void Byte_Multiplication_By_Power_Of_Two_Wraps_Like_The_Original()
    {
        Result("uchar x = 100;\nint main() { uchar y = x * 4; return y; }").Should().Be(144);
    }

    [Fact]
    public void Signed_Division_Is_Not_Reduced()
    {
        Ir.Module module = Lower("int x = 0 - 9;\nint main() { return x / 2; }");

        Fn(module, "main").Body.OfType<Ir.Bin>().Select(static b => b.Kind).Should().Contain(Ir.BinOp.DivS);
        Result("int x = 0 - 9;\nint main() { return x / 2; }").Should().Be(0xFFFC);
    }

    [Fact]
    public void Assignments_Write_Straight_Into_The_Variable_Without_A_Temporary_Copy()
    {
        Ir.Module module = Lower("int a = 1;\nint b = 2;\nint c;\nint main() { c = a << 3; c = c + b; c = *(&a); return c; }");
        Ir.Function main = Fn(module, "main");

        main.Body.OfType<Ir.Mov>().Should().BeEmpty();
        main.Body.OfType<Ir.Bin>().Where(static b => b.Kind == Ir.BinOp.Shl).Should().OnlyContain(static b => b.Dst.Sym == "cc_g_c");
    }

    [Fact]
    public void Chained_Assignment_Keeps_The_Inner_Result_Available()
    {
        const string Source = "int a = 3;\nint b;\nint c;\nint main() { c = (b = a + 4) * 2; return b + c; }";
        Ir.Module module = Lower(Source);

        Fn(module, "main").Body.OfType<Ir.Bin>().First(static bin => bin.Kind == Ir.BinOp.Add).Dst.Sym.Should().Be("cc_g_b");
        Result(Source).Should().Be(7 + 14);
    }

    [Fact]
    public void ForwardTemporaries_Merges_Consecutive_Moves_Through_Dead_Temporary()
    {
        // Arrange: manually create IR with Mov(t, X); Mov(v, t); <other> where t is dead after Mov(v, t)
        var t = new Ir.Cell("main__t@0", 1);
        var v = new Ir.Cell("main__v", 1);
        var x = new Ir.Imm(5, 1);
        var body = new List<Ir.Ins>
        {
            new Ir.Mov(t, x),                                       // t = 5
            new Ir.Mov(v, t),                                       // v = t (t is dead after this)
            new Ir.Mov(v, new Ir.Imm(6, 1)),                       // v = 6 (t definitely not used here)
        };

        // Act: apply ForwardTemporaries
        List<Ir.Ins> optimized = IrPasses.ForwardTemporaries(body);

        // Assert: should merge first two into single Mov(v, 5)
        optimized.Should().HaveCount(2);
        optimized[0].Should().BeOfType<Ir.Mov>().Which.Dst.Sym.Should().Be("main__v");
        ((Ir.Mov)optimized[0]).Src.Should().Be(x);
        optimized[1].Should().BeOfType<Ir.Mov>();
    }
}
