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
            new InstructionForm("LDI", OperandPattern.Parse("(IX{d})"), [0xDD, 0x7E]),
            new InstructionForm("IM", OperandPattern.Parse("{c=0}"), [0xED, 0x46]),
            new InstructionForm("IM", OperandPattern.Parse("{c=2}"), [0xED, 0x5E]),
            new InstructionForm("BITX", OperandPattern.Parse("{c=1},(IX{d})"), [0xCB, 0x4E]),
            new InstructionForm("BITX", OperandPattern.Parse("{c=2},(IX{d})"), [0xCB, 0x56]),
            new InstructionForm("RLCX", OperandPattern.Parse("(IX{d})"), [0xDD, 0xCB, 0x06], [EncodingPart.Byte(0xDD), EncodingPart.Byte(0xCB), EncodingPart.Slot(0), EncodingPart.Byte(0x06)]),
            new InstructionForm("LDI", OperandPattern.Parse("(IX)"), [0xDD, 0x7E], [EncodingPart.Byte(0xDD), EncodingPart.Byte(0x7E), EncodingPart.Byte(0x00)]),
            new InstructionForm("LDW", OperandPattern.Parse("({w}),{b}"), [0x36], [EncodingPart.Byte(0x36), EncodingPart.Slot(1), EncodingPart.Slot(0)]),
            new InstructionForm("EX", OperandPattern.Parse("AF,AF'"), [0x08]),
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

    [Theory]
    [InlineData("LDI (IX+5)", 0x05)]
    [InlineData("LDI (ix - 1)", 0xFF)]
    [InlineData("LDI (IX+2*3)", 0x06)]
    [InlineData("LDI (IX-128)", 0x80)]
    [InlineData("LDI (IX+127)", 0x7F)]
    public void Signed_Displacement_Is_Encoded_In_Twos_Complement(string source, int displacement)
    {
        Asm(source).Image.Should().Equal(0xDD, 0x7E, (byte)displacement);
    }

    [Theory]
    [InlineData("LDI (IX+128)", "displacement 128 out of range -128..127")]
    [InlineData("LDI (IX-129)", "displacement -129 out of range -128..127")]
    public void Signed_Displacement_Out_Of_Range_Is_Reported(string source, string message)
    {
        FluentActions.Invoking(() => Asm(source)).Should().Throw<AssemblerException>().WithMessage($"*{message}*");
    }

    [Theory]
    [InlineData("IM 0", 0xED, 0x46)]
    [InlineData("IM 1+1", 0xED, 0x5E)]
    public void Constant_Slot_Selects_Opcode_And_Emits_Nothing(string source, int first, int second)
    {
        Asm(source).Image.Should().Equal((byte)first, (byte)second);
    }

    [Fact]
    public void Constant_Slot_Works_Next_To_Forward_Referenced_Field()
    {
        Asm("BITX 2,(IX+off)\noff = 4").Image.Should().Equal(0xCB, 0x56, 0x04);
    }

    [Theory]
    [InlineData("IM 1", "invalid operand '1' for IM (allowed constants: 0, 2)")]
    [InlineData("IM mode\nmode = 2", "'mode' must be known at this point (it selects the IM opcode)")]
    public void Constant_Slot_Errors(string source, string message)
    {
        FluentActions.Invoking(() => Asm(source)).Should().Throw<AssemblerException>().WithMessage($"*{message}*");
    }

    [Theory]
    [InlineData("RLCX (IX+2)", new byte[] { 0xDD, 0xCB, 0x02, 0x06 })]
    [InlineData("LDI (IX)", new byte[] { 0xDD, 0x7E, 0x00 })]
    [InlineData("LDW ($1234),$56", new byte[] { 0x36, 0x56, 0x34, 0x12 })]
    public void Layout_Orders_Opcode_Bytes_And_Fields(string source, byte[] expected)
    {
        Asm(source).Image.Should().Equal(expected);
    }

    [Fact]
    public void Layout_Keeps_Program_Counter_At_Instruction_Start()
    {
        Asm("  .org $10\n  RLCX (IX+*-$10+1)").Image.Should().Equal(0xDD, 0xCB, 0x01, 0x06);
    }

    [Theory]
    [InlineData(0, "layout references fields [], expected [0]")]
    [InlineData(2, "layout references fields [0,0], expected [0]")]
    public void Layout_Must_Reference_Each_Field_Once(int slotReferences, string message)
    {
        EncodingPart[] layout = [EncodingPart.Byte(0xDD), .. Enumerable.Repeat(EncodingPart.Slot(0), slotReferences)];
        var form = new InstructionForm("BAD", OperandPattern.Parse("(IX{d})"), [0xDD], layout);

        FluentActions.Invoking(() => new InstructionSet("bad", Endianness.Little, [form]))
            .Should().Throw<InvalidDataException>().WithMessage($"*{message}*");
    }

    [Theory]
    [InlineData("EX AF,AF' ; komentarz ' z apostrofem")]
    [InlineData("EX AF,AF'")]
    [InlineData("ex af , af'   ; ok")]
    public void Apostrophe_After_Identifier_Does_Not_Open_String(string source)
    {
        Asm(source).Image.Should().Equal(0x08);
    }

    [Fact]
    public void Apostrophe_Still_Opens_Strings_Elsewhere()
    {
        Asm(".byte 'A;B', ';' ; komentarz").Image.Should().Equal(0x41, 0x3B, 0x42, 0x3B);
        Assembler.Directives.OperandList.Split("AF',1").Should().Equal("AF'", "1");
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
    [InlineData("LD #0BH", 0x0B)]
    [InlineData("LD #0FFh", 0xFF)]
    [InlineData("LD #0b101", 5)]
    [InlineData("LD #0x1F", 0x1F)]
    [InlineData("LD #$1F", 0x1F)]
    [InlineData("LD #%1010", 10)]
    [InlineData("LD #17 % 5", 2)]
    [InlineData("LD #1010B", 10)]
    [InlineData("LD #17Q", 15)]
    public void Zilog_Mixes_Intel_Motorola_And_C_Numbers(string source, int value)
    {
        Asm(source, SyntaxDialects.Zilog).Image.Should().Equal(0xA9, (byte)value);
    }

    [Fact]
    public void Zilog_Dollar_Is_Program_Counter_Unless_Followed_By_Hex_Digit()
    {
        Asm("  ORG 1000H\n  JMP $\n  JMP $+3\n  JMP $10", SyntaxDialects.Zilog).Image
            .Should().Equal(0x4C, 0x00, 0x10, 0x4C, 0x06, 0x10, 0x4C, 0x10, 0x00);
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
