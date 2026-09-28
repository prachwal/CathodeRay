using CathodeRay.Assembler;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class ScopeTests
{
    private static byte[] Asm(string source, string cpu = "stub", string? syntax = null) =>
        Repo.Assemble(cpu, source, syntax).Image;

    [Fact]
    public void Same_Label_In_Two_Procs_Resolves_Locally()
    {
        Asm(".proc foo\nloop: NOP\nBNE loop\nRTS\n.endproc\n.proc bar\nloop: NOP\nBNE loop\nRTS\n.endproc\n", "6502")
            .Should().Equal(0xEA, 0xD0, 0xFD, 0x60, 0xEA, 0xD0, 0xFD, 0x60);
    }

    [Fact]
    public void Qualified_Access_From_Outside()
    {
        var result = Repo.Assemble("6502", ".org $0600\n.proc foo\nloop: NOP\n.endproc\nLDA foo::loop\n");

        result.Symbols["foo::loop"].Should().Be(0x600);
        result.Image.Should().Equal(0xEA, 0xAD, 0x00, 0x06);
    }

    [Fact]
    public void Scope_Hides_Without_Label_Proc_Defines_One()
    {
        var result = Repo.Assemble("6502", ".scope glob\nshared: NOP\n.endscope\n.proc work\ntask: NOP\n.endproc\n");

        result.Symbols.Should().NotContainKey("glob");
        result.Symbols["work"].Should().Be(1);
        result.Symbols["glob::shared"].Should().Be(0);
        result.Symbols["work::task"].Should().Be(1);
    }

    [Fact]
    public void Nested_Scopes_Qualify_Full_Path()
    {
        var result = Repo.Assemble("6502", ".org $0600\n.scope outer\n.scope inner\nval: NOP\n.endscope\n.endscope\nLDA outer::inner::val\n");

        result.Symbols["outer::inner::val"].Should().Be(0x600);
    }

    [Fact]
    public void Anonymous_Scope_Hides_Symbols()
    {
        FluentActions.Invoking(() => Asm(".scope\nhidden: NOP\n.endscope\nLDA hidden\n", "6502"))
            .Should().Throw<AssemblerException>().WithMessage("*undefined symbol 'hidden'*");
    }

    [Fact]
    public void Cheap_Local_Works_Inside_Scope()
    {
        Asm("aa: NOP\n@x: NOP\n.proc foo\n@x: NOP\nLDA @x\n.endproc\n")
            .Should().Equal(0x00, 0x00, 0x00, 0x06, 0x02, 0x00);
    }

    [Fact]
    public void Cheap_Local_Context_Clears_On_Scope_Exit_Like_Ca65()
    {
        FluentActions.Invoking(() => Asm("aa: NOP\n@x: NOP\n.proc foo\n@x: NOP\n.endproc\nLDA @x\n"))
            .Should().Throw<AssemblerException>().WithMessage("*no preceding global label*");
    }

    [Fact]
    public void Mos_Uses_Dotted_Uppercase_Names()
    {
        Asm(".PROC FOO\nLOOP: NOP\nBNE LOOP\nRTS\n.ENDPROC\nLDA FOO::LOOP\n", "6502", "mos")
            .Should().Equal(0xEA, 0xD0, 0xFD, 0x60, 0xA5, 0x00);
    }

    [Theory]
    [InlineData(".endscope\n", "'.endscope' without")]
    [InlineData(".endproc\n", "'.endproc' without")]
    [InlineData(".scope foo\n", "unterminated '.scope'")]
    [InlineData(".proc foo\n", "unterminated '.proc'")]
    [InlineData(".scope foo\nbar: NOP\n.endscope\n.scope foo\nbar: NOP\n.endscope\n", "duplicate symbol 'foo::bar'")]
    [InlineData(".proc foo\n.endproc\n.proc foo\n.endproc\n", "duplicate symbol 'foo'")]
    [InlineData(".proc foo\n.endscope\n", "closes '.proc'")]
    [InlineData(".scope foo\n.endproc\n", "closes '.scope'")]
    [InlineData(".scope 1x\n.endscope\n", "invalid scope name")]
    [InlineData("lbl: .scope foo\n.endscope\n", "label on")]
    [InlineData("LDA foo:bar\n", "unexpected ':'")]
    [InlineData("LDA foo::\n", "identifier expected after")]
    public void Malformed_Scopes_Are_Rejected(string source, string message)
    {
        FluentActions.Invoking(() => Asm(source, "6502"))
            .Should().Throw<AssemblerException>().WithMessage($"*{message}*");
    }

    public static TheoryData<string> GoldenNames()
    {
        var data = new TheoryData<string>();
        foreach (string dir in Directory.GetDirectories(Repo.Path("tests", "CathodeRay.Tests", "Scopes")).Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(dir));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(GoldenNames))]
    public void Golden_Matches_Reference(string cpu)
    {
        string main = Repo.Path("tests", "CathodeRay.Tests", "Scopes", cpu, "main.s");
        AssemblerTarget target = AssemblerTargets.Find(cpu)!;
        var assembler = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax);
        static string? Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

        assembler.Assemble(File.ReadAllText(main), main, Read).Image.Should().Equal(
            File.ReadAllBytes(Repo.Path("tests", "CathodeRay.Tests", "Scopes", cpu, "expected.bin")),
            $"Scopes/{cpu}/main.s ma dać te same bajty co referencja");
    }
}
