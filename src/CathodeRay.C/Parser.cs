namespace CathodeRay.C;

/// <summary>Parser rekurencyjny mini-C: funkcje, bloki, deklaracje, <c>if/while/for/return</c>,
/// wyrażenia z priorytetami C (przypisanie najniżej, potem <c>?:</c>, <c>||</c> … jednomian).</summary>
public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly Dictionary<Ast.Node, int> _lines = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Ast.Expr, Ast.Expr> _postfix = new(ReferenceEqualityComparer.Instance);
    private int _pos;

    private Parser(IReadOnlyList<Token> tokens) => _tokens = tokens;

    /// <summary>Parsuje program.</summary>
    /// <param name="source">Tekst programu (dyrektywy <c>#</c> mapowane jak kropkowe).</param>
    /// <param name="reader">Czyta pliki <c>.include</c> (null = brak).</param>
    /// <returns>Drzewo programu.</returns>
    /// <exception cref="CParseException">Błąd składni z pozycją.</exception>
    /// <exception cref="CPreprocessException">Błąd dyrektywy.</exception>
    public static Ast.Program Parse(string source, Func<string, string?>? reader = null) =>
        new Parser(Lexer.Tokenize(CPreprocessor.Expand(CPreprocessor.MapDirectives(source), reader))).Program();

    private static bool IsType(Token token) =>
        token is { Kind: TokenKind.Keyword } && token.Text is "uchar" or "int" or "void";

    private T At<T>(int line, T node)
        where T : Ast.Node
    {
        _lines[node] = line;
        return node;
    }

    private Token Peek(int ahead = 0) => _pos + ahead < _tokens.Count ? _tokens[_pos + ahead] : _tokens[^1];

    private Token Next() => _tokens[_pos++];

    private bool Take(string text)
    {
        if (Peek() is { Kind: TokenKind.Punct } token && token.Text == text)
        {
            _pos++;
            return true;
        }

        return false;
    }

    private Token Expect(string text)
    {
        if (Take(text))
        {
            return _tokens[_pos - 1];
        }

        Token token = Peek();
        throw new CParseException(token.Line, token.Column, $"expected '{text}', got '{token.Text}'.");
    }

    private Token ExpectKind(TokenKind kind, string what)
    {
        Token token = Peek();
        if (token.Kind != kind)
        {
            throw new CParseException(token.Line, token.Column, $"expected {what}, got '{token.Text}'.");
        }

        return Next();
    }

    private Ast.Program Program()
    {
        var globals = new List<Ast.Decl>();
        var functions = new List<Ast.Function>();
        while (Peek().Kind != TokenKind.End)
        {
            Token type = Peek();
            if (!IsType(type))
            {
                throw new CParseException(type.Line, type.Column, $"expected type, got '{type.Text}'.");
            }

            Next();
            string name = ExpectKind(TokenKind.Ident, "name").Text;
            if (Peek() is { Kind: TokenKind.Punct, Text: "(" })
            {
                functions.Add(FunctionRest(type.Text, name, type.Line));
            }
            else
            {
                globals.Add(GlobalRest(type.Text, name, type.Line));
            }
        }

        if (functions.Count == 0 && globals.Count == 0)
        {
            Token token = Peek();
            throw new CParseException(token.Line, token.Column, "expected a function.");
        }

        return new Ast.Program(
            globals,
            functions,
            new Dictionary<Ast.Node, int>(_lines, ReferenceEqualityComparer.Instance));
    }

    private Ast.Decl GlobalRest(string type, string name, int line)
    {
        int stars = Stars();
        int length = ArrayLength();
        Ast.Expr? init = Take("=") ? Expression() : null;
        Expect(";");
        return At(line, new Ast.Decl(type, name, init, stars, length));
    }

    private int Stars()
    {
        int count = 0;
        while (Take("*"))
        {
            count++;
        }

        return count;
    }

    private Ast.Function FunctionRest(string type, string name, int line)
    {
        Expect("(");
        var parameters = new List<Ast.Param>();
        if (!Take(")"))
        {
            do
            {
                Token paramType = Peek();
                if (!IsType(paramType) || paramType.Text == "void")
                {
                    throw new CParseException(paramType.Line, paramType.Column, "expected parameter type.");
                }

                Next();
                int stars = Stars();
                parameters.Add(new Ast.Param(paramType.Text, ExpectKind(TokenKind.Ident, "parameter name").Text, stars));
            }
            while (Take(","));

            Expect(")");
        }

        if (Take(";"))
        {
            return At(line, new Ast.Function(type, name, parameters, new Ast.Block([]), IsExtern: true));
        }

        return At(line, new Ast.Function(type, name, parameters, Block()));
    }

    private Ast.Block Block()
    {
        Expect("{");
        var items = new List<Ast.Stmt>();
        while (!Take("}"))
        {
            if (Peek().Kind == TokenKind.End)
            {
                Token token = Peek();
                throw new CParseException(token.Line, token.Column, "expected '}'.");
            }

            items.Add(Statement());
        }

        return new Ast.Block(items);
    }

    private Ast.Stmt Statement()
    {
        Token token = Peek();
        if (token is { Kind: TokenKind.Keyword, Text: "if" })
        {
            return If();
        }

        if (token is { Kind: TokenKind.Keyword, Text: "while" })
        {
            Next();
            Expect("(");
            Ast.Expr cond = Expression();
            Expect(")");
            return At(token.Line, new Ast.While(cond, Statement()));
        }

        if (token is { Kind: TokenKind.Keyword, Text: "for" })
        {
            return For();
        }

        if (token is { Kind: TokenKind.Keyword, Text: "break" or "continue" })
        {
            Next();
            Expect(";");
            return token.Text == "break" ? At(token.Line, new Ast.Break()) : At(token.Line, new Ast.Continue());
        }

        if (token is { Kind: TokenKind.Keyword, Text: "return" })
        {
            Next();
            Ast.Expr? result = Peek() is { Kind: TokenKind.Punct, Text: ";" } ? null : Expression();
            Expect(";");
            return At(token.Line, new Ast.Return(result));
        }

        if (token is { Kind: TokenKind.Punct, Text: "{" })
        {
            return Block();
        }

        if (token is { Kind: TokenKind.Punct, Text: ";" })
        {
            Next();
            return At(token.Line, new Ast.Nop());
        }

        if (IsType(token))
        {
            return Decl();
        }

        Ast.Expr value = Discard(Expression());
        Expect(";");
        return At(token.Line, new Ast.ExprStmt(value));
    }

    private Ast.If If()
    {
        Token keyword = Next();
        Expect("(");
        Ast.Expr cond = Expression();
        Expect(")");
        Ast.Stmt then = Statement();
        Ast.Stmt? els = Peek() is { Kind: TokenKind.Keyword, Text: "else" } ? Else() : null;
        return At(keyword.Line, new Ast.If(cond, then, els));
    }

    private Ast.Stmt Else()
    {
        Next();
        return Statement();
    }

    private Ast.For For()
    {
        Token keyword = Next();
        Expect("(");
        Ast.Stmt? init = Peek() is { Kind: TokenKind.Punct, Text: ";" } ? null : ForInit();
        if (init is not Ast.Decl)
        {
            Expect(";");
        }

        Ast.Expr? cond = Peek() is { Kind: TokenKind.Punct, Text: ";" } ? null : Expression();
        Expect(";");
        Ast.Expr? step = Peek() is { Kind: TokenKind.Punct, Text: ")" } ? null : Discard(Expression());
        Expect(")");
        return At(keyword.Line, new Ast.For(init, cond, step, Statement()));
    }

    private Ast.Stmt ForInit()
    {
        Token first = Peek();
        return IsType(first) ? Decl() : At(first.Line, new Ast.ExprStmt(Discard(Expression())));
    }

    private Ast.Decl Decl()
    {
        Token typeToken = Next();
        string type = typeToken.Text;
        int stars = Stars();
        string name = ExpectKind(TokenKind.Ident, "variable name").Text;
        int length = ArrayLength();
        Ast.Expr? init = Take("=") ? Expression() : null;
        Expect(";");
        return At(typeToken.Line, new Ast.Decl(type, name, init, stars, length));
    }

    private int ArrayLength()
    {
        if (!Take("["))
        {
            return 0;
        }

        Token size = ExpectKind(TokenKind.Number, "array length");
        Expect("]");
        return int.TryParse(size.Text, out int length) ? length : 0;
    }

    private Ast.Expr Expression() => Assignment();

    private Ast.Expr Assignment()
    {
        Ast.Expr left = Conditional();
        Token token = Peek();
        if (token is { Kind: TokenKind.Punct } && token.Text is "=" or "+=" or "-=" or "*=" or "/=" or "%=" or "<<=" or ">>=" or "&=" or "|=" or "^=")
        {
            Next();
            Ast.Expr right = Assignment();
            if (left is Ast.Deref || left is Ast.Index)
            {
                if (token.Text != "=")
                {
                    throw new CParseException(token.Line, token.Column, "compound assignment on pointers is not supported.");
                }

                return new Ast.AssignTo(left, right);
            }

            if (left is not Ast.Var variable)
            {
                throw new CParseException(token.Line, token.Column, "assignment needs a variable.");
            }

            return token.Text == "="
                ? new Ast.Assign(variable.Name, right)
                : new Ast.Assign(variable.Name, new Ast.Binary(token.Text[..^1], left, right));
        }

        return left;
    }

    private Ast.Expr Conditional()
    {
        Ast.Expr cond = Or();
        if (Take("?"))
        {
            Ast.Expr then = Expression();
            Expect(":");
            return new Ast.Ternary(cond, then, Conditional());
        }

        return cond;
    }

    private Ast.Expr Or()
    {
        Ast.Expr left = And();
        while (Take("||"))
        {
            left = new Ast.Binary("||", left, Or());
        }

        return left;
    }

    private Ast.Expr And()
    {
        Ast.Expr left = BitOr();
        while (Take("&&"))
        {
            left = new Ast.Binary("&&", left, And());
        }

        return left;
    }

    private Ast.Expr Binary(Func<Ast.Expr> next, params string[] ops)
    {
        Ast.Expr left = next();
        while (Peek() is { Kind: TokenKind.Punct } token && Array.IndexOf(ops, token.Text) >= 0)
        {
            Next();
            left = new Ast.Binary(token.Text, left, next());
        }

        return left;
    }

    private Ast.Expr BitOr() => Binary(BitXor, "|");

    private Ast.Expr BitXor() => Binary(BitAnd, "^");

    private Ast.Expr BitAnd() => Binary(Equality, "&");

    private Ast.Expr Equality() => Binary(Relational, "==", "!=");

    private Ast.Expr Relational() => Binary(Shift, "<", "<=", ">", ">=");

    private Ast.Expr Shift() => Binary(Additive, "<<", ">>");

    private Ast.Expr Additive() => Binary(Multiplicative, "+", "-");

    private Ast.Expr Multiplicative() => Binary(Unary, "*", "/", "%");

    private Ast.Expr Unary()
    {
        Token token = Peek();
        if (token is { Kind: TokenKind.Punct } && token.Text is "++" or "--")
        {
            Next();
            return Step(Unary(), token, token.Text[0].ToString());
        }

        if (token is { Kind: TokenKind.Punct } && token.Text is "-" or "~" or "!")
        {
            Next();
            return new Ast.Unary(token.Text, Unary());
        }

        if (token is { Kind: TokenKind.Punct, Text: "&" })
        {
            Next();
            Token name = ExpectKind(TokenKind.Ident, "variable name");
            return new Ast.AddressOf(name.Text);
        }

        if (token is { Kind: TokenKind.Punct, Text: "*" })
        {
            Next();
            return new Ast.Deref(Unary());
        }

        return Postfix();
    }

    private Ast.Expr Postfix()
    {
        Ast.Expr baseValue = Primary();
        while (Take("["))
        {
            Ast.Expr index = Expression();
            Expect("]");
            baseValue = new Ast.Index(baseValue, index);
        }

        while (Peek() is { Kind: TokenKind.Punct, Text: "++" or "--" } op)
        {
            Next();
            Ast.Expr updated = Step(baseValue, op, op.Text[0].ToString());
            string undo = op.Text[0] == '+' ? "-" : "+";
            Ast.Expr old = new Ast.Binary(undo, updated, new Ast.Number("1"));
            _postfix[old] = updated;
            baseValue = old;
        }

        return baseValue;
    }

    /// <summary>Wartość <c>x++</c> to <c>(x = x + 1) - 1</c> (poprawna też dla uchar/wskaźników);
    /// w pozycji instrukcji (wartość odrzucona) zostaje samo przypisanie.</summary>
    private Ast.Expr Discard(Ast.Expr expr) =>
        _postfix.TryGetValue(expr, out Ast.Expr? update) ? update : expr;

    private Ast.Expr Step(Ast.Expr target, Token op, string sign)
    {
        var one = new Ast.Number("1");
        return target switch
        {
            Ast.Var variable => new Ast.Assign(variable.Name, new Ast.Binary(sign, target, one)),
            Ast.Deref or Ast.Index => new Ast.AssignTo(target, new Ast.Binary(sign, target, one)),
            _ => throw new CParseException(op.Line, op.Column, $"'{op.Text}' needs a variable."),
        };
    }

    private Ast.Expr Primary()
    {
        Token token = Peek();
        if (token.Kind == TokenKind.Number)
        {
            Next();
            return new Ast.Number(token.Text);
        }

        if (token.Kind == TokenKind.String)
        {
            Next();
            return new Ast.Str(token.Text);
        }

        if (token.Kind == TokenKind.Ident)
        {
            Next();
            if (Take("("))
            {
                var args = new List<Ast.Expr>();
                if (!Take(")"))
                {
                    do
                    {
                        args.Add(Expression());
                    }
                    while (Take(","));

                    Expect(")");
                }

                return new Ast.Call(token.Text, args);
            }

            return new Ast.Var(token.Text);
        }

        if (Take("("))
        {
            Ast.Expr inner = Expression();
            Expect(")");
            return inner;
        }

        throw new CParseException(token.Line, token.Column, $"expected expression, got '{token.Text}'.");
    }
}
