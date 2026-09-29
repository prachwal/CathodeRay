using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 29 C: static, extern, const.</summary>
public sealed class CQualifierTests
{
    private static int Run(string source)
    {
        var (cpu, _, _) = CCodegenTests.RunC(source);
        return (cpu.State.X * 256) + cpu.State.A;
    }

    [Fact]
    public void Static_Local_Keeps_Value_Between_Calls_And_Initializes_Once()
    {
        const string Source = """
            int next() {
                static int n = 10;
                static uchar calls;
                static uchar tab[3] = {1, 2, 3};
                static uchar *msg = "hey";
                calls++;
                n = n + tab[calls % 3];
                return n * 10 + calls + msg[calls % 3];
            }
            int main() { next(); next(); return next(); }
            """;
        Run(Source).Should().Be(160 + 3 + 'h');
    }

    [Fact]
    public void Static_Local_Survives_Recursion_Unsaved()
    {
        const string Source = """
            int depth(int n) {
                static int deepest;
                if (n > deepest) deepest = n;
                if (n > 0) depth(n - 1);
                return deepest;
            }
            int main() { return depth(4); }
            """;
        Run(Source).Should().Be(4);
    }

    [Fact]
    public void Static_Local_Rejects_NonConstant_Initializer()
    {
        FluentActions.Invoking(() => Run("int f() { return 1; } int g() { static int x = f(); return x; } int main() { return g(); }"))
            .Should().Throw<CCodegenException>().WithMessage("*constant initializer*");
    }

    [Fact]
    public void Static_Global_And_Function_Are_Module_Local()
    {
        string dir = Directory.CreateTempSubdirectory("cathode-static-").FullName;
        try
        {
            string a = Path.Combine(dir, "a.c");
            string b = Path.Combine(dir, "b.c");
            File.WriteAllText(a, "static int count = 5;\nstatic int helper() { return count + 1; }\nint fa() { return helper(); }\n");
            File.WriteAllText(b, "static int count = 100;\nstatic int helper() { return count + 2; }\nint fa();\nint main() { return fa() + helper(); }\n");
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

            cpu.State.A.Should().Be(6 + 102);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Extern_Variables_Link_Across_Modules_Including_Wide_And_Arrays()
    {
        string dir = Directory.CreateTempSubdirectory("cathode-extern-").FullName;
        try
        {
            string a = Path.Combine(dir, "a.c");
            string b = Path.Combine(dir, "b.c");
            File.WriteAllText(a, "int total = 1000;\nuchar tab[4] = {1, 2, 3, 4};\nuchar *label = \"ok\";\nint bump() { total += 5; return total; }\n");
            File.WriteAllText(b, "extern int total;\nextern uchar tab[4];\nextern uchar *label;\nint bump();\nint main() { total += 20; tab[2] = 30; bump(); return total + tab[2] + label[1]; }\n");
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

            ((cpu.State.X * 256) + cpu.State.A).Should().Be(1025 + 30 + 'k');
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Extern_Then_Definition_In_Same_File_And_Errors()
    {
        Run("extern int v;\nint v = 7;\nint main() { return v; }").Should().Be(7);
        FluentActions.Invoking(() => Run("extern int v = 3;\nint main() { return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*cannot be initialized*");
        FluentActions.Invoking(() => Run("int v;\nint v;\nint main() { return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*redefinition*");
        FluentActions.Invoking(() => Run("int v;\nextern uchar v;\nint main() { return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*redefinition*");
        FluentActions.Invoking(() => Run("int main() { extern int x; return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*file scope*");
    }

    [Fact]
    public void Const_Variables_Arrays_And_Pointers_Are_Read_Only()
    {
        Run("const int k = 5;\nconst uchar tab[3] = {1, 2, 3};\nint main() { const uchar *p = tab; return k + p[2] + tab[1]; }").Should().Be(5 + 3 + 2);
        FluentActions.Invoking(() => Run("const int k = 5;\nint main() { k = 6; return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*assignment to const variable 'k'*");
        FluentActions.Invoking(() => Run("const int k = 5;\nint main() { k++; return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*const*");
        FluentActions.Invoking(() => Run("const uchar tab[2] = {1, 2};\nint main() { tab[0] = 9; return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*const*");
        FluentActions.Invoking(() => Run("int main() { uchar x = 1; const uchar *p = &x; *p = 2; return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*const*");
        FluentActions.Invoking(() => Run("const uchar tab[2] = {1, 2};\nint main() { uchar *p = tab; return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*discards const*");
    }

    [Fact]
    public void Const_Parameters_Accept_Both_And_Reject_Writes()
    {
        const string Source = """
            int sum(const uchar *s, uchar n) {
                int t = 0;
                for (uchar i = 0; i < n; i++) t += s[i];
                return t;
            }
            uchar buf[3] = {4, 5, 6};
            const uchar ro[2] = {10, 20};
            int main() { return sum(buf, 3) + sum(ro, 2) + sum("ab", 2); }
            """;
        Run(Source).Should().Be(15 + 30 + 97 + 98);
        FluentActions.Invoking(() => Run("void wipe(const uchar *s) { s[0] = 0; } int main() { return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*const*");
        FluentActions.Invoking(() => Run("void take(uchar *s) { } const uchar ro[1] = {1}; int main() { take(ro); return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*discards const*");
    }

    [Fact]
    public void Const_Struct_And_Pointer_To_Const_Struct_Fields_Are_Read_Only()
    {
        const string Ok = """
            struct S { uchar a; int b; };
            const struct S g = {3, 400};
            int main() { const struct S *p = &g; return p->a + p->b + g.a; }
            """;
        Run(Ok).Should().Be(3 + 400 + 3);
        FluentActions.Invoking(() => Run("struct S { uchar a; };\nconst struct S g = {1};\nint main() { g.a = 2; return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*const*");
        FluentActions.Invoking(() => Run("struct S { uchar a; };\nint f(const struct S *p) { p->a = 2; return 0; }\nint main() { return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*const*");
    }

    [Fact]
    public void Pointer_Const_And_Postfix_Const_Are_Parsed()
    {
        Run("uchar x = 4;\nuchar * const p = &x;\nuchar const c = 6;\nint main() { return *p + c; }").Should().Be(10);
    }
}
