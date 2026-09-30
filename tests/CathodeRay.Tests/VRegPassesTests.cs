using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 37, zadanie 3: przebiegi VReg (CSE, zwijanie kopii, martwe rejestry) są dźwięczne
/// i nie ruszają efektów (wołania, zapisy, volatile).</summary>
public sealed class VRegPassesTests
{
    private static VReg.Function Func(params VReg.Block[] blocks) =>
        new("f", false, [], new Dictionary<int, string>(), [], [], 2, blocks);

    private static List<VReg.Ins> Run(params VReg.Ins[] code) =>
        VRegPasses.Run(Func(new VReg.Block("b", true, code)), null).Blocks[0].Code.ToList();

    [Fact]
    public void Cse_Merges_Duplicate_Bin()
    {
        var r2 = new VReg.Reg(2, 2);
        var r1 = new VReg.Reg(1, 2);
        var r3 = new VReg.Reg(3, 2);
        List<VReg.Ins> code = Run(
            new VReg.Bin(Ir.BinOp.Add, r1, r2, new VReg.Imm(3, 2)),
            new VReg.Bin(Ir.BinOp.Add, r3, r2, new VReg.Imm(3, 2)),
            new VReg.Ret(r3, 2));
        code.OfType<VReg.Bin>().Should().HaveCount(1, "drugie działanie to kopia pierwszego");
        code.OfType<VReg.Mov>().Should().BeEmpty("kopie zwija przebieg wzdłuż");
        code.Last().Should().BeOfType<VReg.Ret>();
    }

    [Fact]
    public void Dce_Drops_Dead_Mov()
    {
        List<VReg.Ins> code = Run(
            new VReg.Mov(new VReg.Reg(1, 1), new VReg.Imm(9, 1)),
            new VReg.Ret(new VReg.Imm(0, 2), 2));
        code.OfType<VReg.Mov>().Should().BeEmpty();
    }

    [Fact]
    public void Coalesce_Removes_Copy_Chain()
    {
        var r1 = new VReg.Reg(1, 2);
        var r2 = new VReg.Reg(2, 2);
        var r3 = new VReg.Reg(3, 2);
        List<VReg.Ins> code = Run(
            new VReg.Bin(Ir.BinOp.Add, r1, new VReg.Reg(0, 2), new VReg.Imm(1, 2)),
            new VReg.Mov(r2, r1),
            new VReg.Mov(r3, r2),
            new VReg.Ret(r3, 2));
        code.OfType<VReg.Mov>().Should().BeEmpty();
        code.OfType<VReg.Ret>().Single().Value.Should().Be(r1);
    }

    [Fact]
    public void Volatile_Load_Survives()
    {
        var load = new VReg.Load(new VReg.Reg(1, 1), new VReg.Pinned("io", 1), 0, 1, true);
        List<VReg.Ins> code = Run(load, new VReg.Ret(new VReg.Imm(0, 2), 2));
        code.Should().Contain(load, "odczyt volatile to efekt");
    }

    [Fact]
    public void Store_Survives()
    {
        var store = new VReg.Store(new VReg.Addr("g", 0), 0, new VReg.Imm(5, 1), 1);
        List<VReg.Ins> code = Run(store, new VReg.Ret(new VReg.Imm(0, 2), 2));
        code.Should().Contain(store, "zapis to efekt");
    }

    [Fact]
    public void Call_With_Dead_Result_Survives()
    {
        var call = new VReg.Call("f", null, [], [], new VReg.Reg(1, 2));
        List<VReg.Ins> code = Run(call, new VReg.Ret(new VReg.Imm(0, 2), 2));
        code.Should().Contain(call, "wołanie to efekt mimo martwego wyniku");
    }

    [Fact]
    public void Passes_Preserve_Semantics_On_Lifted_Fib()
    {
        const string Source = "int fib(int n) { if (n <= 1) return n; return fib(n - 1) + fib(n - 2); } int main() { return fib(10); }";
        Ir.Module cell = Codegen.Lower(TypeChecker.Check(Parser.Parse(Source, StdLib.HeaderReader)), "t.c");
        VReg.Module vreg = VRegLift.Run(cell);
        var optimized = vreg with
        {
            Functions = [.. vreg.Functions.Select(f => VRegPasses.Run(f, vreg.Volatile))],
        };
        var before = VRegInterpreter.Load([vreg]);
        (int bv, _) = before.RunMain();
        var after = VRegInterpreter.Load([optimized]);
        (int av, _) = after.RunMain();
        av.Should().Be(bv).And.Be(55);
    }
}
