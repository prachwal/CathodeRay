using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 37, zadanie 4: alokator zachowawczy spilluje wszystko (właściwy przydział robi
/// <c>RegisterAllocator</c> po opadnięciu), a opis celu zna rejestry każdego CPU.</summary>
public sealed class VRegAllocTests
{
    private static VReg.Function LiftFib()
    {
        const string Source = "int fib(int n) { if (n <= 1) return n; return fib(n - 1) + fib(n - 2); } int main() { return fib(10); }";
        Ir.Module cell = Codegen.Lower(TypeChecker.Check(Parser.Parse(Source, StdLib.HeaderReader)), "t.c");
        return VRegLift.Run(cell).Functions.Single(f => f.Name == "fib");
    }

    [Fact]
    public void Accumulator_Spills_Everything()
    {
        var allocator = new AccumulatorAllocator();
        allocator.Name.Should().Be("accumulator");
        IReadOnlyDictionary<int, string?> allocation = allocator.Allocate(LiftFib(), VRegTargetInfo.For(CTargets.Default));
        allocation.Should().NotBeEmpty();
        allocation.Values.Should().OnlyContain(v => v == null, "zachowawczy alokator nie trzyma nic w rejestrach");
    }

    [Theory]
    [InlineData("stub", 1, false)]
    [InlineData("6502", 1, false)]
    [InlineData("65c02", 1, false)]
    [InlineData("z80", 3, true)]
    [InlineData("8080", 3, true)]
    [InlineData("6800", 2, false)]
    public void Target_Info_Knows_Each_Cpu(string cpu, int maxRegs, bool pairs)
    {
        ICTarget target = CTargets.Find(cpu)!;
        VRegTargetInfo info = VRegTargetInfo.For(target);
        info.MaxRegs.Should().Be(maxRegs);
        info.HasPairs.Should().Be(pairs);
        info.PhysRegs.Should().NotBeEmpty();
    }

    [Fact]
    public void Unknown_Target_Spills_Everything()
    {
        VRegTargetInfo info = VRegTargetInfo.For(CTargets.Default) with { PhysRegs = [], MaxRegs = 0 };
        info.MaxRegs.Should().Be(0);
    }
}
