namespace CathodeRay.C;

/// <summary>Parser rekurencyjny mini-C: funkcje, bloki, deklaracje, <c>if/while/for/return</c>,
/// wyrażenia z priorytetami C (przypisanie najniżej, potem <c>?:</c>, <c>||</c> … jednomian).</summary>
public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly Dictionary<Ast.Node, int> _lines = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Ast.Expr, Ast.Expr> _postfix = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, int> _enums = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Type, int Stars)> _typedefs = new(StringComparer.Ordinal);
    private readonly List<Ast.StructDef> _structs = [];
    private int _anonymous;
    private int _pos;

    private Parser(IReadOnlyList<Token> tokens) => _tokens = tokens;

    /// <summary>Parsuje program.</summary>
    /// <param name="source">Tekst programu (dyrektywy <c>#</c> obsługuje preprocesor).</param>
    /// <param name="reader">Czyta pliki <c>#include</c> (<c>&lt;f&gt;</c> z nawiasami; null = brak).</param>
    /// <param name="defines">Makra z linii poleceń (nazwa → wartość).</param>
    /// <returns>Drzewo programu.</returns>
    /// <exception cref="CParseException">Błąd składni z pozycją.</exception>
    /// <exception cref="CPreprocessException">Błąd dyrektywy.</exception>
    public static Ast.Program Parse(string source, Func<string, string?>? reader = null, IReadOnlyDictionary<string, string>? defines = null) =>
        new Parser(Lexer.Tokenize(CPreprocessor.Process(source, reader, defines))).Program();

    private static bool TryValue(Ast.Expr expr, out int value)
    {
        value = 0;
        if (expr is not Ast.Number number)
        {
            return false;
        }

        string text = number.Text;
        bool hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        return int.TryParse(hex ? text[2..] : text, hex ? System.Globalization.NumberStyles.HexNumber : System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Składa działania na dwóch stałych (16-bit z zawijaniem): <c>1 &lt;&lt; 15</c>
    /// i <c>200 + 100</c> są <c>int</c>, a nie 8-bitowym uchar.</summary>
    private static Ast.Expr Fold(Ast.Binary binary)
    {
        if (!TryValue(binary.Left, out int a) || !TryValue(binary.Right, out int b))
        {
            return binary;
        }

        int? result = binary.Op switch
        {
            "+" => a + b,
            "-" => a - b,
            "*" => a * b,
            "/" when b != 0 => a / b,
            "%" when b != 0 => a % b,
            "<<" when b < 16 => a << b,
            ">>" when b < 16 => a >> b,
            "&" => a & b,
            "|" => a | b,
            "^" => a ^ b,
            _ => null,
        };
        return result is int folded
            ? new Ast.Number((folded & 0xFFFF).ToString(System.Globalization.CultureInfo.InvariantCulture))
            : binary;
    }

    private static bool IsBuiltinType(Token token) =>
        token is { Kind: TokenKind.Keyword } && token.Text is "uchar" or "int" or "uint" or "void" or "struct";

    private bool IsType(Token token) =>
        IsBuiltinType(token) || (token is { Kind: TokenKind.Keyword, Text: "const" }) || (token.Kind == TokenKind.Ident && _typedefs.ContainsKey(token.Text));

    /// <summary>Zjada <c>static</c>/<c>extern</c> przed deklaracją.</summary>
    private DeclFlags Modifiers()
    {
        DeclFlags flags = DeclFlags.None;
        while (Peek() is { Kind: TokenKind.Keyword, Text: "static" or "extern" } modifier)
        {
            Next();
            flags |= modifier.Text == "static" ? DeclFlags.Static : DeclFlags.Extern;
        }

        return flags;
    }

    private bool StartsDeclaration(Token token) =>
        IsType(token) || token is { Kind: TokenKind.Keyword, Text: "static" or "extern" };

    /// <summary>Nazwa typu (słowo kluczowe albo alias <c>typedef</c>) z gwiazdkami aliasu.</summary>
    private (string Type, int Stars) TypeSpec()
    {
        bool isConst = ConsumeConst();
        Token token = Next();
        (string Type, int Stars) spec;
        if (token is { Kind: TokenKind.Keyword, Text: "struct" })
        {
            string name = Peek().Kind == TokenKind.Ident ? Next().Text : $"__anon{_anonymous++}";
            if (Peek() is { Kind: TokenKind.Punct, Text: "{" })
            {
                StructBody(name, token.Line);
            }

            spec = ($"struct {name}", 0);
        }
        else
        {
            spec = token.Kind == TokenKind.Ident && _typedefs.TryGetValue(token.Text, out (string Type, int Stars) alias)
                ? alias
                : (token.Text, 0);
        }

        isConst |= ConsumeConst();
        return isConst && !spec.Type.StartsWith("const ", StringComparison.Ordinal) ? ($"const {spec.Type}", spec.Stars) : spec;
    }

    private bool ConsumeConst()
    {
        bool found = false;
        while (Peek() is { Kind: TokenKind.Keyword, Text: "const" })
        {
            Next();
            found = true;
        }

        return found;
    }

    private void StructBody(string name, int line)
    {
        Expect("{");
        var fields = new List<Ast.FieldDecl>();
        while (!Take("}"))
        {
            Token at = Peek();
            if (!IsType(at))
            {
                throw new CParseException(at.Line, at.Column, $"expected field type, got '{at.Text}'.");
            }

            (string type, int baseStars) = TypeSpec();
            int stars = baseStars + Stars();
            if (TryFnPtr(type, stars, out string fieldFn, out string fieldFnName, out int fieldFnLength, out Ast.Expr? fieldFnExpr))
            {
                if (fieldFnExpr is not null)
                {
                    throw new CParseException(at.Line, at.Column, "field array needs a literal length.");
                }

                Expect(";");
                fields.Add(At(at.Line, new Ast.FieldDecl(fieldFn, 0, fieldFnName, fieldFnLength)));
                continue;
            }

            string field = ExpectKind(TokenKind.Ident, "field name").Text;
            int length = ArrayLength(out bool unsized, out Ast.Expr? fieldLength);
            if (unsized || fieldLength is not null)
            {
                throw new CParseException(at.Line, at.Column, "field array needs a literal length.");
            }

            Expect(";");
            fields.Add(At(at.Line, new Ast.FieldDecl(type, stars, field, length)));
        }

        _structs.Add(At(line, new Ast.StructDef(name, fields)));
    }

    private void TypedefDecl()
    {
        Next();
        Token at = Peek();
        if (!IsType(at))
        {
            throw new CParseException(at.Line, at.Column, $"expected type, got '{at.Text}'.");
        }

        (string type, int baseStars) = TypeSpec();
        int stars = baseStars + Stars();
        if (TryFnPtr(type, stars, out string fnType, out string alias, out _, out _))
        {
            _typedefs[alias] = (fnType, 0);
        }
        else
        {
            _typedefs[ExpectKind(TokenKind.Ident, "type name").Text] = (type, stars);
        }

        Expect(";");
    }

    /// <summary>Deklarator wskaźnika do funkcji <c>(*nazwa[n])(typy)</c> po typie wyniku; typ zapisany jako
    /// <c>fptr&lt;wynik;par1;par2&gt;</c> (gwiazdki jako sufiks <c>*</c>).</summary>
    private bool TryFnPtr(string returnType, int returnStars, out string fnType, out string name, out int length, out Ast.Expr? lengthExpr)
    {
        fnType = string.Empty;
        name = string.Empty;
        length = 0;
        lengthExpr = null;
        if (Peek() is not { Kind: TokenKind.Punct, Text: "(" } || Peek(1) is not { Kind: TokenKind.Punct, Text: "*" })
        {
            return false;
        }

        Next();
        Next();
        if (Peek() is { Kind: TokenKind.Punct, Text: ")" })
        {
            name = string.Empty;
        }
        else
        {
            name = ExpectKind(TokenKind.Ident, "function pointer name").Text;
            length = ArrayLength(out bool unsized, out lengthExpr);
            if (unsized)
            {
                Token at = Peek();
                throw new CParseException(at.Line, at.Column, "function pointer array needs a length.");
            }
        }

        Expect(")");
        Expect("(");
        var parameters = new List<string>();
        if (!Take(")"))
        {
            do
            {
                Token at = Peek();
                if (!IsType(at))
                {
                    throw new CParseException(at.Line, at.Column, $"expected parameter type, got '{at.Text}'.");
                }

                (string type, int baseStars) = TypeSpec();
                int stars = baseStars + Stars();
                if (TryFnPtr(type, stars, out string inner, out _, out _, out _))
                {
                    parameters.Add(inner);
                    continue;
                }

                if (Peek().Kind == TokenKind.Ident)
                {
                    Next();
                }

                if (type == "void" && stars == 0)
                {
                    continue;
                }

                parameters.Add(type + new string('*', stars));
            }
            while (Take(","));

            Expect(")");
        }

        fnType = $"fptr<{returnType + new string('*', returnStars)};{string.Join(";", parameters)}>";
        return true;
    }

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
            DeclFlags flags = Modifiers();
            Token type = Peek();
            if (type is { Kind: TokenKind.Keyword, Text: "enum" })
            {
                EnumDecl();
                continue;
            }

            if (type is { Kind: TokenKind.Keyword, Text: "typedef" })
            {
                TypedefDecl();
                continue;
            }

            if (!IsType(type))
            {
                throw new CParseException(type.Line, type.Column, $"expected type, got '{type.Text}'.");
            }

            (string typeName, int baseStars) = TypeSpec();
            if (Take(";"))
            {
                continue;
            }

            int stars = baseStars + Stars();
            if (TryFnPtr(typeName, stars, out string fnType, out string fnName, out int fnLength, out Ast.Expr? fnLengthExpr))
            {
                globals.Add(DeclRest(fnType, fnName, 0, type.Line, flags, (fnLength, false, fnLengthExpr), Peek()));
                Expect(";");
                continue;
            }

            string name = ExpectKind(TokenKind.Ident, "name").Text;
            if (Peek() is { Kind: TokenKind.Punct, Text: "(" })
            {
                functions.Add(FunctionRest(typeName, name, type.Line, stars, flags.HasFlag(DeclFlags.Static)));
            }
            else
            {
                globals.Add(GlobalRest(typeName, name, stars, type.Line, flags));
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
            new Dictionary<Ast.Node, int>(_lines, ReferenceEqualityComparer.Instance),
            _structs);
    }

    private Ast.Decl GlobalRest(string type, string name, int stars, int line, DeclFlags flags)
    {
        Ast.Decl decl = DeclTail(type, name, stars, line, flags);
        Expect(";");
        return decl;
    }

    /// <summary><c>enum [nazwa] { A, B = 5, C };</c> — stałe podstawiane w miejscu użycia (typ zostaje int).</summary>
    private void EnumDecl()
    {
        Next();
        if (Peek().Kind == TokenKind.Ident)
        {
            Next();
        }

        Expect("{");
        int next = 0;
        while (!Take("}"))
        {
            string name = ExpectKind(TokenKind.Ident, "enum constant").Text;
            if (Take("="))
            {
                Token at = Peek();
                if (!TryValue(Conditional(), out next))
                {
                    throw new CParseException(at.Line, at.Column, "enum value must be a constant.");
                }
            }

            _enums[name] = next & 0xFFFF;
            next++;
            if (!Take(","))
            {
                Expect("}");
                break;
            }
        }

        Expect(";");
    }

    private int Stars()
    {
        int count = 0;
        while (Take("*"))
        {
            count++;
            ConsumeConst();
        }

        return count;
    }

    private Ast.Function FunctionRest(string type, string name, int line, int returnStars = 0, bool isStatic = false)
    {
        Expect("(");
        var parameters = new List<Ast.Param>();
        bool variadic = false;
        if (!Take(")"))
        {
            do
            {
                Token paramType = Peek();
                if (Take("..."))
                {
                    variadic = parameters.Count > 0 ? true : throw new CParseException(paramType.Line, paramType.Column, "'...' needs a named parameter before it.");
                    break;
                }

                if (!IsType(paramType) || paramType.Text == "void")
                {
                    throw new CParseException(paramType.Line, paramType.Column, "expected parameter type.");
                }

                (string paramTypeName, int paramBase) = TypeSpec();
                int stars = paramBase + Stars();
                if (TryFnPtr(paramTypeName, stars, out string paramFn, out string paramFnName, out _, out _))
                {
                    parameters.Add(new Ast.Param(paramFn, paramFnName, 0));
                    continue;
                }

                parameters.Add(new Ast.Param(paramTypeName, ExpectKind(TokenKind.Ident, "parameter name").Text, stars));
            }
            while (Take(","));

            Expect(")");
        }

        if (Take(";"))
        {
            return At(line, new Ast.Function(type, name, parameters, new Ast.Block([]), IsExtern: true, ReturnStars: returnStars, IsStatic: isStatic, IsVariadic: variadic));
        }

        if (variadic)
        {
            Token at = Peek();
            throw new CParseException(at.Line, at.Column, "a variadic function can only be declared (prototype), not defined.");
        }

        return At(line, new Ast.Function(type, name, parameters, Block(), ReturnStars: returnStars, IsStatic: isStatic));
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

        if (token is { Kind: TokenKind.Keyword, Text: "do" })
        {
            Next();
            Ast.Stmt body = Statement();
            Token keyword = Peek();
            if (keyword is not { Kind: TokenKind.Keyword, Text: "while" })
            {
                throw new CParseException(keyword.Line, keyword.Column, $"expected 'while', got '{keyword.Text}'.");
            }

            Next();
            Expect("(");
            Ast.Expr cond = Expression();
            Expect(")");
            Expect(";");
            return At(token.Line, new Ast.DoWhile(body, cond));
        }

        if (token is { Kind: TokenKind.Keyword, Text: "switch" })
        {
            return Switch();
        }

        if (token is { Kind: TokenKind.Keyword, Text: "enum" })
        {
            EnumDecl();
            return At(token.Line, new Ast.Nop());
        }

        if (token is { Kind: TokenKind.Keyword, Text: "typedef" })
        {
            TypedefDecl();
            return At(token.Line, new Ast.Nop());
        }

        if (token is { Kind: TokenKind.Keyword, Text: "goto" })
        {
            Next();
            string target = ExpectKind(TokenKind.Ident, "label").Text;
            Expect(";");
            return At(token.Line, new Ast.Goto(target));
        }

        if (token.Kind == TokenKind.Ident && Peek(1) is { Kind: TokenKind.Punct, Text: ":" } && !IsType(token))
        {
            Next();
            Next();
            return At(token.Line, new Ast.Label(token.Text));
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

        if (StartsDeclaration(token))
        {
            return DeclOrTypeOnly();
        }

        Ast.Expr value = Discard(Expression());
        Expect(";");
        return At(token.Line, new Ast.ExprStmt(value));
    }

    private Ast.Switch Switch()
    {
        Token keyword = Next();
        Expect("(");
        Ast.Expr value = Expression();
        Expect(")");
        Expect("{");
        var cases = new List<Ast.SwitchCase>();
        while (!Take("}"))
        {
            Token label = Next();
            Ast.Expr? caseValue = null;
            if (label is { Kind: TokenKind.Keyword, Text: "case" })
            {
                caseValue = Conditional();
            }
            else if (label is not { Kind: TokenKind.Keyword, Text: "default" })
            {
                throw new CParseException(label.Line, label.Column, $"expected 'case' or 'default', got '{label.Text}'.");
            }

            Expect(":");
            var body = new List<Ast.Stmt>();
            while (Peek() is not { Kind: TokenKind.Keyword, Text: "case" or "default" } && Peek() is not { Kind: TokenKind.Punct, Text: "}" })
            {
                if (Peek().Kind == TokenKind.End)
                {
                    Token end = Peek();
                    throw new CParseException(end.Line, end.Column, "expected '}'.");
                }

                body.Add(Statement());
            }

            cases.Add(At(label.Line, new Ast.SwitchCase(caseValue, body)));
        }

        return At(keyword.Line, new Ast.Switch(value, cases));
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

    private Ast.Stmt DeclOrTypeOnly()
    {
        DeclFlags flags = Modifiers();
        if (IsStructOnly())
        {
            return At(Peek().Line, new Ast.Nop());
        }

        return Decl(flags);
    }

    /// <summary>Wygląda na <c>struct X { … };</c> albo <c>struct X;</c> bez deklarowanej zmiennej.</summary>
    private bool IsStructOnly()
    {
        if (Peek() is not { Kind: TokenKind.Keyword, Text: "struct" })
        {
            return false;
        }

        int save = _pos;
        int structs = _structs.Count;
        _ = TypeSpec();
        if (Take(";"))
        {
            return true;
        }

        _pos = save;
        if (_structs.Count > structs)
        {
            _structs.RemoveRange(structs, _structs.Count - structs);
        }

        return false;
    }

    private Ast.Decl Decl(DeclFlags flags = DeclFlags.None)
    {
        Token typeToken = Peek();
        (string type, int baseStars) = TypeSpec();
        int stars = baseStars + Stars();
        Ast.Decl decl;
        if (TryFnPtr(type, stars, out string fnType, out string fnName, out int fnLength, out Ast.Expr? fnLengthExpr))
        {
            decl = DeclRest(fnType, fnName, 0, typeToken.Line, flags, (fnLength, false, fnLengthExpr), Peek());
        }
        else
        {
            string name = ExpectKind(TokenKind.Ident, "variable name").Text;
            decl = DeclTail(type, name, stars, typeToken.Line, flags);
        }

        Expect(";");
        return decl;
    }

    /// <summary>Reszta deklaracji po nazwie: <c>[n]</c> lub <c>[]</c>, opcjonalnie <c>= init</c>
    /// (dla tablicy <c>{a, b}</c> lub napis; <c>[]</c> bierze długość z inicjalizatora).</summary>
    private Ast.Decl DeclTail(string type, string name, int stars, int line, DeclFlags flags = DeclFlags.None)
    {
        Token open = Peek();
        int length = ArrayLength(out bool unsized, out Ast.Expr? lengthExpr);
        return DeclRest(type, name, stars, line, flags, (length, unsized, lengthExpr), open);
    }

    private Ast.Decl DeclRest(string type, string name, int stars, int line, DeclFlags flags, (int Length, bool Unsized, Ast.Expr? Expr) array, Token open)
    {
        (int length, bool unsized, Ast.Expr? lengthExpr) = array;
        Ast.Expr? init = null;
        if (Take("="))
        {
            init = length > 0 || unsized || Peek() is { Kind: TokenKind.Punct, Text: "{" } ? ArrayInit() : Expression();
        }

        if (unsized)
        {
            length = init switch
            {
                Ast.InitList list => list.Items.Count,
                Ast.Str str => str.Value.Length + 1,
                _ => throw new CParseException(open.Line, open.Column, "array '[]' needs an initializer."),
            };
        }

        return At(line, new Ast.Decl(type, name, init, stars, length, lengthExpr, flags));
    }

    private Ast.Expr ArrayInit()
    {
        if (!Take("{"))
        {
            return Expression();
        }

        var items = new List<Ast.Expr>();
        while (!Take("}"))
        {
            items.Add(Peek() is { Kind: TokenKind.Punct, Text: "{" } ? ArrayInit() : Conditional());
            if (!Take(","))
            {
                Expect("}");
                break;
            }
        }

        return new Ast.InitList(items);
    }

    private int ArrayLength(out bool unsized, out Ast.Expr? lengthExpr)
    {
        unsized = false;
        lengthExpr = null;
        if (!Take("["))
        {
            return 0;
        }

        if (Take("]"))
        {
            unsized = true;
            return 0;
        }

        Ast.Expr lengthValue = Conditional();
        Expect("]");
        if (TryValue(lengthValue, out int length))
        {
            return length > 0 ? length : throw new CParseException(Peek().Line, Peek().Column, "array length must be positive.");
        }

        lengthExpr = lengthValue;
        return 1;
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
            if (left is Ast.Deref or Ast.Index or Ast.Member)
            {
                if (token.Text == "=")
                {
                    return new Ast.AssignTo(left, right);
                }

                string op = token.Text[..^1];
                return new Ast.AssignOpTo(left, op, right, new Ast.Binary(op, left, right));
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
            left = Fold(new Ast.Binary(token.Text, left, next()));
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
            Ast.Expr operand = Unary();
            if (token.Text is "-" or "~" && TryValue(operand, out int value))
            {
                int folded = token.Text == "-" ? -value : ~value;
                return new Ast.Number((folded & 0xFFFF).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            return new Ast.Unary(token.Text, operand);
        }

        if (token is { Kind: TokenKind.Punct, Text: "(" } && IsType(Peek(1)))
        {
            Next();
            (string castType, int castBase) = TypeSpec();
            int castStars = castBase + Stars();
            if (TryFnPtr(castType, castStars, out string fnType, out _, out _, out _))
            {
                castType = fnType;
                castStars = 0;
            }

            Expect(")");
            return At(token.Line, new Ast.Cast(castType, castStars, Unary()));
        }

        if (token is { Kind: TokenKind.Punct, Text: "&" })
        {
            Next();
            Ast.Expr target = Unary();
            return target switch
            {
                Ast.Var variable => new Ast.AddressOf(variable.Name),
                Ast.Deref or Ast.Index or Ast.Member => new Ast.AddressOfExpr(target),
                _ => throw new CParseException(token.Line, token.Column, "'&' needs a variable, field or element."),
            };
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
        while (true)
        {
            if (Take("["))
            {
                Ast.Expr index = Expression();
                Expect("]");
                baseValue = new Ast.Index(baseValue, index);
            }
            else if (Peek() is { Kind: TokenKind.Punct, Text: "(" })
            {
                Next();
                var arguments = new List<Ast.Expr>();
                if (!Take(")"))
                {
                    do
                    {
                        arguments.Add(Expression());
                    }
                    while (Take(","));

                    Expect(")");
                }

                baseValue = new Ast.CallExpr(baseValue, arguments);
            }
            else if (Peek() is { Kind: TokenKind.Punct, Text: "." or "->" } access)
            {
                Next();
                baseValue = new Ast.Member(baseValue, ExpectKind(TokenKind.Ident, "field name").Text, access.Text == "->");
            }
            else
            {
                break;
            }
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
            Ast.Deref or Ast.Index or Ast.Member => new Ast.AssignOpTo(target, sign, one, new Ast.Binary(sign, target, one)),
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

        if (token is { Kind: TokenKind.Keyword, Text: "sizeof" })
        {
            Next();
            bool paren = Take("(");
            Token operand = Peek();
            Ast.Expr size;
            if (paren && IsType(operand))
            {
                (string sizeType, int sizeBase) = TypeSpec();
                int stars = sizeBase + Stars();
                string bareType = sizeType.StartsWith("const ", StringComparison.Ordinal) ? sizeType[6..] : sizeType;
                size = bareType.StartsWith("struct ", StringComparison.Ordinal) && stars == 0
                    ? new Ast.SizeOfType(bareType, stars)
                    : new Ast.Number(stars > 0 || bareType is "int" or "uint" ? "2" : "1");
            }
            else
            {
                Ast.Expr target = paren ? Expression() : Unary();
                size = target is Ast.Var variable ? new Ast.SizeOf(variable.Name) : new Ast.SizeOfExpr(target);
            }

            if (paren)
            {
                Expect(")");
            }

            return size;
        }

        if (token.Kind == TokenKind.Ident && _enums.TryGetValue(token.Text, out int enumValue) && Peek(1) is not { Kind: TokenKind.Punct, Text: "(" })
        {
            Next();
            return new Ast.Number(enumValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
