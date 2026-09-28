using System.Globalization;

namespace CathodeRay.Assembler.Syntax;

/// <summary>Ewaluator wyrażeń zgodny z dialektem: <c>| ^ &amp; &lt;&lt; &gt;&gt; + - * / %</c>, unarne <c>- + ~</c>,
/// <c>&lt;</c>/<c>&gt;</c> (młodszy/starszy bajt), operatory słowne Intel, nawiasy, znaki <c>'A'</c>, symbole i PC.
/// Zwraca <see langword="null"/>, gdy symbol nie jest jeszcze znany (pierwszy przebieg); składnię sprawdza zawsze.</summary>
internal sealed class Expression
{
    private readonly string _text;
    private readonly SyntaxDialect _dialect;
    private readonly int _pc;
    private readonly Func<string, int?> _lookup;
    private int _pos;

    private Expression(string text, SyntaxDialect dialect, int pc, Func<string, int?> lookup)
    {
        _text = text;
        _dialect = dialect;
        _pc = pc;
        _lookup = lookup;
    }

    public static int? Evaluate(string text, SyntaxDialect dialect, int pc, Func<string, int?> lookup)
    {
        var parser = new Expression(text, dialect, pc, lookup);
        int? value = parser.Binary(0);
        parser.SkipSpaces();
        return parser._pos == text.Length ? value : throw new FormatException($"unexpected '{text[parser._pos..]}' in expression '{text}'.");
    }

    public static bool IsIdentifierStart(char c) => char.IsAsciiLetter(c) || c is '_' or '?' or '@';

    public static bool IsIdentifierPart(char c) => char.IsAsciiLetterOrDigit(c) || c is '_' or '?' or '@';

    /// <summary>Czy znak pod indeksem otwiera łańcuch. Apostrof tuż po znaku identyfikatora (Z80 <c>AF'</c>) nie otwiera.</summary>
    public static bool OpensQuote(string text, int index) =>
        text[index] == '"' || (text[index] == '\'' && (index == 0 || !IsIdentifierPart(text[index - 1])));

    public static bool IsIdentifier(string text) =>
        text.Length > 0 && IsIdentifierStart(text[0]) && text.All(IsIdentifierPart);

    private static int? Apply(int? left, int? right, Func<int, int, int> operation) =>
        left is int l && right is int r ? operation(l, r) : null;

    private static int Divide(int left, int right, Func<int, int, int> operation) =>
        right != 0 ? operation(left, right) : throw new FormatException("division by zero.");

    private static bool IsDigit(char c, int radix) => radix switch
    {
        2 => c is '0' or '1',
        8 => c is >= '0' and <= '7',
        10 => char.IsAsciiDigit(c),
        _ => char.IsAsciiHexDigit(c),
    };

    private static int ParseDigits(string digits, int radix)
    {
        if (digits.Length == 0 || !digits.All(c => IsDigit(c, radix)))
        {
            throw new FormatException($"invalid base-{radix} number '{digits}'.");
        }

        long value = 0;
        foreach (char c in digits)
        {
            value = (value * radix) + int.Parse(c.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (value > int.MaxValue)
            {
                throw new FormatException($"number '{digits}' is too large.");
            }
        }

        return (int)value;
    }

    private int? Binary(int minLevel)
    {
        int? left = Unary();
        while (TryBinaryOperator(minLevel, out int level, out Func<int, int, int>? operation))
        {
            left = Apply(left, Binary(level + 1), operation);
        }

        return left;
    }

    private bool TryBinaryOperator(int minLevel, out int level, out Func<int, int, int> operation)
    {
        SkipSpaces();
        int start = _pos;
        (level, operation) = ReadBinaryOperator();
        if (level >= minLevel && level > 0)
        {
            return true;
        }

        _pos = start;
        return false;
    }

    private (int Level, Func<int, int, int> Operation) ReadBinaryOperator()
    {
        string word = PeekWord();
        if (_dialect.WordOperators && word.Length > 0)
        {
            (int, Func<int, int, int>)? op = word.ToUpperInvariant() switch
            {
                "OR" => (1, static (a, b) => a | b),
                "XOR" => (2, static (a, b) => a ^ b),
                "AND" => (3, static (a, b) => a & b),
                "SHL" => (4, static (a, b) => a << b),
                "SHR" => (4, static (a, b) => a >> b),
                "MOD" => (6, static (a, b) => Divide(a, b, static (x, y) => x % y)),
                _ => null,
            };
            if (op is { } found)
            {
                _pos += word.Length;
                return found;
            }
        }

        if (Take("<<"))
        {
            return (4, static (a, b) => a << b);
        }

        if (Take(">>"))
        {
            return (4, static (a, b) => a >> b);
        }

        return _pos < _text.Length ? _text[_pos++] switch
        {
            '|' => (1, static (a, b) => a | b),
            '^' => (2, static (a, b) => a ^ b),
            '&' => (3, static (a, b) => a & b),
            '+' => (5, static (a, b) => a + b),
            '-' => (5, static (a, b) => a - b),
            '*' => (6, static (a, b) => a * b),
            '/' => (6, static (a, b) => Divide(a, b, static (x, y) => x / y)),
            '%' => (6, static (a, b) => Divide(a, b, static (x, y) => x % y)),
            _ => (0, static (a, _) => a),
        } : (0, static (a, _) => a);
    }

    private int? Unary()
    {
        SkipSpaces();
        string word = PeekWord().ToUpperInvariant();
        if (_dialect.WordOperators && word is "NOT" or "LOW" or "HIGH")
        {
            _pos += word.Length;
            int? operand = Unary();
            return word switch
            {
                "NOT" => ~operand,
                "LOW" => operand & 0xFF,
                _ => (operand >> 8) & 0xFF,
            };
        }

        if (Take("-"))
        {
            return -Unary();
        }

        if (Take("+"))
        {
            return Unary();
        }

        if (Take("~"))
        {
            return ~Unary();
        }

        if (_dialect.LowHighPrefixes && Take("<"))
        {
            return Unary() & 0xFF;
        }

        if (_dialect.LowHighPrefixes && Take(">"))
        {
            return (Unary() >> 8) & 0xFF;
        }

        return Primary();
    }

    private int? Primary()
    {
        SkipSpaces();
        if (_pos >= _text.Length)
        {
            throw new FormatException($"missing operand in expression '{_text}'.");
        }

        char c = _text[_pos];
        if (Take("("))
        {
            int? inner = Binary(0);
            SkipSpaces();
            return Take(")") ? inner : throw new FormatException($"missing ')' in expression '{_text}'.");
        }

        if (c == '\'' && _pos + 2 < _text.Length && _text[_pos + 2] == '\'')
        {
            _pos += 3;
            return _text[_pos - 2];
        }

        if (_dialect.Numbers.HasFlag(NumberFormats.Motorola) && (c == '$' || c == '%') && _pos + 1 < _text.Length && IsDigit(_text[_pos + 1], c == '$' ? 16 : 2))
        {
            _pos++;
            return ParseDigits(ReadWhile(char.IsAsciiLetterOrDigit), c == '$' ? 16 : 2);
        }

        if (c == _dialect.ProgramCounter)
        {
            _pos++;
            return _pc;
        }

        if (char.IsAsciiDigit(c))
        {
            return Number(ReadWhile(char.IsAsciiLetterOrDigit));
        }

        if (IsIdentifierStart(c))
        {
            return _lookup(ReadWhile(IsIdentifierPart));
        }

        throw new FormatException($"unexpected '{c}' in expression '{_text}'.");
    }

    private int Number(string token)
    {
        bool intel = _dialect.Numbers.HasFlag(NumberFormats.Intel);
        if (intel && char.ToUpperInvariant(token[^1]) == 'H')
        {
            return ParseDigits(token[..^1], 16);
        }

        if (_dialect.Numbers.HasFlag(NumberFormats.CStyle) && token.Length > 2 && token[0] == '0' && (token[1] | 0x20) is 'x' or 'b')
        {
            return ParseDigits(token[2..], (token[1] | 0x20) == 'x' ? 16 : 2);
        }

        if (intel && !token.All(char.IsAsciiDigit))
        {
            return char.ToUpperInvariant(token[^1]) switch
            {
                'B' => ParseDigits(token[..^1], 2),
                'Q' or 'O' => ParseDigits(token[..^1], 8),
                'D' => ParseDigits(token[..^1], 10),
                _ => throw new FormatException($"invalid number '{token}'."),
            };
        }

        return ParseDigits(token, 10);
    }

    private string PeekWord()
    {
        int end = _pos;
        while (end < _text.Length && char.IsAsciiLetter(_text[end]))
        {
            end++;
        }

        bool boundary = end == _text.Length || !IsIdentifierPart(_text[end]);
        return boundary ? _text[_pos..end] : string.Empty;
    }

    private string ReadWhile(Func<char, bool> predicate)
    {
        int start = _pos;
        while (_pos < _text.Length && predicate(_text[_pos]))
        {
            _pos++;
        }

        return _text[start.._pos];
    }

    private bool Take(string token)
    {
        SkipSpaces();
        if (string.CompareOrdinal(_text, _pos, token, 0, token.Length) != 0)
        {
            return false;
        }

        _pos += token.Length;
        return true;
    }

    private void SkipSpaces()
    {
        while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
        {
            _pos++;
        }
    }
}
