using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 30, krok 5: interpreter IR jako wyrocznia niezależna od procesora.</summary>
public sealed class IrInterpreterTests
{
    private static (int Value, string Console) Run(string source)
    {
        var (value, _, console) = IrOracle.Run(TypeChecker.Check(Parser.Parse(source, StdLib.HeaderReader)))!.Value;
        return (value, console);
    }

    [Fact]
    public void Runs_Recursion_With_Saved_Frames()
    {
        Run("int fib(int n) { if (n < 2) return n; return fib(n - 1) + fib(n - 2); }\nint main() { return fib(10); }").Value.Should().Be(55);
    }

    [Fact]
    public void Runs_Pointers_Structs_Function_Pointers_Statics_And_Runtime_Init()
    {
        const string Source = """
            struct P { uchar a; int b; };
            typedef int (*op)(int, int);
            int add(int x, int y) { return x + y; }
            int mul(int x, int y) { return x * y; }
            op table[2] = { add, mul };
            struct P pts[2] = { {1, 10}, {2, 20} };
            int total = pts[0].b + pts[1].b;
            int calls;
            int next() { static int n = 100; calls++; return n++; }
            int seed = next();
            int main() {
                struct P copy = pts[1];
                int *p = &copy.b;
                *p += 5;
                uchar buf[4] = {1, 2, 3};
                buf[3] = buf[0] + buf[2];
                int neg = 0 - 7;
                return total + copy.b + table[0](2, 3) + table[1](4, 5) + seed + calls + buf[3] + neg / 2;
            }
            """;
        Run(Source).Value.Should().Be(30 + 25 + 5 + 20 + 100 + 1 + 4 - 3);
    }

    [Fact]
    public void Signed_And_Unsigned_Arithmetic_Match_The_Language_Rules()
    {
        const string Source = """
            int main() {
                int a = 0 - 300;
                uint b = 65535;
                uchar c = 200;
                int r = 0;
                r += (a / 7 == 0 - 42) + (a % 7 == 0 - 6) * 2 + (a >> 2 == 0 - 75) * 4;
                r += (b > 1000) * 8 + (b / 256 == 255) * 16 + ((c + 100) == 300) * 32;
                r += (a < 0) * 64 + ((c << 1) == 144) * 128;
                return r;
            }
            """;
        Run(Source).Value.Should().Be(1 + 2 + 4 + 8 + 16 + 0 + 64 + 128);
    }

    [Fact]
    public void Console_Builtins_Capture_Output()
    {
        (int value, string console) = Run("void putchar(uchar c);\nvoid putdec(int v);\nvoid puthex(uchar b);\nint main() { putchar('h'); putchar('i'); putdec(0 - 42); puthex(255); return 1; }");

        value.Should().Be(1);
        console.Should().Be("hi-42ff");
    }

    [Fact]
    public void Stdlib_Printf_Runs_On_The_Interpreter_Too()
    {
        CheckedProgram program = TypeChecker.Check(Parser.Parse("#include <stdio.h>\nint main() { return printf(\"%d-%x-%s\", 12, 255, \"ok\"); }", StdLib.HeaderReader));
        var modules = new List<Ir.Module> { Codegen.Lower(program, "main.c", objectMode: true) };
        foreach (StdModule module in StdLib.Modules.Where(static m => m.Name is "printf.c"))
        {
            modules.Add(Codegen.Lower(TypeChecker.Check(Parser.Parse(module.Source, StdLib.HeaderReader)), module.Name, objectMode: true));
        }

        var interpreter = IrInterpreter.Load(modules);
        (int value, _) = interpreter.RunMain();

        interpreter.Console.Should().Be("12-ff-ok");
        value.Should().Be("12-ff-ok".Length);
    }

    [Fact]
    public void Interpreter_Detects_A_Tampered_Program()
    {
        Ir.Module module = Codegen.Lower(TypeChecker.Check(Parser.Parse("int main() { int a = 40; int b = 2; return a + b; }")));
        Ir.Function main = module.Functions.Single(static f => f.Name == "main");
        var tampered = main with { Body = [.. main.Body.Select(static i => i is Ir.Bin { Kind: Ir.BinOp.Add } bin ? bin with { Kind = Ir.BinOp.Sub } : i)] };

        IrInterpreter.Load([module]).RunMain().Value.Should().Be(42);
        IrInterpreter.Load([module with { Functions = [tampered] }]).RunMain().Value.Should().Be(38);
    }

    [Fact]
    public void Step_Limit_Stops_Endless_Loops()
    {
        CheckedProgram program = TypeChecker.Check(Parser.Parse("int main() { while (1) { } return 0; }"));

        FluentActions.Invoking(() => IrInterpreter.Load([Codegen.Lower(program)], stepLimit: 1000).RunMain())
            .Should().Throw<InvalidOperationException>().WithMessage("*step limit*");
    }
}
