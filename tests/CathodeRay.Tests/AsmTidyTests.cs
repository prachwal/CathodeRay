using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Test metody Tidy w Z80Isa i Intel8080Isa: usuwa zbędne przeniesienia bajtów (BC/DE ↔ HL po kopii).</summary>
public sealed class AsmTidyTests
{
    [Fact]
    public void Z80_RemovesRedundant_BcToHl()
    {
        string input = "ld c,l\nld b,h\nld l,c\nld h,b\nld (x),a";
        string expected = "ld c,l\nld b,h\nld (x),a";

        string result = AsmPeephole.TidyZ80(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Z80_RemovesRedundant_DeToHl()
    {
        string input = "ld e,l\nld d,h\nld l,e\nld h,d\nld (x),a";
        string expected = "ld e,l\nld d,h\nld (x),a";

        string result = AsmPeephole.TidyZ80(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Z80_IgnoresWhen_LabelBetween()
    {
        string input = "ld c,l\nld b,h\n__label:\nld l,c\nld h,b\nret";

        string result = AsmPeephole.TidyZ80(input);

        result.Should().Be(input);
    }

    [Fact]
    public void Z80_IgnoresWhen_MismatchedRegisters()
    {
        string input = "ld c,l\nld b,h\nld l,e\nld h,d\nret";

        string result = AsmPeephole.TidyZ80(input);

        result.Should().Be(input);
    }

    [Fact]
    public void Z80_RemovesMultiple_Patterns()
    {
        string input = "ld c,l\nld b,h\nld l,c\nld h,b\nld e,l\nld d,h\nld l,e\nld h,d\nld (x),a";
        string expected = "ld c,l\nld b,h\nld e,l\nld d,h\nld (x),a";

        string result = AsmPeephole.TidyZ80(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Z80_IgnoresWithCarriageReturns()
    {
        string input = "ld c,l\r\nld b,h\r\nld l,c\r\nld h,b\r\nld (x),a";
        string expected = "ld c,l\r\nld b,h\r\nld (x),a";

        string result = AsmPeephole.TidyZ80(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Z80_RemovesDead_BcBeforeRet()
    {
        string input = "ld c,l\nld b,h\nret";
        string expected = "ret";

        string result = AsmPeephole.TidyZ80(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Z80_RemovesDead_DeBeforeRet()
    {
        string input = "ld e,l\nld d,h\nret";
        string expected = "ret";

        string result = AsmPeephole.TidyZ80(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Z80_IgnoresDead_RetWithCondition()
    {
        string input = "ld c,l\nld b,h\nret z";
        string expected = "ld c,l\nld b,h\nret z";

        string result = AsmPeephole.TidyZ80(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Z80_RemovesDead_BcWriteAcrossLabelBeforeRet()
    {
        string input = "ld c,l\nld b,h\nf__ret:\n;c:x.c:1\nret";
        string expected = "f__ret:\n;c:x.c:1\nret";

        string result = AsmPeephole.TidyZ80(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Z80_IgnoresDead_InstructionBeforeRet()
    {
        string input = "ld c,l\nld b,h\nld a,1\nret";
        string expected = "ld c,l\nld b,h\nld a,1\nret";

        string result = AsmPeephole.TidyZ80(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Z80_TwiceFunctionTest()
    {
        const string Source = "int twice(int x) { return x + x; } int main() { return twice(21); }";

        CcRun.RunOn(Source, "z80").Value.Should().Be(42);
    }

    [Fact]
    public void I8080_RemovesRedundant_BcToHl()
    {
        string input = "mov c,l\nmov b,h\nmov l,c\nmov h,b\nsta x";
        string expected = "mov c,l\nmov b,h\nsta x";

        string result = AsmPeephole.Tidy8080(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void I8080_RemovesRedundant_DeToHl()
    {
        string input = "mov e,l\nmov d,h\nmov l,e\nmov h,d\nsta x";
        string expected = "mov e,l\nmov d,h\nsta x";

        string result = AsmPeephole.Tidy8080(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void I8080_IgnoresWhen_LabelBetween()
    {
        string input = "mov c,l\nmov b,h\n__label:\nmov l,c\nmov h,b\nret";

        string result = AsmPeephole.Tidy8080(input);

        result.Should().Be(input);
    }

    [Fact]
    public void I8080_IgnoresWhen_MismatchedRegisters()
    {
        string input = "mov c,l\nmov b,h\nmov l,e\nmov h,d\nret";

        string result = AsmPeephole.Tidy8080(input);

        result.Should().Be(input);
    }

    [Fact]
    public void I8080_RemovesMultiple_Patterns()
    {
        string input = "mov c,l\nmov b,h\nmov l,c\nmov h,b\nmov e,l\nmov d,h\nmov l,e\nmov h,d\nsta x";
        string expected = "mov c,l\nmov b,h\nmov e,l\nmov d,h\nsta x";

        string result = AsmPeephole.Tidy8080(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void I8080_RemovesDead_BcBeforeRet()
    {
        string input = "mov c,l\nmov b,h\nret";
        string expected = "ret";

        string result = AsmPeephole.Tidy8080(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void I8080_RemovesDead_DeBeforeRet()
    {
        string input = "mov e,l\nmov d,h\nret";
        string expected = "ret";

        string result = AsmPeephole.Tidy8080(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void I8080_IgnoresDead_RetWithCondition()
    {
        string input = "mov c,l\nmov b,h\nrz";
        string expected = "mov c,l\nmov b,h\nrz";

        string result = AsmPeephole.Tidy8080(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void I8080_RemovesDead_BcWriteAcrossLabelBeforeRet()
    {
        string input = "mov c,l\nmov b,h\nf__ret:\n;c:x.c:1\nret";
        string expected = "f__ret:\n;c:x.c:1\nret";

        string result = AsmPeephole.Tidy8080(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void I8080_IgnoresDead_InstructionBeforeRet()
    {
        string input = "mov c,l\nmov b,h\nmvi a,1\nret";
        string expected = "mov c,l\nmov b,h\nmvi a,1\nret";

        string result = AsmPeephole.Tidy8080(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void I8080_TwiceFunctionTest()
    {
        const string Source = "int twice(int x) { return x + x; } int main() { return twice(21); }";

        CcRun.RunOn(Source, "8080").Value.Should().Be(42);
    }

    [Fact]
    public void Dead_Bc_Write_Stays_When_Label_Is_Followed_By_Other_Code()
    {
        AsmPeephole.TidyZ80("ld c,l\nld b,h\nL:\nld a,1\nret").Should().Be("ld c,l\nld b,h\nL:\nld a,1\nret");
        AsmPeephole.Tidy8080("mov c,l\nmov b,h\nL:\nmvi a,1\nret").Should().Be("mov c,l\nmov b,h\nL:\nmvi a,1\nret");
    }

    [Fact]
    public void Reload_And_Dead_Write_Combine_Before_Ret()
    {
        AsmPeephole.TidyZ80("ld c,l\nld b,h\nld l,c\nld h,b\nf__ret:\nret").Should().Be("f__ret:\nret");
        AsmPeephole.Tidy8080("mov c,l\nmov b,h\nmov l,c\nmov h,b\nf__ret:\nret").Should().Be("f__ret:\nret");
    }

    [Fact]
    public void Z80_XorA_After_Label_And_Call()
    {
        AsmPeephole.TidyZ80("L:\nld a,0\nld c,a").Should().Be("L:\nxor a\nld c,a");
        AsmPeephole.TidyZ80("L:\n;c:x.c:1\nld a,0").Should().Be("L:\n;c:x.c:1\nxor a");
        AsmPeephole.TidyZ80("call f\nld a,0").Should().Be("call f\nxor a");
    }

    [Fact]
    public void Z80_XorA_Ignored_When_Flags_Live()
    {
        // łańcuch sub/sbc: C niesie pożyczkę
        AsmPeephole.TidyZ80("sub (hl)\nld a,0\nsbc a,(hl)").Should().Be("sub (hl)\nld a,0\nsbc a,(hl)");

        // skok warunkowy i wołanie warunkowe zostawiają znane flagi na spadku
        AsmPeephole.TidyZ80("call z,f\nld a,0").Should().Be("call z,f\nld a,0");
        AsmPeephole.TidyZ80("ld b,1\nld a,0").Should().Be("ld b,1\nld a,0");
    }

    [Fact]
    public void I8080_XorA_After_Label_And_Call()
    {
        AsmPeephole.Tidy8080("L:\nmvi a,0\nmov c,a").Should().Be("L:\nxra a\nmov c,a");
        AsmPeephole.Tidy8080("call f\nmvi a,0").Should().Be("call f\nxra a");
    }

    [Fact]
    public void I8080_XorA_Ignored_When_Flags_Live()
    {
        AsmPeephole.Tidy8080("sub m\nmvi a,0\nsbb m").Should().Be("sub m\nmvi a,0\nsbb m");
        AsmPeephole.Tidy8080("call z,f\nmvi a,0").Should().Be("call z,f\nmvi a,0");
    }

    [Fact]
    public void Z80_SwapReload_Collapses_To_Copy()
    {
        AsmPeephole.TidyZ80("ex de,hl\nld l,e\nld h,d\nld a,(hl)").Should().Be("ld e,l\nld d,h\nld a,(hl)");
        AsmPeephole.TidyZ80("ex de,hl\r\nld l,e\r\nld h,d\r\nld a,(hl)").Should().Be("ld e,l\r\nld d,h\r\nld a,(hl)");
    }

    [Fact]
    public void Z80_SwapReload_Ignores_Label_Between()
    {
        AsmPeephole.TidyZ80("ex de,hl\nL:\nld l,e\nld h,d").Should().Be("ex de,hl\nL:\nld l,e\nld h,d");
    }
}
