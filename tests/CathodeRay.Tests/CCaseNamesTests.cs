using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 31, krok 1: identyfikatory C różniące się tylko wielkością liter na celach z asemblerem bez rozróżniania wielkości liter.</summary>
public sealed class CCaseNamesTests
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
    public void Names_Differing_Only_In_Case_Stay_Distinct(string cpu)
    {
        const string Source = """
            int total = 1;
            int Total = 20;
            int TOTAL = 300;
            int foo(int x) { return x + 1; }
            int Foo(int x) { return x + 10; }
            int FOO(int x) { return x + 100; }
            int main() {
                int a = 2;
                int A = 30;
                return foo(a) + Foo(A) + FOO(0) + total + Total + TOTAL;
            }
            """;

        CcRun.RunOn(Source, cpu).Value.Should().Be(3 + 40 + 100 + 1 + 20 + 300);
    }

    [Theory]
    [InlineData("z80")]
    [InlineData("8080")]
    [InlineData("6800")]
    public void Case_Variants_Link_Across_Two_Modules(string cpu)
    {
        string dir = Directory.CreateTempSubdirectory("cathode-case-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.c"), "int Helper(int x) { return x + 5; }\nint helper(int x) { return x * 2; }\n");
            File.WriteAllText(Path.Combine(dir, "b.c"), "int Helper(int x);\nint helper(int x);\nint main() { return Helper(1) * 100 + helper(4); }\n");
            string bin = Path.Combine(dir, "p.bin");
            var error = new StringWriter();
            var output = new StringWriter();
            int exit = CathodeRay.Cli.CliApp.CreateRoot().Parse(["cc", Path.Combine(dir, "b.c"), Path.Combine(dir, "a.c"), "-o", bin, "--cpu", cpu]).Invoke(new System.CommandLine.InvocationConfiguration { Output = output, Error = error });

            exit.Should().Be(0, error.ToString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
