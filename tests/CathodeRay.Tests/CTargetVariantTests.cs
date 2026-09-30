using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 37, zadanie 6: warianty 6502 — <c>nes</c> (2A03) i <c>6510</c> (C64) w rejestrze celów.</summary>
public sealed class CTargetVariantTests
{
    [Theory]
    [InlineData("nes", "6502")]
    [InlineData("6510", "6502")]
    public void Variant_Assembles_As_6502(string cpu, string assembler)
    {
        ICTarget target = CTargets.Find(cpu)!;
        target.AssemblerCpu.Should().Be(assembler);
        (int exit, string stderr) = CcRun.Compile("int main() { return 42; }", "--cpu", cpu);
        exit.Should().Be(0, stderr);
    }

    [Theory]
    [InlineData("nes")]
    [InlineData("6510")]
    public void Variant_Runs_Fib(string cpu)
    {
        const string Source = "int fib(int n) { if (n <= 1) return n; return fib(n - 1) + fib(n - 2); } int main() { return fib(10); }";
        CcRun.RunOn(Source, cpu).Value.Should().Be(55);
        CcRun.RunOn(Source, cpu, "--ir", "vreg").Value.Should().Be(55);
    }

    [Fact]
    public void Nes_With_Cmos_Throws()
    {
        Action create = () => _ = new Mos6502Target(cmos: true, nes: true);
        create.Should().Throw<ArgumentException>();
    }
}
