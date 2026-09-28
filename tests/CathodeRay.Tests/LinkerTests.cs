using CathodeRay.Assembler;
using CathodeRay.Assembler.Link;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class LinkerTests
{
    private const string Config = "MEMORY { RAM: start=$0000, size=$10000, file=%O, fill=yes; }\nSEGMENTS { CODE: load=RAM; DATA: load=RAM; BSS: load=RAM, type=bss; }\n";

    private static ObjectModule Obj(string source, string cpu = "stub")
    {
        AssemblerTarget target = AssemblerTargets.Find(cpu)!;
        return new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax).AssembleObject(cpu, source, "main", _ => null);
    }

    private static AssemblyResult Link(params ObjectModule[] modules) =>
        Linker.Link([.. modules.Select((m, i) => ($"m{i}.o", m))], LinkerConfig.Parse(Config));

    [Fact]
    public void Links_Cross_Module_Call_With_Relocation()
    {
        var main = Obj(".extern getval\n.global main\nmain: LDA getval\nSTA $2000\nHLT\n");
        var lib = Obj(".global getval\ngetval: LDI 42\nHLT\n");

        AssemblyResult result = Link(main, lib);

        result.Image.Should().Equal(0x06, 0x07, 0x00, 0x05, 0x00, 0x20, 0xFF, 0x01, 0x2A, 0xFF);
        result.Symbols["main"].Should().Be(0);
        result.Symbols["getval"].Should().Be(7);
    }

    [Fact]
    public void Concatenates_Same_Segment_From_Modules()
    {
        var a = Obj(".global a\na: NOP\n");
        var b = Obj(".global b\nb: NOP\n");

        AssemblyResult result = Link(a, b);

        result.Image.Should().Equal(0x00, 0x00);
        result.Symbols["b"].Should().Be(1);
    }

    [Fact]
    public void Bss_Takes_Addresses_Without_Bytes()
    {
        var a = Obj(".global v\n.segment \"DATA\"\n.byte 9\n.bss\nv: .byte 0\n.segment \"CODE\"\nHLT\n");

        AssemblyResult result = Link(a);

        result.Symbols["v"].Should().Be(2);
        result.Image.Should().Equal(0xFF, 0x09);
    }

    [Fact]
    public void Duplicate_Global_Is_Rejected()
    {
        var a = Obj(".global x\nx: NOP\n");
        var b = Obj(".global x\nx: NOP\n");

        FluentActions.Invoking(() => Link(a, b))
            .Should().Throw<LinkerException>().WithMessage("*Duplicate symbol 'x'*m0.o*m1.o*");
    }

    [Fact]
    public void Missing_Import_Is_Rejected()
    {
        var a = Obj(".extern nope\nLDA nope\nHLT\n");

        FluentActions.Invoking(() => Link(a))
            .Should().Throw<LinkerException>().WithMessage("*Undefined symbol 'nope'*m0.o*");
    }

    [Fact]
    public void Different_Cpu_Is_Rejected()
    {
        var a = Obj(".global x\nx: NOP\n", "stub");
        var b = Obj(".global y\ny: NOP\n", "6502");

        FluentActions.Invoking(() => Linker.Link([("a.o", a), ("b.o", b)], LinkerConfig.Parse(Config)))
            .Should().Throw<LinkerException>().WithMessage("*same CPU*");
    }

    [Fact]
    public void Rel8_Relocation_Applies_Relative_Offset()
    {
        var main = Obj(".extern far\n.global start\nstart: bne far\nrts\n", "6502");
        var lib = Obj(".global far\nfar: nop\n", "6502");

        AssemblyResult result = Link(main, lib);

        result.Image.Should().Equal(0xD0, 0x01, 0x60, 0xEA);
    }

    [Fact]
    public void Rel8_Out_Of_Range_Is_Rejected()
    {
        var main = Obj(".extern far\nbne far\n" + string.Join('\n', Enumerable.Repeat("nop", 200)) + "\nrts\n", "6502");
        var lib = Obj(".global far\nfar: nop\n", "6502");

        FluentActions.Invoking(() => Link(main, lib))
            .Should().Throw<LinkerException>().WithMessage("*out of range*");
    }

    [Fact]
    public void Bad_Config_Is_Rejected()
    {
        FluentActions.Invoking(() => LinkerConfig.Parse("MEMORY { RAM: start=$100; }\n"))
            .Should().Throw<LinkerException>();
        FluentActions.Invoking(() => LinkerConfig.Parse("GARBAGE { }\n"))
            .Should().Throw<LinkerException>();
    }
}
