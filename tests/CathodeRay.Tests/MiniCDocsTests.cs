using System.Text.RegularExpressions;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>docs/minic.md jest testowany: bloki <c>```c expect=N</c> muszą się skompilować i zwrócić N,
/// bloki <c>```c error="tekst"</c> muszą zakończyć się błędem z tym tekstem.</summary>
public sealed partial class MiniCDocsTests
{
    public static TheoryData<string, string, string> Examples()
    {
        string text = File.ReadAllText(Repo.Path("docs", "minic.md"));
        var data = new TheoryData<string, string, string>();
        int index = 0;
        foreach (Match match in Fence().Matches(text))
        {
            index++;
            string kind = match.Groups["expect"].Success ? "expect" : "error";
            string value = match.Groups["expect"].Success ? match.Groups["expect"].Value : match.Groups["error"].Value;
            data.Add($"{index:D2}-{kind}", value, match.Groups["code"].Value);
        }

        return data;
    }

    [Fact]
    public void Document_Has_Examples()
    {
        Examples().Count.Should().BeGreaterThan(10);
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void Example_Behaves_As_Documented(string name, string expected, string code)
    {
        if (name.EndsWith("expect", StringComparison.Ordinal))
        {
            CcRun.Run(code).Value.Should().Be(int.Parse(expected), $"przykład {name}");
        }
        else
        {
            var (exit, error) = CcRun.Compile(code);
            exit.Should().NotBe(0, $"przykład {name} miał się nie skompilować");
            error.Should().Contain(expected);
        }
    }

    [GeneratedRegex(@"```c (?:expect=(?<expect>\d+)|error=""(?<error>[^""]+)"")\r?\n(?<code>.*?)```", RegexOptions.Singleline)]
    private static partial Regex Fence();
}
