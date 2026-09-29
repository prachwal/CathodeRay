using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 29 C: wskaźniki do funkcji.</summary>
public sealed class CFuncPtrTests
{
    private static int Run(string source)
    {
        var (cpu, _, _) = CCodegenTests.RunC(source);
        return (cpu.State.X * 256) + cpu.State.A;
    }

    [Fact]
    public void Local_Function_Pointer_Calls_Directly_And_Through_Star()
    {
        const string Source = """
            int add(int a, int b) { return a + b; }
            int mul(int a, int b) { return a * b; }
            int main() {
                int (*op)(int, int) = add;
                int r1 = op(3, 4);
                op = &mul;
                int r2 = op(5, 6);
                int r3 = (*op)(2, 7);
                return r1 * 1000 + r2 * 10 + r3;
            }
            """;
        Run(Source).Should().Be(7000 + 300 + 14);
    }

    [Fact]
    public void Typedef_Table_Of_Functions_Dispatches_By_Index()
    {
        const string Source = """
            typedef int (*binop)(int, int);
            int add(int a, int b) { return a + b; }
            int sub(int a, int b) { return a - b; }
            int mul(int a, int b) { return a * b; }
            binop table[3] = { add, sub, mul };
            int run(binop f, int x, int y) { return f(x, y); }
            int main() {
                int total = 0;
                for (int i = 0; i < 3; i++) total += table[i](10, 3) * (i + 1);
                return total + run(sub, 100, 1) * 100 + run(table[2], 2, 2);
            }
            """;
        Run(Source).Should().Be((13 * 1) + (7 * 2) + (30 * 3) + 9900 + 4);
    }

    [Fact]
    public void Callbacks_In_Struct_Fields_And_Void_Functions()
    {
        const string Source = """
            struct Handler { uchar id; void (*fire)(uchar *slot, uchar v); };
            uchar sink[2];
            void put(uchar *slot, uchar v) { *slot = v; }
            void put2(uchar *slot, uchar v) { *slot = v * 2; }
            struct Handler handlers[2] = { {1, put}, {2, put2} };
            int main() {
                handlers[0].fire(sink, 7);
                struct Handler *h = &handlers[1];
                h->fire(sink + 1, 8);
                return sink[0] * 10 + sink[1];
            }
            """;
        Run(Source).Should().Be(86);
    }

    [Fact]
    public void Function_Pointer_Comparisons_Null_And_Conditions()
    {
        const string Source = """
            int one() { return 1; }
            int two() { return 2; }
            int main() {
                int (*f)() = 0;
                int (*g)() = one;
                int r = 0;
                if (!f) r += 1;
                if (g) r += 10;
                if (g == one) r += 100;
                if (g != two) r += 1000;
                f = two;
                r += f() * 10000;
                return r;
            }
            """;
        Run(Source).Should().Be(1111 + 20000 & 0xFFFF);
    }

    [Fact]
    public void Pointers_To_Functions_Returning_Pointers_And_Taking_Pointers()
    {
        const string Source = """
            uchar data[3] = {5, 6, 7};
            uchar *pick(uchar i) { return data + i; }
            typedef uchar *(*picker)(uchar);
            int use(picker p, uchar i) { return *p(i); }
            int main() { return use(pick, 2) * 10 + use(pick, 0); }
            """;
        Run(Source).Should().Be(75);
    }

    [Fact]
    public void Indirect_Call_With_Many_Arguments_And_Nested_Calls()
    {
        const string Source = """
            int f(int a, int b, int c, int d) { return a + b * 2 + c * 3 + d * 4; }
            int id(int v) { return v; }
            int (*fp)(int, int, int, int) = f;
            int main() { return fp(id(1), id(2), id(3), fp(0, 0, 0, id(1))); }
            """;
        Run(Source).Should().Be(1 + 4 + 9 + 16);
    }

    [Fact]
    public void Function_Pointer_Errors_Are_Reported()
    {
        FluentActions.Invoking(() => Run("int a(int x) { return x; } int main() { int (*f)(int, int) = a; return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*cannot convert*");
        FluentActions.Invoking(() => Run("int a(int x) { return x; } int main() { int (*f)(int) = a; return f(1, 2); }"))
            .Should().Throw<CTypeException>().WithMessage("*takes 1 arguments*");
        FluentActions.Invoking(() => Run("int a(int x) { return x; } int main() { int (*f)(int) = a; return f + 1; }"))
            .Should().Throw<CTypeException>().WithMessage("*function pointers*");
        FluentActions.Invoking(() => Run("int x; int main() { return x(1); }"))
            .Should().Throw<CTypeException>().WithMessage("*is not a function*");
        FluentActions.Invoking(() => Run("int main() { uchar x = 0; return (*&x)(1); }"))
            .Should().Throw<CTypeException>();
    }

    [Fact]
    public void Function_Pointer_Tables_Link_Across_Modules_With_Statics()
    {
        string dir = Directory.CreateTempSubdirectory("cathode-fptr-").FullName;
        try
        {
            string a = Path.Combine(dir, "a.c");
            string b = Path.Combine(dir, "b.c");
            File.WriteAllText(a, "static int local(int x) { return x + 1; }\nint (*exported)(int) = local;\nint plain(int x) { return x * 2; }\n");
            File.WriteAllText(b, "extern int (*exported)(int);\nint plain(int x);\nint (*tab[2])(int) = { plain, plain };\nint main() { return exported(10) + tab[1](4); }\n");
            string bin = Path.Combine(dir, "p.bin");
            var err = new StringWriter();
            int exit = CathodeRay.Cli.CliApp.CreateRoot().Parse(["cc", a, b, "-o", bin])
                .Invoke(new System.CommandLine.InvocationConfiguration { Output = new StringWriter(), Error = err });
            exit.Should().Be(0, err.ToString());
            byte[] image = File.ReadAllBytes(bin);
            var bus = new CathodeRay.Stub.StubBus();
            for (int i = 0; i < image.Length; i++)
            {
                bus.Write((ushort)(0x1000 + i), image[i]);
            }

            var cpu = new CathodeRay.Stub.StubCpu(CathodeRay.Stub.StubIsa.FromJsonFile(Repo.IsaFile("mcp_stub_instructions.json")), bus);
            cpu.State.ProgramCounter = 0x1000;
            for (int steps = 0; !cpu.State.Halted; steps++)
            {
                steps.Should().BeLessThan(500_000);
                cpu.Step();
            }

            cpu.State.A.Should().Be(11 + 8);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
