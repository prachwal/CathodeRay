using CathodeRay.C;
using CathodeRay.Cli;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 37, zadanie 1: flaga <c>--ir</c> sterownika <c>cc</c> (faza 0 z <c>docs/vreg-design.md</c>).</summary>
public sealed class VRegCliTests
{
    private static (int Exit, string Output, string Stderr) Invoke(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exit = CliApp.CreateRoot().Parse(args).Invoke(new System.CommandLine.InvocationConfiguration { Output = output, Error = error });
        return (exit, output.ToString(), error.ToString());
    }

    [Fact]
    public void Ir_List_Prints_Kinds()
    {
        (int exit, string output, _) = Invoke("cc", "--ir", "list");
        exit.Should().Be(0);
        output.Should().Contain("cell").And.Contain("vreg");
    }

    [Fact]
    public void Ir_Vreg_Reports_Not_Implemented()
    {
        (int exit, string stderr) = CcRun.Compile("int main() { return 42; }", "--ir", "vreg");
        exit.Should().NotBe(0);
        stderr.Should().Contain("not yet implemented");
    }

    [Fact]
    public void Ir_Bogus_Reports_Unknown()
    {
        (int exit, string stderr) = CcRun.Compile("int main() { return 42; }", "--ir", "bogus");
        exit.Should().NotBe(0);
        stderr.Should().Contain("unknown IR");
    }

    [Fact]
    public void Ir_Default_Cell_Still_Compiles_Return_42()
    {
        (int exit, string stderr) = CcRun.Compile("int main() { return 42; }");
        exit.Should().Be(0, stderr);
    }

    [Fact]
    public void VReg_Model_Holds_Blocks()
    {
        var block = new VReg.Block("__entry", false, [new VReg.Ret(new VReg.Imm(42, 2), 2)]);
        var function = new VReg.Function("main", false, [], new Dictionary<int, string>(), [], [], 2, [block]);
        function.Blocks.Should().HaveCount(1);
        function.Blocks[0].Code.Should().HaveCount(1);
    }
}
