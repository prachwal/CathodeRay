using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 31, krok 14: <c>volatile</c>, <c>inline</c>, <c>register</c>.</summary>
public sealed class CQualifiersTests
{
    private static Ir.Module Lower(string source) => Codegen.Lower(TypeChecker.Check(Parser.Parse(source)));

    [Fact]
    public void Qualifiers_Parse_In_Every_Position()
    {
        const string Source = """
            volatile uchar flag;
            const volatile int ro = 5;
            uchar volatile other;
            static inline int twice(int x) { return x + x; }
            inline int thrice(int x) { return x * 3; }
            volatile uchar * volatile port;
            typedef volatile uchar reg8;
            reg8 status;
            int main() {
                register int i;
                register uchar n = 3;
                int total = 0;
                for (i = 0; i < n; i++) total = total + twice(i) + thrice(i);
                flag = 1;
                other = flag;
                return total + ro + other + (port == 0) + status;
            }
            """;

        CcRun.Run(Source, "-Werror").Value.Should().Be(0 + 2 + 4 + 0 + 3 + 6 + 5 + 1 + 1 + 0);
    }

    [Fact]
    public void Volatile_Local_Keeps_Every_Read_While_Plain_Local_Is_Folded()
    {
        Ir.Module plain = Lower("int main() { uchar v = 1; uchar a = v; uchar b = v; return a + b; }");
        Ir.Module vol = Lower("int main() { volatile uchar v = 1; uchar a = v; uchar b = v; return a + b; }");

        int PlainReads(Ir.Module m) => m.Functions.Single().Body.OfType<Ir.Mov>().Count(static mov => mov.Src is Ir.Cell { Sym: "main__v" });

        PlainReads(plain).Should().Be(0);
        PlainReads(vol).Should().Be(2);
        vol.Volatile.Should().Contain("main__v");
    }

    [Fact]
    public void Volatile_Read_Through_A_Pointer_Is_Not_Removed_When_Unused()
    {
        Ir.Module vol = Lower("void poke(volatile uchar *p) { *p; }\nint main() { return 0; }");
        Ir.Module plain = Lower("void poke(uchar *p) { *p; }\nint main() { return 0; }");

        vol.Functions.Single(static f => f.Name == "poke").Body.OfType<Ir.Load>().Should().ContainSingle(static l => l.Volatile);
        plain.Functions.Single(static f => f.Name == "poke").Body.OfType<Ir.Load>().Should().BeEmpty();
    }

    [Theory]
    [InlineData("6502", "lda cc_g_reg")]
    [InlineData("z80", "ld a,(cc_g_reg)")]
    public void Volatile_Global_Is_Reloaded_After_A_Store(string cpu, string load)
    {
        const string Source = "volatile uchar reg;\nuchar plain;\nint main() { reg = 5; uchar a = reg; plain = 6; uchar b = plain; return a + b; }";
        string dir = Directory.CreateTempSubdirectory("cathode-vol-").FullName;
        try
        {
            string src = Path.Combine(dir, "a.c");
            File.WriteAllText(src, Source);
            string lst = Path.Combine(dir, "a.lst");
            var error = new StringWriter();
            int exit = CathodeRay.Cli.CliApp.CreateRoot().Parse(["cc", src, "-o", Path.Combine(dir, "a.bin"), "-l", lst, "--cpu", cpu]).Invoke(new System.CommandLine.InvocationConfiguration { Output = new StringWriter(), Error = error });
            exit.Should().Be(0, error.ToString());
            string listing = File.ReadAllText(lst);

            listing.Split(load, StringSplitOptions.None).Length.Should().BeGreaterThan(1, "odczyt volatile po zapisie zostaje");
            listing.Should().NotContain(cpu == "6502" ? "lda cc_g_plain" : "ld a,(cc_g_plain)");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Stub_Peephole_Keeps_Volatile_Load_After_Store()
    {
        const string Source = "volatile uchar reg;\nint main() { reg = 5; return reg; }";
        string dir = Directory.CreateTempSubdirectory("cathode-vol-stub-").FullName;
        try
        {
            string src = Path.Combine(dir, "a.c");
            File.WriteAllText(src, Source);
            string lst = Path.Combine(dir, "a.lst");
            int exit = CathodeRay.Cli.CliApp.CreateRoot().Parse(["cc", src, "-o", Path.Combine(dir, "a.bin"), "-l", lst]).Invoke(new System.CommandLine.InvocationConfiguration { Output = new StringWriter(), Error = new StringWriter() });
            exit.Should().Be(0);

            File.ReadAllText(lst).Should().Contain("LDA cc_g_reg");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
