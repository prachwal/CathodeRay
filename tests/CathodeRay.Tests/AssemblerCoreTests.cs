using CathodeRay.Assembler;
using CathodeRay.Assembler.Isa;
using CathodeRay.Assembler.Syntax;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Mechanizmy rdzenia asemblera niezależne od konkretnego CPU (celem jest mały, sztuczny zestaw instrukcji).</summary>
public sealed class AssemblerCoreTests
{
    private static InstructionSet TinyIsa(Endianness endianness = Endianness.Little) => new(
        "tiny",
        endianness,
        [
            new InstructionForm("NOP", OperandPattern.Parse(null), [0xEA]),
            new InstructionForm("LD", OperandPattern.Parse("#{b}"), [0xA9]),
            new InstructionForm("LD", OperandPattern.Parse("{b}"), [0xA5]),
            new InstructionForm("LD", OperandPattern.Parse("{w}"), [0xAD]),
            new InstructionForm("LD", OperandPattern.Parse("({b}),Y"), [0xB1]),
            new InstructionForm("JMP", OperandPattern.Parse("{w}"), [0x4C]),
            new InstructionForm("JMP", OperandPattern.Parse("({w})"), [0x6C]),
            new InstructionForm("BR", OperandPattern.Parse("{r}"), [0x80]),
        ]);

    private static AssemblyResult Asm(string source, SyntaxDialect? dialect = null, Endianness endianness = Endianness.Little) =>
        new TwoPassAssembler(TinyIsa(endianness), dialect ?? SyntaxDialects.Ca65).Assemble(source);

    [Fact]
    public void Chooses_Short_Form_When_Value_Fits_And_Long_For_Forward_Reference()
    {
        Asm("""
                    .org $1000
                    LD $12
                    LD $1234
                    LD later
            later = $12
            """).Image.Should().Equal(0xA5, 0x12, 0xAD, 0x34, 0x12, 0xAD, 0x12, 0x00);
    }

    [Fact]
    public void Most_Specific_Pattern_Wins_Over_Parenthesized_Expression()
    {
        Asm("JMP ($1234)\nLD ($10),Y\nJMP ($10+2)*2").Image
            .Should().Equal(0x6C, 0x34, 0x12, 0xB1, 0x10, 0x4C, 0x24, 0x00);
    }

    [Fact]
    public void Shape_Of_Missing_Mode_Is_An_Error_Not_An_Expression()
    {
        FluentActions.Invoking(() => Asm("LD ($1234)"))
            .Should().Throw<AssemblerException>().WithMessage("*addressing mode ({w}) is not available for LD*");
    }

    [Theory]
    [InlineData("BR *", 0xFE)]
    [InlineData("BR *+2", 0x00)]
    [InlineData("BR *+129", 0x7F)]
    [InlineData("BR *-126", 0x80)]
    public void Relative_Branch_Counts_From_Next_Instruction(string source, int offset)
    {
        Asm(source).Image.Should().Equal(0x80, (byte)offset);
    }

    [Fact]
    public void Relative_Branch_Out_Of_Range_Is_Reported()
    {
        FluentActions.Invoking(() => Asm("BR *+130"))
            .Should().Throw<AssemblerException>().WithMessage("*branch target out of range (128 bytes*");
    }

    [Theory]
    [InlineData(Endianness.Little, 0x34, 0x12)]
    [InlineData(Endianness.Big, 0x12, 0x34)]
    public void Words_Follow_Target_Endianness(Endianness endianness, int first, int second)
    {
        Asm(".word $1234\nLD $1234", endianness: endianness).Image
            .Should().Equal((byte)first, (byte)second, 0xAD, (byte)first, (byte)second);
    }

    [Fact]
    public void Assignment_With_Forward_Reference_Resolves_Before_Second_Pass()
    {
        Asm("""
            size = end - start
                    .org $0200
            start:  .byte size
                    .res 2, $FF
            end:
            """).Image.Should().Equal(0x03, 0xFF, 0xFF);
    }

    [Fact]
    public void Byte_Directive_Accepts_Strings_And_Character_Constants()
    {
        Asm(".byte \"A,B\", 'C', 'DE', 0").Image.Should().Equal(0x41, 0x2C, 0x42, 0x43, 0x44, 0x45, 0x00);
    }

    [Fact]
    public void End_Stops_Assembly()
    {
        Asm("NOP\n.end\nthis is not parsed").Image.Should().Equal(0xEA);
    }

    [Fact]
    public void Ca65_Symbols_Are_Case_Sensitive()
    {
        FluentActions.Invoking(() => Asm("Value = 1\nLD #value"))
            .Should().Throw<AssemblerException>().WithMessage("*undefined symbol 'value'*");
    }

    [Fact]
    public void Mos_Labels_Need_No_Colon_And_Org_Uses_Star()
    {
        const string source = """
                *= $0300
            LOOP    NOP
            VALUE   = 7
                    LD #VALUE
                    JMP LOOP
            """;

        Asm(source, SyntaxDialects.Mos).Image.Should().Equal(0xEA, 0xA9, 0x07, 0x4C, 0x00, 0x03);
    }

    [Theory]
    [InlineData("LD #$1F", 0x1F)]
    [InlineData("LD #%101", 5)]
    [InlineData("LD #<$1234", 0x34)]
    [InlineData("LD #>$1234", 0x12)]
    [InlineData("LD #2+3*4", 14)]
    [InlineData("LD #(2+3)*4", 20)]
    [InlineData("LD #1<<4|1", 0x11)]
    [InlineData("LD #$F0&$3C^1", 0x31)]
    [InlineData("LD #~0&$FF", 0xFF)]
    [InlineData("LD #17%5", 2)]
    [InlineData("LD #'A'", 0x41)]
    public void Evaluates_Ca65_Expressions(string source, int value)
    {
        Asm(source).Image.Should().Equal(0xA9, (byte)value);
    }

    [Theory]
    [InlineData("LD #1/0", "division by zero")]
    [InlineData("LD #(1", "missing ')'")]
    [InlineData("LD #1 2", "unexpected '2'")]
    [InlineData("LD #0FFH", "invalid base-10 number '0FFH'")]
    public void Reports_Expression_Errors(string source, string message)
    {
        FluentActions.Invoking(() => Asm(source)).Should().Throw<AssemblerException>().WithMessage($"*{message}*");
    }

    [Fact]
    public void Listing_Shows_Address_Bytes_Line_And_Source()
    {
        AssemblyResult result = Asm("        .org $0400\nstart:  LD #1\n        .byte 1,2,3,4,5");
        var listing = new StringWriter();

        result.WriteListing(listing);

        listing.ToString().Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n').Should().StartWith(
        [
            "0400                   1          .org $0400",
            "0400  A9 01            2  start:  LD #1",
            "0402  01 02 03 04      3          .byte 1,2,3,4,5",
            "0406  05",
        ]);
        result.Origin.Should().Be(0x0400);
        result.Symbols["start"].Should().Be(0x0400);
    }

    [Fact]
    public void Operand_Pattern_Ignores_Case_And_Spaces_But_Not_Commas_In_Slots()
    {
        OperandPattern pattern = OperandPattern.Parse("({b}),Y");

        pattern.TryMatch(" ( ptr + 1 ) , y ", out string[] captures).Should().BeTrue();
        captures.Should().Equal("ptr + 1");
        OperandPattern.Parse("{w}").TryMatch("1,2", out _).Should().BeFalse();
        OperandPattern.Parse("A,{b}").TryMatch("A,','", out captures).Should().BeTrue();
        captures.Should().Equal("','");
    }
}
