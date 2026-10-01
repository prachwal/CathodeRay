using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 38, zadanie 1: model CPU zgadza się z alokatorem i ISA (rejestry, pary jako aliasy,
/// wynik w rejestrze, warianty 6502 identyczne).</summary>
public sealed class CpuModelConsistencyTests
{
    [Fact]
    public void Every_Target_Has_A_Model()
    {
        foreach (ICTarget target in CTargets.All)
        {
            CpuModels.For(target).Cpu.Should().Be(target.Name);
        }
    }

    [Fact]
    public void Cell_Registers_And_Pairs_Exist_In_Model()
    {
        foreach (ByteTarget target in CTargets.All.OfType<ByteTarget>())
        {
            ByteIsa isa = target.CreateIsa();
            CpuModel model = CpuModels.For(target);
            foreach (string register in isa.CellRegisters)
            {
                model.HasRegister(register).Should().BeTrue($"{register} na {target.Name}");
                model.Find(register)!.Width.Should().Be(1);
            }

            foreach (string pair in isa.CellPairs)
            {
                CpuRegister? entry = model.Find(pair);
                entry.Should().NotBeNull($"para {pair} na {target.Name}");
                entry!.Width.Should().Be(2);
                (entry.Parts[0] + entry.Parts[1]).Should().Be(pair, "starszy rejestr pierwszy jak w CellPairs");
            }
        }
    }

    [Fact]
    public void Allocatable_Registers_Are_Clobbered_By_Calls()
    {
        foreach (ByteTarget target in CTargets.All.OfType<ByteTarget>())
        {
            ByteIsa isa = target.CreateIsa();
            CpuModel model = CpuModels.For(target);
            foreach (string register in isa.CellRegisters)
            {
                model.ClobberedByCall.Should().Contain(register, $"przydział {register} wymaga zapisu wokół wołania ({target.Name})");
            }
        }
    }

    [Fact]
    public void Result_Reg_Matches_Isa()
    {
        foreach (ByteTarget target in CTargets.All.OfType<ByteTarget>())
        {
            ByteIsa isa = target.CreateIsa();
            CpuModel model = CpuModels.For(target);
            (model.ResultReg is not null).Should().Be(isa.ReturnsInResultReg, target.Name);
            if (model.ResultReg is { } result)
            {
                model.Find(result)!.Width.Should().Be(2);
            }
        }
    }

    [Theory]
    [InlineData("65c02")]
    [InlineData("nes")]
    [InlineData("6510")]
    public void Mos6502_Variants_Share_Registers(string cpu)
    {
        CpuModels.For(cpu).Registers.Select(static r => r.Name).Should()
            .BeEquivalentTo(CpuModels.For("6502").Registers.Select(static r => r.Name));
    }

    [Fact]
    public void Aliases_Are_Symmetric()
    {
        CpuModel z80 = CpuModels.For("z80");
        z80.AliasesOf("h").Should().Contain("hl");
        z80.AliasesOf("hl").Should().BeEquivalentTo("hl", "h", "l");
        z80.AliasesOf("a").Should().BeEquivalentTo("a");
    }

    [Fact]
    public void Scratch_Is_Scratch_And_Never_Cell_Assigned()
    {
        foreach (ICTarget target in CTargets.All)
        {
            CpuModel model = CpuModels.For(target);
            model.Scratch.Should().BeSubsetOf(model.ClobberedByCall, target.Name);
            foreach (string register in model.Scratch)
            {
                model.HasRegister(register).Should().BeTrue($"{register} na {target.Name}");
            }
        }

        foreach (ByteTarget target in CTargets.All.OfType<ByteTarget>())
        {
            ByteIsa isa = target.CreateIsa();
            CpuModels.For(target).Scratch.Should().NotIntersectWith(isa.CellRegisters, $"prymitywy zachowują komórki ({target.Name})");
        }
    }

    [Fact]
    public void SavedAround_Pairs_Exist_In_Model()
    {
        string source = File.ReadAllText(Repo.Path("samples", "minic", "05_calls.c"));
        Ir.Module module = Codegen.Lower(TypeChecker.Check(Parser.Parse(source, StdLib.HeaderReader)), "05_calls.c");
        foreach (ByteTarget target in CTargets.All.OfType<ByteTarget>())
        {
            ByteIsa isa = target.CreateIsa();
            Ir.Module tuned = RegisterAllocator.Tune(module, isa);
            CpuModel model = CpuModels.For(target);
            foreach (Ir.Call call in tuned.Functions.SelectMany(static f => f.Body).OfType<Ir.Call>())
            {
                foreach (string pair in isa.SavedAround(call))
                {
                    CpuRegister? entry = model.Find(pair);
                    entry.Should().NotBeNull($"para {pair} na {target.Name}");
                    entry!.Parts.Should().HaveCount(2, pair);
                }
            }
        }
    }

    /// <summary>Plan 38, zadanie 5: puste <c>ArgRegs</c> to dzisiejsze ABI (komórki <c>cc_argN</c>);
    /// Opt 1 (argumenty w rejestrach) wypełni to pole, a <c>ByteIsa.ArgCell</c> już je czyta.</summary>
    [Fact]
    public void Empty_ArgRegs_Means_Memory_ABI()
    {
        foreach (ByteTarget target in CTargets.All.OfType<ByteTarget>())
        {
            ByteIsa isa = target.CreateIsa();
            CpuModels.For(target).ArgRegs.Should().BeEmpty(target.Name);
            for (int i = 0; i < 6; i++)
            {
                isa.ArgCell(i, 0).Should().Be($"cc_arg{i + 1}", target.Name);
                isa.ArgCell(i, 1).Should().Be($"cc_arg{i + 1}_h", target.Name);
            }
        }
    }
}
