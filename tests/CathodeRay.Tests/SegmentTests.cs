using CathodeRay.Assembler;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class SegmentTests
{
    private static Dictionary<string, int> Map(params string[] entries) =>
        entries.ToDictionary(
            static e => e[..e.LastIndexOf('@')],
            static e => Convert.ToInt32(e[(e.LastIndexOf('@') + 1)..], 16),
            StringComparer.OrdinalIgnoreCase);

    private static AssemblyResult Asm(string source, Dictionary<string, int>? map = null, string cpu = "stub", string? syntax = null)
    {
        AssemblerTarget target = AssemblerTargets.Find(cpu)!;
        var assembler = new TwoPassAssembler(Repo.LoadTarget(target), syntax is null ? target.DefaultSyntax : target.FindSyntax(syntax)!);
        return assembler.Assemble(source, "main", _ => null, [], _ => null, map);
    }

    [Fact]
    public void Segments_Start_At_Mapped_Origins_With_Bss_Skipped()
    {
        var result = Asm(
            ".segment \"CODE\"\nLDI 1\n.segment \"DATA\"\n.byte 9\n.bss\nbuf: .byte 0\n.segment \"CODE\"\nHLT\n",
            Map("CODE@0", "DATA@10", "BSS@20"));

        result.Image.Should().Equal(0x01, 0x01, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x09);
        result.Symbols["buf"].Should().Be(0x20);
        result.Segments.Should().Equal(
            new SegmentSpan("CODE", 0, 3, false),
            new SegmentSpan("DATA", 0x10, 0x11, false),
            new SegmentSpan("BSS", 0x20, 0x21, true));
    }

    [Fact]
    public void Cross_Segment_Forward_Reference_Works()
    {
        var result = Asm("JMP done\n.segment \"DATA\"\nval: .byte 7\n.segment \"CODE\"\ndone: LDA val\nHLT\n", Map("CODE@0", "DATA@100"));

        result.Image[0..3].Should().Equal(0x04, 0x03, 0x00);
        result.Symbols["done"].Should().Be(3);
        result.Symbols["val"].Should().Be(0x100);
    }

    [Fact]
    public void Org_Sets_Current_Segment_Counter()
    {
        var result = Asm(".segment \"DATA\"\n.org $50\n.byte 1\n", Map("DATA@0"));

        result.Segments.Should().ContainSingle().Which.Should().Be(new SegmentSpan("DATA", 0x50, 0x51, false));
    }

    [Fact]
    public void Program_Without_Segments_Is_Single_Code()
    {
        var result = Asm("LDI 1\nHLT");

        result.Segments.Should().ContainSingle().Which.Should().Be(new SegmentSpan("CODE", 0, 3, false));
    }

    [Fact]
    public void Mos_Uses_Dotted_Segment_Names()
    {
        var result = Asm("*= $0600\nLDA #1\n.SEGMENT \"DATA\"\n.BYTE 9\n.BSS\nBUF: .BYTE 0\n.SEGMENT \"CODE\"\nRTS", Map("CODE@0600", "DATA@0200", "BSS@0100"), "6502", "mos");

        result.Image.Should().HaveCount(0x403).And.StartWith(0x09);
        result.Image[0x400..].Should().Equal(0xA9, 0x01, 0x60);
        result.Symbols["BUF"].Should().Be(0x100);
        result.Segments.Should().HaveCount(3);
    }

    [Fact]
    public void Bss_Overlapping_Code_Is_Rejected()
    {
        FluentActions.Invoking(() => Asm(".bss\nbuf: .byte 0\n.segment \"CODE\"\nLDI 1\n", Map("CODE@0", "BSS@0")))
            .Should().Throw<AssemblerException>().WithMessage("*BSS*");
    }

    [Fact]
    public void Code_Overlapping_Bss_Is_Rejected()
    {
        FluentActions.Invoking(() => Asm("LDI 1\n.bss\nbuf: .byte 0, 0\n", Map("CODE@0", "BSS@1")))
            .Should().Throw<AssemblerException>().WithMessage("*overlaps BSS*");
    }

    [Fact]
    public void Bad_Segment_Directives_Are_Rejected()
    {
        FluentActions.Invoking(() => Asm(".segment\n"))
            .Should().Throw<AssemblerException>().WithMessage("*.segment needs a quoted name*");
        FluentActions.Invoking(() => Asm(".code 1\n"))
            .Should().Throw<AssemblerException>().WithMessage("*no operand allowed*");
        FluentActions.Invoking(() => Asm(".segment \"A@B\"\n"))
            .Should().Throw<AssemblerException>().WithMessage("*must not contain '@'*");
    }
}
