using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 37, zadanie 7: liniowy alokator jest zachłannie poprawny (rozłączne przedziały dzielą slot,
/// nakładające się spillują, determinizm) i doradczy.</summary>
public sealed class VRegLinearScanTests
{
    private static VReg.Function Func(params VReg.Ins[] code) =>
        new("f", false, [], new Dictionary<int, string>(), [], [], 2, [new VReg.Block("b", true, code)]);

    [Fact]
    public void Overlapping_Regs_Spill_On_Stub()
    {
        var r1 = new VReg.Reg(1, 2);
        var r2 = new VReg.Reg(2, 2);
        var r3 = new VReg.Reg(3, 2);
        var function = Func(
            new VReg.Mov(r1, new VReg.Imm(5, 2)),
            new VReg.Mov(r2, new VReg.Imm(6, 2)),
            new VReg.Bin(Ir.BinOp.Add, r3, r1, r2),
            new VReg.Ret(r3, 2));
        IReadOnlyDictionary<int, string?> allocation = new LinearScanAllocator().Allocate(function, VRegTargetInfo.For(CTargets.Default));
        allocation.Values.Count(static v => v is null).Should().Be(2, "stub ma 1 slot na 3 nakładające się rejestry");
        allocation.Values.Count(static v => v is not null).Should().Be(1);
    }

    [Fact]
    public void Disjoint_Regs_Share_Slot()
    {
        var r1 = new VReg.Reg(1, 2);
        var r2 = new VReg.Reg(2, 2);
        var function = Func(
            new VReg.Mov(r1, new VReg.Imm(5, 2)),
            new VReg.Ret(r1, 2),
            new VReg.Mov(r2, new VReg.Imm(7, 2)),
            new VReg.Ret(r2, 2));
        IReadOnlyDictionary<int, string?> allocation = new LinearScanAllocator().Allocate(function, VRegTargetInfo.For(CTargets.Default));
        allocation.Values.Should().OnlyContain(v => v != null);
        allocation[r1.Id].Should().Be(allocation[r2.Id], "rozłączne przedziały dzielą slot");
    }

    [Fact]
    public void Wide_Spills_Without_Pairs()
    {
        var r1 = new VReg.Reg(1, 4);
        var function = Func(new VReg.Mov(r1, new VReg.Imm(5, 4)), new VReg.Ret(r1, 4));
        IReadOnlyDictionary<int, string?> allocation = new LinearScanAllocator().Allocate(function, VRegTargetInfo.For(CTargets.Default));
        allocation[r1.Id].Should().BeNull("stub nie trzyma 4 B w rejestrze");
    }

    [Fact]
    public void Allocation_Is_Deterministic()
    {
        const string Source = "int fib(int n) { if (n <= 1) return n; return fib(n - 1) + fib(n - 2); }";
        Ir.Module cell = Codegen.Lower(TypeChecker.Check(Parser.Parse(Source, StdLib.HeaderReader)), "t.c");
        VReg.Function fib = VRegLift.Run(cell).Functions.Single(f => f.Name == "fib");
        var allocator = new LinearScanAllocator();
        VRegTargetInfo info = VRegTargetInfo.For(CTargets.Find("z80")!);
        allocator.Allocate(fib, info).Should().BeEquivalentTo(allocator.Allocate(fib, info));
    }
}
