using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Test metody Tidy w Z80Isa i Intel8080Isa: usuwa zbędne przeniesienia bajtów (BC/DE ↔ HL po kopii).</summary>
public sealed class AsmTidyTests
{
    [Fact]
    public void Z80_RemovesRedundant_BcToHl()
    {
        string input = "ld c,l\nld b,h\nld l,c\nld h,b\nret";
        string expected = "ld c,l\nld b,h\nret";

        string result = Z80Isa.Tidy(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Z80_RemovesRedundant_DeToHl()
    {
        string input = "ld e,l\nld d,h\nld l,e\nld h,d\nret";
        string expected = "ld e,l\nld d,h\nret";

        string result = Z80Isa.Tidy(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Z80_IgnoresWhen_LabelBetween()
    {
        string input = "ld c,l\nld b,h\n__label:\nld l,c\nld h,b\nret";

        string result = Z80Isa.Tidy(input);

        result.Should().Be(input);
    }

    [Fact]
    public void Z80_IgnoresWhen_MismatchedRegisters()
    {
        string input = "ld c,l\nld b,h\nld l,e\nld h,d\nret";

        string result = Z80Isa.Tidy(input);

        result.Should().Be(input);
    }

    [Fact]
    public void Z80_RemovesMultiple_Patterns()
    {
        string input = "ld c,l\nld b,h\nld l,c\nld h,b\nld e,l\nld d,h\nld l,e\nld h,d\nret";
        string expected = "ld c,l\nld b,h\nld e,l\nld d,h\nret";

        string result = Z80Isa.Tidy(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void Z80_IgnoresWithCarriageReturns()
    {
        string input = "ld c,l\r\nld b,h\r\nld l,c\r\nld h,b\r\nret";
        string expected = "ld c,l\r\nld b,h\r\nret";

        string result = Z80Isa.Tidy(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void I8080_RemovesRedundant_BcToHl()
    {
        string input = "mov c,l\nmov b,h\nmov l,c\nmov h,b\nret";
        string expected = "mov c,l\nmov b,h\nret";

        string result = Intel8080Isa.Tidy(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void I8080_RemovesRedundant_DeToHl()
    {
        string input = "mov e,l\nmov d,h\nmov l,e\nmov h,d\nret";
        string expected = "mov e,l\nmov d,h\nret";

        string result = Intel8080Isa.Tidy(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void I8080_IgnoresWhen_LabelBetween()
    {
        string input = "mov c,l\nmov b,h\n__label:\nmov l,c\nmov h,b\nret";

        string result = Intel8080Isa.Tidy(input);

        result.Should().Be(input);
    }

    [Fact]
    public void I8080_IgnoresWhen_MismatchedRegisters()
    {
        string input = "mov c,l\nmov b,h\nmov l,e\nmov h,d\nret";

        string result = Intel8080Isa.Tidy(input);

        result.Should().Be(input);
    }

    [Fact]
    public void I8080_RemovesMultiple_Patterns()
    {
        string input = "mov c,l\nmov b,h\nmov l,c\nmov h,b\nmov e,l\nmov d,h\nmov l,e\nmov h,d\nret";
        string expected = "mov c,l\nmov b,h\nmov e,l\nmov d,h\nret";

        string result = Intel8080Isa.Tidy(input);

        result.Should().Be(expected);
    }
}
