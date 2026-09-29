using CathodeRay.Assembler;
using CathodeRay.Stub;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Programy testowe instrukcji stub (samples/stub/ops): asemblacja,
/// uruchomienie, asercje komórek i rejestrów. Kazdy mnemonik wystepuje
/// w co najmniej jednym programie (flagi C/Z przez skoki, V tylko unitowo).</summary>
public sealed class StubOpProgramsTests
{
    private static (StubCpu Cpu, StubBus Bus, AssemblyResult Result) Run(string name)
    {
        StubIsa isa = StubIsa.FromJsonFile(Repo.IsaFile("mcp_stub_instructions.json"));
        string path = Repo.Path("samples", "stub", "ops", name);
        AssemblerTarget target = AssemblerTargets.Find("stub")!;
        static string? ReadText(string p) => File.Exists(p) ? File.ReadAllText(p) : null;
        static byte[]? ReadBinary(string p) => File.Exists(p) ? File.ReadAllBytes(p) : null;
        AssemblyResult result = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax)
            .Assemble(File.ReadAllText(path), path, ReadText, [], ReadBinary);
        result.Origin.Should().Be(0, "programy ops ladujemy od adresu 0");
        var bus = new StubBus();
        for (int i = 0; i < result.Image.Length; i++)
        {
            bus.Write((ushort)(result.Origin + i), result.Image[i]);
        }

        var cpu = new StubCpu(isa, bus);
        for (int steps = 0; !cpu.State.Halted; steps++)
        {
            steps.Should().BeLessThan(10_000, "program ma się zatrzymać");
            cpu.Step();
        }

        return (cpu, bus, result);
    }

    private static byte Cell((StubCpu Cpu, StubBus Bus, AssemblyResult Result) run, string symbol) =>
        run.Bus.Read((ushort)run.Result.Symbols[symbol]);

    [Fact]
    public void Alu_Covers_Add_Sub_Adc_And_Carry()
    {
        var run = Run("alu.asm");

        Cell(run, "r_add").Should().Be(127);
        Cell(run, "r_wrap").Should().Be(44);
        Cell(run, "r_carry").Should().Be(1);
        Cell(run, "r_sub").Should().Be(30);
        Cell(run, "r_borrow").Should().Be(246);
        Cell(run, "r_adc0").Should().Be(15);
        Cell(run, "r_adc1").Should().Be(16);
    }

    [Fact]
    public void Logic_Covers_And_Ora_Eor_Not_Plain_And_Indexed()
    {
        var run = Run("logic.asm");

        Cell(run, "r_and").Should().Be(48);
        Cell(run, "r_ora").Should().Be(255);
        Cell(run, "r_eor").Should().Be(240);
        Cell(run, "r_not").Should().Be(195);
        Cell(run, "r_ldx").Should().Be(7);
        Cell(run, "r_andx").Should().Be(5);
        Cell(run, "r_orax").Should().Be(7);
        Cell(run, "r_eorx").Should().Be(2);
    }

    [Fact]
    public void Shifts_Covers_Shl_Shr_And_Carry_Chain()
    {
        var run = Run("shifts.asm");

        Cell(run, "r_shl").Should().Be(2);
        Cell(run, "r_c1").Should().Be(1);
        Cell(run, "r_shl0").Should().Be(10);
        Cell(run, "r_c0").Should().Be(1);
        Cell(run, "r_shr").Should().Be(64);
        Cell(run, "r_shr0").Should().Be(2);
        Cell(run, "r_adc1").Should().Be(1);
        Cell(run, "r_adc0").Should().Be(0);
    }

    [Fact]
    public void Branches_Covers_Cpa_Cpx_And_All_Conditional_Jumps()
    {
        var run = Run("branches.asm");

        Cell(run, "r_eq").Should().Be(1);
        Cell(run, "r_ne").Should().Be(1);
        Cell(run, "r_lt").Should().Be(1);
        Cell(run, "r_ge").Should().Be(1);
        Cell(run, "r_xeq").Should().Be(1);
        Cell(run, "r_xlt").Should().Be(1);
        Cell(run, "r_cnt").Should().Be(5);
    }

    [Fact]
    public void Moves_Covers_Transfers_Increments_And_Jump_Over()
    {
        var run = Run("moves.asm");

        Cell(run, "r0").Should().Be(42);
        Cell(run, "r1").Should().Be(43);
        Cell(run, "r2").Should().Be(42);
        Cell(run, "r_x").Should().Be(42);
        Cell(run, "r3").Should().Be(7);
        run.Cpu.State.X.Should().Be(42);
    }

    [Fact]
    public void MemIdx_Covers_Loads_Stores_And_Indexed_Arithmetic()
    {
        var run = Run("memidx.asm");

        Cell(run, "b").Should().Be(11);
        Cell(run, "r_idx").Should().Be(50);
        Cell(run, "r_addx").Should().Be(55);
        Cell(run, "r_adcx").Should().Be(55);
        Cell(run, "r_adcx2").Should().Be(56);
        Cell(run, "buf4").Should().Be(77);
    }

    [Fact]
    public void Stack_Covers_Ldsp_Push_Pop_And_Nested_Calls()
    {
        var run = Run("stack.asm");

        Cell(run, "r1").Should().Be(2);
        Cell(run, "r0").Should().Be(1);
        Cell(run, "r_call").Should().Be(11);
        run.Cpu.State.A.Should().Be(11);
        run.Cpu.State.StackPointer.Should().Be(0xFF);
    }
}
