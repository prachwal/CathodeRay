using CathodeRay.Assembler;
using CathodeRay.Assembler.Link;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class ObjectTests
{
    private static ObjectModule AsmObj(string source, string cpu = "stub", string? syntax = null)
    {
        AssemblerTarget target = AssemblerTargets.Find(cpu)!;
        return new TwoPassAssembler(Repo.LoadTarget(target), syntax is null ? target.DefaultSyntax : target.FindSyntax(syntax)!)
            .AssembleObject(cpu, source, "main", _ => null);
    }

    [Fact]
    public void Extern_Becomes_Relocation_With_Addend()
    {
        ObjectModule module = AsmObj("EXTERN getval\nGLOBAL main\nmain: LDA getval+2\nSTA $2000\nHLT\n".Replace("EXTERN", ".extern").Replace("GLOBAL", ".global"));

        module.Cpu.Should().Be("stub");
        module.Relocations.Should().ContainSingle().Which.Should().Be(new Relocation("CODE", 1, RelocKind.Abs16, "getval", 2));
        module.Symbols.Should().Contain(new ObjectSymbol("main", "CODE", 0, true));
    }

    [Fact]
    public void Json_Round_Trips()
    {
        ObjectModule module = AsmObj(".global getval\ngetval: LDI 42\nHLT\n");
        ObjectModule back = ObjectModule.FromJson(module.ToJson());

        back.Should().BeEquivalentTo(module);
        back.ToJson().Should().Be(module.ToJson());
    }

    [Fact]
    public void Undefined_Export_Is_Rejected()
    {
        FluentActions.Invoking(() => AsmObj(".global nope\nHLT\n"))
            .Should().Throw<AssemblerException>().WithMessage("*exported symbol 'nope' is not defined*");
    }

    [Fact]
    public void Local_Definition_Of_External_Is_Rejected()
    {
        FluentActions.Invoking(() => AsmObj(".extern foo\nfoo: NOP\n"))
            .Should().Throw<AssemblerException>().WithMessage("*declared external*");
    }

    [Fact]
    public void Non_Linear_Expression_Is_Rejected()
    {
        FluentActions.Invoking(() => AsmObj(".extern e\nLDA e*2\nHLT\n"))
            .Should().Throw<AssemblerException>().WithMessage("*not relocatable*");
    }
}
