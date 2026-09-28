using CathodeRay.Stub;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class StubAssemblerTests
{
    private static StubIsa Isa()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "data", "instructions", "mcp_stub_instructions.json")))
        {
            dir = dir.Parent;
        }

        return StubIsa.FromJsonFile(Path.Combine(dir!.FullName, "data", "instructions", "mcp_stub_instructions.json"));
    }

    private static byte[] Asm(string source) => StubAssembler.Assemble(source, Isa());

    [Fact]
    public void Encodes_Operand_Sizes()
    {
        Asm("""
            NOP
            LDI 5
            STA $1234
            HLT
            """).Should().Equal(0x00, 0x01, 0x05, 0x05, 0x34, 0x12, 0xFF);
    }

    [Theory]
    [InlineData("LDI 42", 42)]
    [InlineData("LDI $2A", 42)]
    [InlineData("LDI 0x2a", 42)]
    [InlineData("ldi 42", 42)]
    public void Parses_Number_Formats_And_Case(string source, int operand)
    {
        Asm(source).Should().Equal(0x01, (byte)operand);
    }

    [Fact]
    public void Resolves_Forward_And_Backward_Labels()
    {
        Asm("""
            start:  JMP end     ; forward
            loop:   INC
                    JMP loop    ; backward
            end:    JMP start
            """).Should().Equal(0x04, 0x07, 0x00, 0x03, 0x04, 0x03, 0x00, 0x04, 0x00, 0x00);
    }

    [Fact]
    public void Ignores_Comments_And_Blank_Lines()
    {
        Asm("; header\r\n\r\n  NOP ; trailing\r\nlabel_only:\r\n").Should().Equal(0x00);
    }

    [Theory]
    [InlineData("FOO", 1, "unknown mnemonic 'FOO'")]
    [InlineData("NOP\nLDI", 2, "LDI requires an operand")]
    [InlineData("INC 1", 1, "INC takes no operand")]
    [InlineData("LDI 256", 1, "out of range")]
    [InlineData("LDI $FFFFFFFF", 1, "invalid operand '$FFFFFFFF'")]
    [InlineData(".byte 1, 256", 1, "out of range")]
    [InlineData(".byte 1,,2", 1, ".byte has an empty value")]
    [InlineData(".byte", 1, ".byte requires an operand")]
    [InlineData("NOP\nNOP\n.org 1", 3, ".org 1 must be in 2..65535")]
    [InlineData(".org $10000", 1, "must be in 0..65535")]
    [InlineData(".org later\nlater: NOP", 1, "undefined label 'later'")]
    [InlineData(".word 1", 1, "unknown mnemonic '.word'")]
    [InlineData("JMP $10000", 1, "out of range")]
    [InlineData("JMP nowhere", 1, "undefined label 'nowhere'")]
    [InlineData("LDI 1x", 1, "invalid operand '1x'")]
    [InlineData("a:\na: NOP", 2, "duplicate label 'a'")]
    [InlineData("1a: NOP", 1, "invalid label '1a'")]
    public void Reports_Errors_With_Line(string source, int line, string message)
    {
        FluentActions.Invoking(() => Asm(source))
            .Should().Throw<AssemblerException>()
            .Where(e => e.Line == line)
            .WithMessage($"*{message}*");
    }

    [Fact]
    public void Byte_Emits_Values_And_Labels()
    {
        Asm("""
            data:   .byte 1, $FF, 0x10, data, end
            end:    HLT
            """).Should().Equal(0x01, 0xFF, 0x10, 0x00, 0x05, 0xFF);
    }

    [Fact]
    public void Org_Pads_With_Zeros_And_Moves_Labels()
    {
        Asm("""
                    JMP main
                    .org $0008
            main:   LDI 7
                    .ORG 12
            table:  .byte main, table
            """).Should().Equal(0x04, 0x08, 0x00, 0, 0, 0, 0, 0, 0x01, 0x07, 0, 0, 0x08, 0x0C);
    }

    [Fact]
    public void Selects_Opcode_By_Addressing_Mode()
    {
        byte[] expected =
        [
            0x06, 0x34, 0x12,
            0x0C, 0x34, 0x12,
            0x0D, 0x1A, 0x00,
            0x02, 0x07,
            0x0E, 0x00, 0x01,
            0x0F, 0x01,
            0x10, 0x00, 0x02,
            0x08, 0x02,
            0x09,
            0x0A, 0x0E,
            0x0B, 0x00, 0x00,
        ];

        Asm("""
            LDA $1234
            LDA $1234,X
            STA data - 1 , x
            ADD 7
            ADD $0100,X
            ADC 1
            ADC $0200,X
            LDX 2
            INX
            CPX 14
            BNE 0
            data:
            """).Should().Equal(expected);
    }

    [Theory]
    [InlineData("LDI 1,X", "LDI has no indexed mode")]
    [InlineData("INX 1,X", "INX takes no operand")]
    [InlineData("LDA 1,Y", "invalid operand '1,Y'")]
    [InlineData("LDA 1,X,X", "invalid operand '1,X,X'")]
    [InlineData("LDX", "LDX requires an operand")]
    public void Reports_Addressing_Mode_Errors(string source, string message)
    {
        FluentActions.Invoking(() => Asm(source)).Should().Throw<AssemblerException>().WithMessage($"*{message}*");
    }

    [Fact]
    public void Rejects_Plain_Operand_For_Indexed_Only_Mnemonic()
    {
        const string json = """
            {"instructions":[{"opcode":"01","mnemonic":"LDY","cycles":4,"words":3,
              "operands":[{"name":"a16","type":"address16_x"}]}]}
            """;
        StubIsa isa = StubIsa.FromJson(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)));

        FluentActions.Invoking(() => StubAssembler.Assemble("LDY 5", isa))
            .Should().Throw<AssemblerException>().WithMessage("*LDY requires an indexed operand (a16,X)*");
    }

    [Theory]
    [InlineData("""{"opcode":"01","mnemonic":"LDA","cycles":1,"words":2,"operands":[{"name":"a","type":"address16"}]}""", "needs 3 words")]
    [InlineData("""{"opcode":"01","mnemonic":"LDA","cycles":1,"words":3,"operands":[{"name":"a","type":"zeropage"}]}""", "Unknown operand type 'zeropage'")]
    public void Isa_Rejects_Inconsistent_Modes(string entry, string message)
    {
        string json = $$"""{"instructions":[{{entry}}]}""";

        FluentActions.Invoking(() => StubIsa.FromJson(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json))))
            .Should().Throw<InvalidDataException>().WithMessage($"*{message}*");
    }

    [Fact]
    public void Isa_With_Immediate_And_Absolute_For_Same_Mnemonic_Is_Ambiguous()
    {
        const string json = """
            {"instructions":[
              {"opcode":"01","mnemonic":"ADD","cycles":2,"words":2,"operands":[{"name":"d8","type":"immediate8"}]},
              {"opcode":"02","mnemonic":"ADD","cycles":3,"words":3,"operands":[{"name":"a16","type":"address16"}]}]}
            """;
        StubIsa isa = StubIsa.FromJson(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)));

        FluentActions.Invoking(() => StubAssembler.Assemble("NOP", isa))
            .Should().Throw<InvalidOperationException>().WithMessage("*ADD*ambiguous*");
    }

    [Fact]
    public void Evaluates_Label_Arithmetic()
    {
        Asm("""
            op:     LDI 1+2
                    STA op+1
                    .byte end-op, end - 1 + 2
            end:    HLT
            """).Should().Equal(0x01, 0x03, 0x05, 0x01, 0x00, 0x07, 0x08, 0xFF);
    }

    [Theory]
    [InlineData("LDI 1-2", "value -1 out of range")]
    [InlineData("LDI -1", "invalid operand '-1'")]
    [InlineData("LDI 1+", "invalid operand ''")]
    [InlineData("LDI 1+x", "undefined label 'x'")]
    public void Reports_Arithmetic_Errors(string source, string message)
    {
        FluentActions.Invoking(() => Asm(source)).Should().Throw<AssemblerException>().WithMessage($"*{message}*");
    }

    [Fact]
    public void Trailing_Org_Does_Not_Grow_Image()
    {
        Asm("NOP\n.org $100").Should().Equal(0x00);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("65535", 65535)]
    [InlineData("$ff", 255)]
    [InlineData("0X1F", 31)]
    public void NumberLiteral_Parses(string text, int expected)
    {
        NumberLiteral.TryParse(text, out int value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("$")]
    [InlineData("0x")]
    [InlineData("$FFFFFFFF")]
    [InlineData("12a")]
    [InlineData(" 1")]
    public void NumberLiteral_Rejects(string text)
    {
        NumberLiteral.TryParse(text, out _).Should().BeFalse();
    }

    [Fact]
    public void Assembled_Program_Runs_On_StubCpu()
    {
        byte[] image = Asm("""
                    LDI 250
            loop:   ADD 3
                    STA $2000
                    SUB 1
                    INC
                    HLT
            """);
        var bus = new StubBus();
        for (int i = 0; i < image.Length; i++)
        {
            bus.Write((ushort)i, image[i]);
        }

        var cpu = new StubCpu(Isa(), bus);
        while (!cpu.State.Halted)
        {
            cpu.Step();
        }

        bus.Read(0x2000).Should().Be(253);
        cpu.State.A.Should().Be(253);
        cpu.State.Carry.Should().BeTrue();
    }
}
