using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 38, zadanie 2: alokator przydziela tylko rejestry z modelu (test, zero zmian w <c>src</c>).</summary>
public sealed class CpuModelAllocatorTests
{
    private static Ir.Module LowerSample(string file)
    {
        string source = File.ReadAllText(Repo.Path("samples", "minic", file));
        return Codegen.Lower(TypeChecker.Check(Parser.Parse(source, StdLib.HeaderReader)), file);
    }

    public static TheoryData<string> Samples() => new() { "05_calls.c", "20_matrix.c", "10_struct.c" };

    [Theory]
    [MemberData(nameof(Samples))]
    public void Allocator_Assigns_Only_Model_Registers(string file)
    {
        Ir.Module module = LowerSample(file);
        foreach (ByteTarget target in CTargets.All.OfType<ByteTarget>())
        {
            ByteIsa isa = target.CreateIsa();
            Dictionary<string, string> map = RegisterAllocator.Run(module, isa);
            CpuModel model = CpuModels.For(target);
            foreach (string registers in map.Values)
            {
                model.HasRegister(registers).Should().BeTrue($"przydział '{registers}' na {target.Name} ({file})");
            }

            if (isa.CellRegisters.Count == 0)
            {
                map.Should().BeEmpty($"brak rejestrowych komórek na {target.Name}");
            }
        }
    }

    [Fact]
    public void Allocator_Assigns_Something_On_Z80()
    {
        Ir.Module module = LowerSample("05_calls.c");
        ByteIsa isa = ((ByteTarget)CTargets.Find("z80")!).CreateIsa();
        RegisterAllocator.Run(module, isa).Should().NotBeEmpty("test ma być niepusty");
    }

    [Fact]
    public void VregTargetInfo_Matches_Model()
    {
        foreach (ICTarget target in CTargets.All)
        {
            VRegTargetInfo info = VRegTargetInfo.For(target);
            CpuModel model = CpuModels.For(target);
            foreach (string register in info.PhysRegs)
            {
                model.HasRegister(register).Should().BeTrue($"{register} na {target.Name}");
            }

            info.HasPairs.Should().Be(model.Registers.Any(static r => r.Parts.Count == 2), target.Name);
        }
    }

    [Fact]
    public void Pairs_Are_Two_8bit_Aliases()
    {
        foreach (ByteTarget target in CTargets.All.OfType<ByteTarget>())
        {
            ByteIsa isa = target.CreateIsa();
            CpuModel model = CpuModels.For(target);
            foreach (string pair in isa.CellPairs)
            {
                CpuRegister entry = model.Find(pair)!;
                entry.Parts.Should().HaveCount(2, pair);
                foreach (string part in entry.Parts)
                {
                    model.Find(part)!.Width.Should().Be(1, part);
                }
            }
        }
    }
}
