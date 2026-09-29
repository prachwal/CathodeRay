using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 31, kroki 2-3: komórki i wskaźniki na stronie zerowej 6502.</summary>
public sealed class CZeroPageTests
{
    public static TheoryData<string> Targets()
    {
        var data = new TheoryData<string>();
        foreach (string name in TargetHarness.Targets.Select(static t => t.Name))
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void Pointer_Loaded_Into_Its_Own_Cell_Keeps_Working(string cpu)
    {
        const string Source = """
            struct Node { int value; struct Node *next; };
            struct Node c = {30, 0};
            struct Node b = {20, &c};
            struct Node a = {10, &b};
            int sum(struct Node *n) {
                int total = 0;
                while (n) {
                    total = total + n->value;
                    n = n->next;
                }
                return total;
            }
            int main() { return sum(&a); }
            """;

        CcRun.RunOn(Source, cpu).Value.Should().Be(60);
    }

    [Fact]
    public void Hot_Cells_Use_Zero_Page_Operands_On_6502()
    {
        const string Source = "uint sum(uchar *p, uchar n) { uint s; s = 0; while (n) { s = s + *p; p++; n--; } return s; }\nint main() { return 0; }";
        string dir = Directory.CreateTempSubdirectory("cathode-zp-").FullName;
        try
        {
            string src = Path.Combine(dir, "a.c");
            File.WriteAllText(src, Source);
            string lst = Path.Combine(dir, "a.lst");
            var error = new StringWriter();
            int exit = CathodeRay.Cli.CliApp.CreateRoot().Parse(["cc", src, "-o", Path.Combine(dir, "a.bin"), "-l", lst, "--cpu", "6502"]).Invoke(new System.CommandLine.InvocationConfiguration { Output = new StringWriter(), Error = error });
            exit.Should().Be(0, error.ToString());
            string listing = File.ReadAllText(lst);

            listing.Should().Contain("lda (sum__p),y", "wskaźnik leży na stronie zerowej, więc bez kopii do __p");
            listing.Should().Contain("z:sum__s");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("6502")]
    [InlineData("65c02")]
    public void Many_Functions_Do_Not_Overflow_The_Zero_Page_Budget(string cpu)
    {
        var source = new System.Text.StringBuilder();
        for (int i = 0; i < 40; i++)
        {
            source.AppendLine($"int f{i}(int a, int b) {{ int c; c = a + b + {i}; while (c > 100) c = c - 7; return c; }}");
        }

        source.AppendLine("int main() { return f0(1, 2) + f39(300, 4); }");

        CcRun.RunOn(source.ToString(), cpu).Value.Should().BeGreaterThan(0);
    }
}
