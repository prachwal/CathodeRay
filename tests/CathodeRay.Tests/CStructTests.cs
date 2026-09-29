using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 28: typedef, goto, funkcje zwracające wskaźnik, złożone przypisania z ++ w celu,
/// inicjalizatory globalne, struct.</summary>
public sealed class CStructTests
{
    private static int Run(string source)
    {
        var (cpu, _, _) = CCodegenTests.RunC(source);
        return (cpu.State.X * 256) + cpu.State.A;
    }

    [Fact]
    public void Typedef_Aliases_Types_And_Pointers()
    {
        const string Source = """
            typedef uchar byte;
            typedef int word;
            typedef byte *bptr;
            byte tab[3] = {4, 5, 6};
            word twice(word x) { return x + x; }
            int main() {
                bptr p = tab;
                byte b = p[2];
                word w = twice(300);
                return b + w + sizeof(word) + sizeof(bptr);
            }
            """;
        Run(Source).Should().Be(6 + 600 + 2 + 2);
    }

    [Fact]
    public void Goto_Jumps_Forward_And_Backward()
    {
        const string Source = """
            int main() {
                int i = 0;
                int s = 0;
            again:
                i++;
                if (i > 5) goto done;
                s = s + i;
                goto again;
            done:
                return s;
            }
            """;
        Run(Source).Should().Be(1 + 2 + 3 + 4 + 5);
    }

    [Fact]
    public void Goto_Rejects_Missing_And_Duplicate_Labels()
    {
        FluentActions.Invoking(() => Run("int main() { goto nowhere; return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*undefined label 'nowhere'*");
        FluentActions.Invoking(() => Run("int main() { a: ; a: ; return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*duplicate label*");
    }

    [Fact]
    public void Function_Returns_Pointer()
    {
        const string Source = """
            uchar buf[4] = {10, 20, 30, 40};
            int nums[2] = {1000, 2000};
            uchar *at(int i) { return buf + i; }
            int *second() { return nums + 1; }
            uchar *none() { return 0; }
            int main() {
                uchar *p = at(2);
                *at(1) = 99;
                int *q = second();
                int isnull = none() == 0;
                return *p + buf[1] + *q + isnull;
            }
            """;
        Run(Source).Should().Be(30 + 99 + 2000 + 1);
    }

    [Fact]
    public void Pointer_Returning_Prototype_Links_Across_Modules()
    {
        string dir = Directory.CreateTempSubdirectory("cathode-ptr-").FullName;
        try
        {
            string a = Path.Combine(dir, "a.c");
            string b = Path.Combine(dir, "b.c");
            File.WriteAllText(a, "uchar data[3] = {7, 8, 9};\nuchar *third() { return data + 2; }\n");
            File.WriteAllText(b, "uchar *third();\nint main() { return *third(); }\n");
            var err = new StringWriter();
            int exit = CathodeRay.Cli.CliApp.CreateRoot().Parse(["cc", a, b, "-o", Path.Combine(dir, "p.bin")])
                .Invoke(new System.CommandLine.InvocationConfiguration { Output = new StringWriter(), Error = err });
            exit.Should().Be(0, err.ToString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Pointers_Compare_With_Each_Other_And_With_Null()
    {
        const string Source = """
            uchar buf[4];
            int main() {
                uchar *a = buf;
                uchar *b = buf + 3;
                uchar *z = 0;
                return (a < b) + (b > a) * 2 + (a != z) * 4 + (z == 0) * 8 + (b <= a) * 16 + (a == buf) * 32;
            }
            """;
        Run(Source).Should().Be(1 + 2 + 4 + 8 + 32);
    }

    [Fact]
    public void Global_Constant_Expressions_Sizeof_And_Address_Offsets()
    {
        const string Source = """
            enum { K = 3 };
            int tab[4] = {10, 20, 30, 40};
            uchar bytes[5] = {1, 2, 3, 4, 5};
            int n = sizeof(tab) + K * 2;
            int m = (sizeof bytes << 1) - 1;
            int *second = tab + 1;
            uchar *last = bytes + 4;
            uchar *back = last - 2;
            int *addr = &n;
            int main() {
                return n * 100 + m + *second + *last + *back + *addr;
            }
            """;
        Run(Source).Should().Be((14 * 100) + 9 + 20 + 5 + 3 + 14);
    }

    [Fact]
    public void Global_Runtime_Initializers_Run_Before_Main_In_Order()
    {
        const string Source = """
            int base = 5;
            int calls;
            int next() { calls++; return calls * 10; }
            int a = next();
            int b = a + base * 2;
            uchar c = a + b;
            int *pa = &a;
            int main() {
                return calls * 1000 + a + b + c + *pa;
            }
            """;
        Run(Source).Should().Be((1 * 1000) + 10 + 20 + 30 + 10);
    }

    [Fact]
    public void Global_Runtime_Initializers_Work_Across_Modules()
    {
        string dir = Directory.CreateTempSubdirectory("cathode-init-").FullName;
        try
        {
            string a = Path.Combine(dir, "a.c");
            string b = Path.Combine(dir, "b.c");
            string bin = Path.Combine(dir, "p.bin");
            File.WriteAllText(a, "int seed() { return 21; }\nint twice = seed() * 2;\nint get() { return twice; }\n");
            File.WriteAllText(b, "int get();\nint five() { return 5; }\nint k = five() + 1;\nint main() { return get() + k; }\n");
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

            cpu.State.A.Should().Be(42 + 6);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
