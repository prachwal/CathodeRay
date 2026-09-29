using CathodeRay.Assembler;
using CathodeRay.C;
using CathodeRay.Stub;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Programy mini-C (samples/minic): kompilacja, uruchomienie, asercje wyniku.
/// Kazda wspierana konstrukcja jezyka wystepuje w co najmniej jednym programie.</summary>
public sealed class MiniCProgramsTests
{
    private static string Dir() => Repo.Path("samples", "minic");

    private static (StubCpu Cpu, StubBus Bus, AssemblyResult Result) Run(string name)
    {
        string path = Path.Combine(Dir(), name);
        string? Read(string p) => File.Exists(p) ? File.ReadAllText(p) : null;
        Func<string, string?> reader = p =>
        {
            string full = Path.Combine(Dir(), p);
            return Read(full);
        };
        CheckedProgram checkedProgram = TypeChecker.Check(Parser.Parse(File.ReadAllText(path), reader));
        string asm = Crt0.Source + Codegen.Emit(checkedProgram);
        AssemblerTarget target = AssemblerTargets.Find("stub")!;
        var origins = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["CODE"] = 0x1000,
            ["INIT"] = 0x3F00,
            ["BSS"] = 0x4000,
            ["DATA"] = 0x5000,
        };
        AssemblyResult result = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax)
            .Assemble(asm, path, _ => null, [], null, origins);
        var bus = new StubBus();
        for (int i = 0; i < result.Image.Length; i++)
        {
            bus.Write((ushort)(result.Origin + i), result.Image[i]);
        }

        StubIsa isa = StubIsa.FromJsonFile(Repo.IsaFile("mcp_stub_instructions.json"));
        var cpu = new StubCpu(isa, bus);
        cpu.State.ProgramCounter = (ushort)origins["CODE"];
        for (int steps = 0; !cpu.State.Halted; steps++)
        {
            steps.Should().BeLessThan(100_000, "program ma się zatrzymać");
            cpu.Step();
        }

        return (cpu, bus, result);
    }

    [Fact]
    public void Types_Covers_Types_Params_Globals_And_Returns()
    {
        var (cpu, _, _) = Run("01_types.c");

        cpu.State.A.Should().Be(165);
    }

    [Fact]
    public void Control_Covers_If_While_For()
    {
        var (cpu, _, _) = Run("02_control.c");

        cpu.State.A.Should().Be(42);
    }

    [Fact]
    public void UcharArith_Covers_Uchar_Arithmetic()
    {
        var (cpu, _, _) = Run("03a_uchar_arith.c");

        cpu.State.A.Should().Be(7);
        cpu.State.X.Should().Be(4);
    }

    [Fact]
    public void UcharCmp_Covers_Uchar_Comparisons_And_Logic()
    {
        var (cpu, _, _) = Run("03b_uchar_cmp.c");

        cpu.State.A.Should().Be(20);
    }

    [Fact]
    public void IntOps_Covers_All_Int_Operators()
    {
        var (cpu, _, _) = Run("04_int_ops.c");

        cpu.State.A.Should().Be(4);
        cpu.State.X.Should().Be(6);
    }

    [Fact]
    public void Calls_Covers_Calls_Recursion_And_Compound_Assignment()
    {
        var (cpu, _, _) = Run("05_calls.c");

        cpu.State.A.Should().Be(144);
        cpu.State.X.Should().Be(1);
    }

    [Fact]
    public void Defines_Covers_Include_And_Define()
    {
        var (cpu, _, _) = Run("06_defines.c");

        cpu.State.A.Should().Be(109);
    }

    [Fact]
    public void PtrBasic_Covers_Address_And_Deref()
    {
        var (cpu, _, _) = Run("ptr_basic.c");

        cpu.State.A.Should().Be(84);
    }

    [Fact]
    public void PtrArray_Covers_Arrays_Index_And_Arithmetic()
    {
        var (cpu, _, _) = Run("ptr_array.c");

        cpu.State.A.Should().Be(80);
    }

    [Fact]
    public void PtrInt_Covers_Int_Pointer_Scale()
    {
        var (cpu, _, _) = Run("ptr_int.c");

        cpu.State.A.Should().Be(100);
    }

    [Theory]
    [InlineData("07_control.c", 62)]
    [InlineData("08_int_signed.c", 10)]
    [InlineData("09_init.c", 136)]
    public void Plan27_Samples_Return_Expected_Value(string file, int expected)
    {
        var (cpu, _, _) = Run(file);

        cpu.State.A.Should().Be((byte)expected);
    }
}
