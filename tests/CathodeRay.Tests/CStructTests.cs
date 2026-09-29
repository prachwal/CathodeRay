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
}
