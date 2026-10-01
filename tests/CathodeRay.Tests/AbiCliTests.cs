using CathodeRay.C;
using CathodeRay.Cli;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 39, zadanie 1: flaga <c>--abi</c> sterownika <c>cc</c> (faza 0: v2 jeszcze nigdzie nie działa).</summary>
public sealed class AbiCliTests
{
    private static (int Exit, string Output, string Stderr) Invoke(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exit = CliApp.CreateRoot().Parse(args).Invoke(new System.CommandLine.InvocationConfiguration { Output = output, Error = error });
        return (exit, output.ToString(), error.ToString());
    }

    [Fact]
    public void Abi_List_Prints_Kinds()
    {
        (int exit, string output, _) = Invoke("cc", "--abi", "list");
        exit.Should().Be(0);
        output.Should().Contain("v1").And.Contain("v2");
    }

    [Theory]
    [InlineData("stub")]
    [InlineData("6502")]
    [InlineData("z80")]
    [InlineData("6800")]
    public void Abi_V2_Not_Implemented_Yet(string cpu)
    {
        (int exit, string stderr) = CcRun.Compile("int main() { return 42; }", "--cpu", cpu, "--abi", "v2");
        exit.Should().NotBe(0);
        stderr.Should().Contain("not implemented");
    }

    [Fact]
    public void Abi_Bogus_Reports_Unknown()
    {
        (int exit, string stderr) = CcRun.Compile("int main() { return 42; }", "--abi", "bogus");
        exit.Should().NotBe(0);
        stderr.Should().Contain("unknown ABI");
    }

    [Fact]
    public void Abi_Default_V1_Compiles()
    {
        (int exit, string stderr) = CcRun.Compile("int main() { return 42; }");
        exit.Should().Be(0, stderr);
    }

    [Fact]
    public void Only_Nes_Supports_V2()
    {
        CTargets.All.Where(static t => t.SupportsAbiV2).Select(static t => t.Name).Should().BeEquivalentTo("nes");
    }
}
