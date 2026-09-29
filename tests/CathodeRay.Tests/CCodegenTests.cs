using CathodeRay.Assembler;
using CathodeRay.C;
using CathodeRay.Stub;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class CCodegenTests
{
    internal static (StubCpu Cpu, StubBus Bus, AssemblyResult Result) RunC(
        string source,
        Func<string, string?>? reader = null,
        Action<StubBus, AssemblyResult>? poke = null)
    {
        CheckedProgram checkedProgram = TypeChecker.Check(Parser.Parse(source, reader));
        string asm = Crt0.Source + Codegen.Emit(checkedProgram);
        AssemblerTarget target = AssemblerTargets.Find("stub")!;
        var origins = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["CODE"] = 0x1000,
            ["BSS"] = 0x2000,
            ["DATA"] = 0x2100,
        };
        AssemblyResult result = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax)
            .Assemble(asm, "prog.c", _ => null, [], null, origins);
        var bus = new StubBus();
        for (int i = 0; i < result.Image.Length; i++)
        {
            bus.Write((ushort)(result.Origin + i), result.Image[i]);
        }

        poke?.Invoke(bus, result);
        StubIsa isa = StubIsa.FromJsonFile(Repo.IsaFile("mcp_stub_instructions.json"));
        var cpu = new StubCpu(isa, bus);
        cpu.State.ProgramCounter = (ushort)origins["CODE"];
        for (int steps = 0; !cpu.State.Halted; steps++)
        {
            steps.Should().BeLessThan(100_000, "program ma się zatrzymać");
            cpu.Step();
        }

        return (cpu, bus, result);
    }

    [Fact]
    public void Fib_Returns_F13()
    {
        const string Source = """
            int fib(int n) {
                if (n < 2) return n;
                return fib(n - 1) + fib(n - 2);
            }
            int main() { return fib(7); }
            """;
        var (cpu, _, _) = RunC(Source);

        cpu.State.A.Should().Be(13);
    }

    [Fact]
    public void Recursive_Sum_Reaches_Depth_11()
    {
        const string Source = """
            int sum(int n) {
                if (n <= 0) return 0;
                return n + sum(n - 1);
            }
            int main() { return sum(10); }
            """;
        var (cpu, _, _) = RunC(Source);

        cpu.State.A.Should().Be(55);
    }

    [Fact]
    public void Loop_Sums_One_To_Ten()
    {
        const string Source = """
            int main() {
                int s = 0;
                int i;
                for (i = 1; i <= 10; i = i + 1) s = s + i;
                return s;
            }
            """;
        var (cpu, _, _) = RunC(Source);

        cpu.State.A.Should().Be(55);
    }

    [Fact]
    public void Mul_Div_Mod_Via_Lib()
    {
        const string Source = """
            int main() {
                uchar m = 6 * 7;
                uchar d = m / 5;
                uchar r = m % 5;
                if (d == 8) { if (r == 2) return 1; }
                return 0;
            }
            """;
        var (cpu, _, _) = RunC(Source);

        cpu.State.A.Should().Be(1);
    }

    [Fact]
    public void Shifts_And_Logic()
    {
        const string Source = """
            int main() {
                uchar x = 3;
                x = x << 2;
                x = x | 240;
                x = x & 63;
                x = x ^ 63;
                x = ~x;
                if (x == 252) return 1;
                return 0;
            }
            """;
        var (cpu, _, _) = RunC(Source);

        cpu.State.A.Should().Be(1);
    }

    [Fact]
    public void Int_Add_Sub_Compare()
    {
        const string Source = """
            int add(int a, int b) { return a + b; }
            int main() {
                int x = add(4660, 34);
                int y = x - 18;
                if (y == 4676) { if (y > 4675) { if (y < 4677) return 1; } }
                return 0;
            }
            """;
        var (cpu, _, _) = RunC(Source);

        cpu.State.A.Should().Be(1);
    }

    [Fact]
    public void Nested_Calls_And_Ternary()
    {
        const string Source = """
            int twice(int v) { return v + v; }
            int four(int v) { return twice(twice(v)); }
            int main() {
                int x = four(3) == 12 ? 7 : 8;
                int i = 0;
                while (i < x) i = i + 1;
                return i;
            }
            """;
        var (cpu, _, _) = RunC(Source);

        cpu.State.A.Should().Be(7);
    }

    [Fact]
    public void Globals_Shared_Between_Functions()
    {
        const string Source = """
            int total;
            void bump(int v) { total = total + v; }
            int main() {
                total = 0;
                bump(20);
                bump(22);
                return total;
            }
            """;
        var (cpu, _, _) = RunC(Source);

        cpu.State.A.Should().Be(42);
    }

    [Fact]
    public void Int_Params_And_Return()
    {
        const string Source = """
            int pick(int a, int b) { if (a >= b) return a; return b; }
            int main() { return pick(300, 700); }
            """;
        var (cpu, _, _) = RunC(Source);

        cpu.State.A.Should().Be(188);
    }

    [Fact]
    public void Countdown_With_Decrement()
    {
        const string Source = """
            int main() {
                int n = 100;
                while (n != 0) n = n - 1;
                return n + 5;
            }
            """;
        var (cpu, _, _) = RunC(Source);

        cpu.State.A.Should().Be(5);
    }

    [Fact]
    public void Short_Circuit_Skips_Division_By_Zero()
    {
        const string Source = """
            int main() {
                uchar x = 0;
                if (x != 0 && 10 / x == 1) return 1;
                if (x == 0 || 10 / x == 1) return 2;
                return 3;
            }
            """;
        var (cpu, _, _) = RunC(Source);

        cpu.State.A.Should().Be(2);
    }

    [Fact]
    public void Globals_With_Initializers()
    {
        const string Source = """
            int g = 4660;
            uchar b = 7;
            int main() { return g - b; }
            """;
        var (cpu, _, _) = RunC(Source);

        cpu.State.A.Should().Be((4660 - 7) & 0xFF);
    }

    [Fact]
    public void Crt0_Zeroes_Bss_And_Sets_Stack()
    {
        const string Source = "int g; int main() { return g + 5; }";
        var (cpu, _, result) = RunC(
            Source,
            poke: (bus, res) =>
            {
                int start = res.Symbols["__bss_start"];
                for (int i = 0; i < 256; i++)
                {
                    bus.Write((ushort)(start + i), 0xAA);
                }
            });

        cpu.State.A.Should().Be(5);
        cpu.State.StackPointer.Should().Be(0xFF);
    }

    [Fact]
    public void Preprocessor_Include_And_Define_Run()
    {
        const string Source = """
            #include "hw.inc"
            #define STEP 3
            int main() {
                int i = 0;
                while (i < LIMIT) i = i + STEP;
                return i + BASE;
            }
            """;
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["hw.inc"] = "int LIMIT = 9;\n#define BASE 100\n",
        };
        var (cpu, _, _) = RunC(Source, reader: files.GetValueOrDefault);

        cpu.State.A.Should().Be(109);
    }
}
