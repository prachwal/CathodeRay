using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class M6809TargetTests
{
    [Fact]
    public void Compiles_And_Runs_Minimal_C_Program()
    {
        CcRun.RunOn("int main() { return 7; }", "6809").Value.Should().Be(7);
    }

    [Fact]
    public void Compiles_And_Runs_Byte_And_Word_Arithmetic()
    {
        const string source = "int main() { int x = 3; int y = 4; return x + y * 2; }";

        CcRun.RunOn(source, "6809").Value.Should().Be(11);
    }
}
