namespace CathodeRay.C;

/// <summary>Parser rekurencyjny mini-C: funkcje, bloki, deklaracje, <c>if/while/for/return</c>,
/// wyrażenia z priorytetami C (przypisanie najniżej, potem <c>?:</c>, <c>||</c> … jednomian).</summary>
public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly Dictionary<Ast.Node, int> _lines = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Ast.Expr, Ast.Expr> _postfix = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, Ast.Expr> _enums = new(StringComparer.Ordinal);
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

        // stałe 32-bitowe nie są składane w 16-bitowej arytmetyce parsera
        if (!Literal.TryParse(number.Text, out Literal literal) || literal.IsLong)
        {
            return false;
        }

        value = (int)literal.Value;
        return true;
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

    /// <summary>Wyrażenie zbudowane wyłącznie ze stałych, <c>sizeof</c> i operatorów (wartość liczy checker).</summary>
    private static bool IsConstantShape(Ast.Expr expr) => expr switch
    {
        Ast.Number or Ast.SizeOf or Ast.SizeOfType or Ast.SizeOfExpr or Ast.OffsetOf => true,
        Ast.Unary unary => IsConstantShape(unary.Operand),
        Ast.Binary binary => IsConstantShape(binary.Left) && IsConstantShape(binary.Right),
        Ast.Ternary ternary => IsConstantShape(ternary.Cond) && IsConstantShape(ternary.Then) && IsConstantShape(ternary.Else),
        Ast.Cast cast => IsConstantShape(cast.Value),
        _ => false,
    };

    private static bool IsBuiltinType(Token token) =>
        token is { Kind: TokenKind.Keyword } && token.Text is "uchar" or "int" or "uint" or "long" or "ulong" or "short" or "unsigned" or "signed" or "float" or "void" or "struct" or "union";

    private bool IsType(Token token) =>
        IsBuiltinType(token) || (token is { Kind: TokenKind.Keyword, Text: "const" or "volatile" }) || (token.Kind == TokenKind.Ident && _typedefs.ContainsKey(token.Text));

    /// <summary>Zjada <c>static</c>/<c>extern</c> przed deklaracją.</summary>
    private DeclFlags Modifiers()
    {
        DeclFlags flags = DeclFlags.None;
        while (Peek() is { Kind: TokenKind.Keyword, Text: "static" or "extern" or "inline" or "register" } modifier)
        {
            Next();
            if (modifier.Text is "static" or "extern")
            {
                flags |= modifier.Text == "static" ? DeclFlags.Static : DeclFlags.Extern;
            }
        }

        return flags;
    }

    private bool StartsDeclaration(Token token) =>
        IsType(token) || token is { Kind: TokenKind.Keyword, Text: "static" or "extern" or "inline" or "register" };

    /// <summary>Nazwa typu (słowo kluczowe albo alias <c>typedef</c>) z gwiazdkami aliasu.</summary>
    private (string Type, int Stars) TypeSpec()
    {
        (bool isConst, bool isVolatile) = ConsumeQualifiers();
        Token token = Next();
        (string Type, int Stars) spec;
        if (token is { Kind: TokenKind.Keyword, Text: "struct" or "union" })
        {
            string name = Peek().Kind == TokenKind.Ident ? Next().Text : $"__anon{_anonymous++}";
            if (Peek() is { Kind: TokenKind.Punct, Text: "{" })
            {
                StructBody(name, token.Line, token.Text == "union");
            }

            spec = ($"struct {name}", 0);
        }
        else if (token is { Kind: TokenKind.Keyword, Text: "uchar" or "int" or "uint" or "long" or "ulong" or "short" or "unsigned" or "signed" })
        {
            spec = (IntegerSpec(token), 0);
        }
        else
        {
            spec = token.Kind == TokenKind.Ident && _typedefs.TryGetValue(token.Text, out (string Type, int Stars) alias)
                ? alias
                : (token.Text, 0);
        }

        (bool laterConst, bool laterVolatile) = ConsumeQualifiers();
        string bare = TypeQualifiers.Split(spec.Type, out bool aliasConst, out bool aliasVolatile);
        isConst |= laterConst | aliasConst;
        isVolatile |= laterVolatile | aliasVolatile;
        return (TypeQualifiers.Join(bare, isConst, isVolatile), spec.Stars);
    }

    /// <summary>Składa nazwę typu całkowitego z kilku słów: <c>unsigned char</c>, <c>signed char</c>, <c>unsigned short int</c>, <c>long int</c>,
    /// <c>unsigned long</c>… (<c>short</c> to <c>int</c>, <c>unsigned</c> to <c>uint</c>, <c>char</c> bez <c>signed</c> to <c>uchar</c>).</summary>
    private string IntegerSpec(Token first)
    {
        bool unsigned = false;
        bool signed = false;
        int longs = 0;
        string? baseName = null;
        Token token = first;
        while (true)
        {
            switch (token.Text)
            {
                case "unsigned":
                    unsigned = true;
                    break;
                case "signed":
                    signed = true;
                    break;
                case "short":
                    break;
                case "long":
                    longs++;
                    break;
                case "ulong":
                    unsigned = true;
                    longs++;
                    break;
                case "uint":
                    unsigned = true;
                    baseName = "int";
                    break;
                default:
                    baseName = token.Text == "uchar" ? "char" : token.Text;
                    break;
            }

            if (Peek() is not { Kind: TokenKind.Keyword, Text: "uchar" or "int" or "uint" or "long" or "ulong" or "short" or "unsigned" or "signed" })
            {
                break;
            }

            token = Next();
        }

        if (baseName == "char")
        {
            return signed ? "schar" : "uchar";
        }

        if (longs > 0)
        {
            return unsigned ? "ulong" : "long";
        }

        return unsigned ? "uint" : "int";
    }

    private (bool Const, bool Volatile) ConsumeQualifiers()
    {
        bool isConst = false;
        bool isVolatile = false;
        while (Peek() is { Kind: TokenKind.Keyword, Text: "const" or "volatile" } qualifier)
        {
            Next();
            isConst |= qualifier.Text == "const";
            isVolatile |= qualifier.Text == "volatile";
        }

        return (isConst, isVolatile);
    }

    private void StructBody(string name, int line, bool isUnion = false)
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

            string fieldSuffix = length > 0 ? DimSuffix() : string.Empty;
            if (fieldSuffix.Length > 0)
            {
                type += new string('*', stars) + fieldSuffix;
                stars = 0;
            }

            int bits = 0;
            if (Take(":"))
            {
                Token width = ExpectKind(TokenKind.Number, "bit-field width");
                if (!int.TryParse(width.Text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out bits) || bits < 1)
                {
                    throw new CParseException(width.Line, width.Column, "bit-field width must be a positive literal.");
                }
            }

            Expect(";");
            fields.Add(At(at.Line, new Ast.FieldDecl(type, stars, field, length, bits)));
        }

        _structs.Add(At(line, new Ast.StructDef(name, fields, isUnion)));
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
            if (TryArrayPointer(ref typeName, ref stars, out string globalArrayName))
            {
                globals.Add(DeclRest(typeName, globalArrayName, stars, type.Line, flags, (0, false, null), Peek()));
                Expect(";");
                continue;
            }

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
        Ast.Expr next = new Ast.Number("0");
        while (!Take("}"))
        {
            string name = ExpectKind(TokenKind.Ident, "enum constant").Text;
            if (Take("="))
            {
                Token at = Peek();
                next = Conditional();
                if (!IsConstantShape(next))
                {
                    throw new CParseException(at.Line, at.Column, "enum value must be a constant.");
                }
            }

            _enums[name] = next;
            next = TryValue(next, out int value)
                ? new Ast.Number(((value + 1) & 0xFFFF).ToString(System.Globalization.CultureInfo.InvariantCulture))
                : new Ast.Binary("+", next, new Ast.Number("1"));
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
            ConsumeQualifiers();
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

                if (!IsType(paramType))
                {
                    throw new CParseException(paramType.Line, paramType.Column, "expected parameter type.");
                }

                (string paramTypeName, int paramBase) = TypeSpec();
                int stars = paramBase + Stars();
                if (TryArrayPointer(ref paramTypeName, ref stars, out string arrayParamName))
                {
                    parameters.Add(new Ast.Param(paramTypeName, arrayParamName.Length > 0 ? arrayParamName : throw new CParseException(paramType.Line, paramType.Column, "parameter needs a name."), stars));
                    continue;
                }

                if (paramTypeName == "void" && stars == 0)
                {
                    if (parameters.Count == 0 && Peek() is { Kind: TokenKind.Punct, Text: ")" })
                    {
                        break;
                    }

                    throw new CParseException(paramType.Line, paramType.Column, "a parameter cannot be void.");
                }

                if (TryFnPtr(paramTypeName, stars, out string paramFn, out string paramFnName, out _, out _))
                {
                    parameters.Add(new Ast.Param(paramFn, paramFnName, 0));
                    continue;
                }

                string paramName = ExpectKind(TokenKind.Ident, "parameter name").Text;
                if (Take("["))
                {
                    // parametr tablicowy: pierwszy wymiar znika (wskaźnik), pozostałe wchodzą do typu elementu
                    if (!Take("]"))
                    {
                        Conditional();
                        Expect("]");
                    }

                    string rest = DimSuffix();
                    if (rest.Length > 0)
                    {
                        paramTypeName += new string('*', stars) + rest;
                        stars = 0;
                    }

                    stars++;
                }

                parameters.Add(new Ast.Param(paramTypeName, paramName, stars));
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
        if (Peek() is not { Kind: TokenKind.Keyword, Text: "struct" or "union" })
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
        if (TryArrayPointer(ref type, ref stars, out string arrayName))
        {
            decl = DeclRest(type, arrayName, stars, typeToken.Line, flags, (0, false, null), Peek());
        }
        else if (TryFnPtr(type, stars, out string fnType, out string fnName, out int fnLength, out Ast.Expr? fnLengthExpr))
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
        string suffix = length > 0 || unsized ? DimSuffix() : string.Empty;
        if (suffix.Length > 0)
        {
            type += new string('*', stars) + suffix;
            stars = 0;
        }

        return DeclRest(type, name, stars, line, flags, (length, unsized, lengthExpr), open);
    }

    private Ast.Decl DeclRest(string type, string name, int stars, int line, DeclFlags flags, (int Length, bool Unsized, Ast.Expr? Expr) array, Token open)
    {
        (int length, bool unsized, Ast.Expr? lengthExpr) = array;
        Ast.Expr? init = null;
        if (Take("="))
        {
            init = length > 0 || unsized || Peek() is { Kind: TokenKind.Punct, Text: "{" } ? ArrayInit() : Assignment();
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
            return Assignment();
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

    /// <summary>Dalsze wymiary tablicy (<c>[4][5]</c>) jako sufiks nazwy typu; wymiary muszą być stałymi literałowymi.</summary>
    private string DimSuffix()
    {
        string suffix = string.Empty;
        while (Peek() is { Kind: TokenKind.Punct, Text: "[" })
        {
            Token open = Next();
            Ast.Expr dimension = Conditional();
            Expect("]");
            if (!TryValue(dimension, out int size) || size <= 0)
            {
                throw new CParseException(open.Line, open.Column, "inner array dimension must be a positive literal.");
            }

            suffix += $"[{size}]";
        }

        return suffix;
    }

    /// <summary>Wskaźnik do tablicy: <c>(*p)[4]</c> albo bez nazwy <c>(*)[4]</c>; typ dostaje wymiary, a liczba gwiazdek rośnie.</summary>
    private bool TryArrayPointer(ref string type, ref int stars, out string name)
    {
        name = string.Empty;
        if (Peek() is not { Kind: TokenKind.Punct, Text: "(" } || Peek(1) is not { Kind: TokenKind.Punct, Text: "*" })
        {
            return false;
        }

        int nameAt = Peek(2).Kind == TokenKind.Ident ? 3 : 2;
        if (Peek(nameAt) is not { Kind: TokenKind.Punct, Text: ")" } || Peek(nameAt + 1) is not { Kind: TokenKind.Punct, Text: "[" })
        {
            return false;
        }

        Next();
        Next();
        if (nameAt == 3)
        {
            name = Next().Text;
        }

        Expect(")");
        string starText = new('*', stars);
        type += starText + DimSuffix();
        stars = 1;
        return true;
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

    /// <summary>Wyrażenie z operatorem przecinka (najniższy priorytet); argumenty i inicjalizatory używają <see cref="Assignment"/>.</summary>
    private Ast.Expr Expression()
    {
        Ast.Expr left = Assignment();
        while (Peek() is { Kind: TokenKind.Punct, Text: "," })
        {
            Next();
            Ast.Expr right = Assignment();
            var comma = new Ast.Comma(Discard(left), right);
            if (_postfix.TryGetValue(right, out Ast.Expr? update))
            {
                _postfix[comma] = new Ast.Comma(comma.Left, update);
            }

            left = comma;
        }

        return left;
    }

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
            if (!TryArrayPointer(ref castType, ref castStars, out _) && TryFnPtr(castType, castStars, out string fnType, out _, out _, out _))
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
                        arguments.Add(Assignment());
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
                string bareType = TypeQualifiers.Split(sizeType, out _, out _);
                size = bareType.StartsWith("struct ", StringComparison.Ordinal) && stars == 0
                    ? new Ast.SizeOfType(bareType, stars)
                    : new Ast.Number(stars > 0 || bareType is "int" or "uint" ? "2" : bareType is "long" or "ulong" or "float" ? "4" : "1");
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

        if (token is { Kind: TokenKind.Ident, Text: "offsetof" } && Peek(1) is { Kind: TokenKind.Punct, Text: "(" })
        {
            Next();
            Next();
            (string structType, int structBase) = TypeSpec();
            Expect(",");
            string field = ExpectKind(TokenKind.Ident, "field name").Text;
            Expect(")");
            return At(token.Line, new Ast.OffsetOf(structType, structBase, field));
        }

        if (token.Kind == TokenKind.Ident && _enums.TryGetValue(token.Text, out Ast.Expr? enumValue) && Peek(1) is not { Kind: TokenKind.Punct, Text: "(" })
        {
            Next();
            return enumValue is Ast.Number enumNumber ? new Ast.Number(enumNumber.Text) : enumValue;
        }

        if (token.Kind == TokenKind.String)
        {
            Next();
            string joined = token.Text;
            while (Peek().Kind == TokenKind.String)
            {
                joined += Next().Text;
            }

            return new Ast.Str(joined);
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
                        args.Add(Assignment());
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
