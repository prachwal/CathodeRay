using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class CFrontendTests
{
    [Fact]
    public void Lexer_Tokenizes_With_Line_And_Column()
    {
        Lexer.Tokenize("int f() {\n  return 0x1F + 1;\n}\n")
            .Should().Equal(
                new Token(TokenKind.Keyword, "int", 1, 1),
                new Token(TokenKind.Ident, "f", 1, 5),
                new Token(TokenKind.Punct, "(", 1, 6),
                new Token(TokenKind.Punct, ")", 1, 7),
                new Token(TokenKind.Punct, "{", 1, 9),
                new Token(TokenKind.Keyword, "return", 2, 3),
                new Token(TokenKind.Number, "0x1F", 2, 10),
                new Token(TokenKind.Punct, "+", 2, 15),
                new Token(TokenKind.Number, "1", 2, 17),
                new Token(TokenKind.Punct, ";", 2, 18),
                new Token(TokenKind.Punct, "}", 3, 1),
                new Token(TokenKind.End, string.Empty, 4, 1));
    }

    [Fact]
    public void Lexer_Skips_Comments_And_Takes_Longest_Operator()
    {
        IReadOnlyList<Token> tokens = Lexer.Tokenize("a <<= 1; // shift\nb = a >> 2; /* multi\nline */ c = b;");

        tokens.Select(static t => t.Text).Should().Equal("a", "<<=", "1", ";", "b", "=", "a", ">>", "2", ";", "c", "=", "b", ";", string.Empty);
    }

    [Theory]
    [InlineData("@", 1, 1)]
    [InlineData("/* nope", 1, 1)]
    [InlineData("'a'", 1, 1)]
    public void Lexer_Rejects_With_Position(string source, int line, int col)
    {
        FluentActions.Invoking(() => Lexer.Tokenize(source))
            .Should().Throw<CParseException>()
            .Where(e => e.Line == line && e.Column == col);
    }

    public static TheoryData<string> ValidPrograms()
    {
        return new TheoryData<string>
        {
            { "uchar f() { return 1; }" },
            { "int add(int a, int b) { return a + b; }" },
            { "void nop() { ; }" },
            { "int f() { uchar x = 5; int y; y = x * 2 + 1; return y; }" },
            { "int f(int n) { if (n == 0) return 1; else return n; }" },
            { "int f(int n) { while (n > 0) n = n - 1; return n; }" },
            { "int f() { int i; for (i = 0; i < 10; i = i + 1) { } return i; }" },
            { "int f() { for (;;) { return 1; } }" },
            { "int f(int a, int b) { return a < b ? a : b; }" },
            { "int f() { int x = 1; x += 2; x <<= 1; return x & 255 | 0; }" },
            { "int g(int x) { return x; } int f() { return g(1) + g(2); }" },
            { "int f(int a) { if (a && 1 || !a) return ~a; return -a % 3; }" },
        };
    }

    [Theory]
    [MemberData(nameof(ValidPrograms))]
    public void Parser_Accepts(string source) =>
        FluentActions.Invoking(() => Parser.Parse(source)).Should().NotThrow();

    [Fact]
    public void Parser_Builds_Function_Shape()
    {
        Ast.Program program = Parser.Parse("int add(int a, int b) { return a + b; }");

        Ast.Function expected = new(
            "int",
            "add",
            [new Ast.Param("int", "a"), new Ast.Param("int", "b")],
            new Ast.Block([new Ast.Return(new Ast.Binary("+", new Ast.Var("a"), new Ast.Var("b")))]));
        program.Functions.Should().ContainSingle().Which.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Parser_Respects_Precedence_And_Desugars_Compound_Assign()
    {
        Ast.Program program = Parser.Parse("int f() { x = 1 + 2 * 3; y += 4; }");

        Ast.Stmt first = new Ast.ExprStmt(
            new Ast.Assign(
                "x",
                new Ast.Binary("+", new Ast.Number("1"), new Ast.Binary("*", new Ast.Number("2"), new Ast.Number("3")))));
        Ast.Stmt second = new Ast.ExprStmt(
            new Ast.Assign("y", new Ast.Binary("+", new Ast.Var("y"), new Ast.Number("4"))));
        program.Functions[0].Body.Items.Should().Equal(first, second);
    }

    public static TheoryData<string, int, int> InvalidPrograms()
    {
        return new TheoryData<string, int, int>
        {
            { string.Empty, 1, 1 },
            { "int f( { }", 1, 8 },
            { "int f() {", 1, 10 },
            { "int f() }", 1, 9 },
            { "int 1f() { }", 1, 5 },
            { "int f(int) { }", 1, 10 },
            { "int f() { x = ; }", 1, 15 },
            { "int f() { if x) { } }", 1, 14 },
            { "int f() { while (1) { }", 1, 24 },
            { "int f() { for (i = 0 i < 1) { } }", 1, 22 },
            { "int f() { 1 = 2; }", 1, 13 },
            { "int f() { return; return", 1, 25 },
        };
    }

    [Theory]
    [MemberData(nameof(InvalidPrograms))]
    public void Parser_Rejects_With_Position(string source, int line, int col)
    {
        FluentActions.Invoking(() => Parser.Parse(source))
            .Should().Throw<CParseException>()
            .Where(e => e.Line == line && e.Column == col);
    }
}
