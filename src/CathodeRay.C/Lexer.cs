using System.Text;

namespace CathodeRay.C;

/// <summary>Lekser mini-C: identyfikatory, liczby dec/<c>0x</c>, słowa kluczowe,
/// operatory (zachłannie, najdłuższy pasuje), komentarze <c>//</c> i <c>/* */</c>.</summary>
public static class Lexer
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "uchar", "int", "void", "if", "else", "while", "for", "return", "break", "continue",
    };

    private static readonly string[] Operators =
    [
        "<<=", ">>=", "==", "!=", "<=", ">=", "&&", "||", "<<", ">>", "++", "--",
        "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=",
        "+", "-", "*", "/", "%", "<", ">", "=", "!", "~", "&", "|", "^",
        "(", ")", "{", "}", "[", "]", ";", ",", "?", ":",
    ];

    /// <summary>Tokenizuje źródło.</summary>
    /// <param name="source">Tekst programu.</param>
    /// <returns>Tokeny z końcowym <c>End</c>.</returns>
    /// <exception cref="CParseException">Zły znak lub niezamknięty komentarz/stała.</exception>
    public static IReadOnlyList<Token> Tokenize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var tokens = new List<Token>();
        int pos = 0, line = 1, col = 1;
        while (pos < source.Length)
        {
            char c = source[pos];
            if (c == '\n')
            {
                pos++;
                line++;
                col = 1;
            }
            else if (char.IsWhiteSpace(c))
            {
                pos++;
                col++;
            }
            else if (c == '/' && pos + 1 < source.Length && source[pos + 1] == '/')
            {
                while (pos < source.Length && source[pos] != '\n')
                {
                    pos++;
                    col++;
                }
            }
            else if (c == '/' && pos + 1 < source.Length && source[pos + 1] == '*')
            {
                int startLine = line, startCol = col;
                pos += 2;
                col += 2;
                bool closed = false;
                while (pos < source.Length)
                {
                    if (source[pos] == '*' && pos + 1 < source.Length && source[pos + 1] == '/')
                    {
                        pos += 2;
                        col += 2;
                        closed = true;
                        break;
                    }

                    if (source[pos] == '\n')
                    {
                        line++;
                        col = 1;
                    }
                    else
                    {
                        col++;
                    }

                    pos++;
                }

                if (!closed)
                {
                    throw new CParseException(startLine, startCol, "unterminated comment.");
                }
            }
            else if (char.IsAsciiLetter(c) || c == '_')
            {
                int start = pos, startCol = col;
                while (pos < source.Length && (char.IsAsciiLetterOrDigit(source[pos]) || source[pos] == '_'))
                {
                    pos++;
                    col++;
                }

                string text = source[start..pos];
                tokens.Add(new Token(Keywords.Contains(text) ? TokenKind.Keyword : TokenKind.Ident, text, line, startCol));
            }
            else if (char.IsAsciiDigit(c))
            {
                int start = pos, startCol = col;
                while (pos < source.Length && (char.IsAsciiLetterOrDigit(source[pos]) || source[pos] == '_'))
                {
                    pos++;
                    col++;
                }

                tokens.Add(new Token(TokenKind.Number, source[start..pos], line, startCol));
            }
            else if (c == '\'' || c == '"')
            {
                int startCol = col;
                pos++;
                col++;
                var text = new StringBuilder();
                while (true)
                {
                    if (pos >= source.Length || source[pos] == '\n')
                    {
                        throw new CParseException(line, startCol, "unterminated literal.");
                    }

                    char ch = source[pos++];
                    col++;
                    if (ch == c)
                    {
                        break;
                    }

                    if (ch == '\\' && pos < source.Length)
                    {
                        char esc = source[pos++];
                        col++;
                        ch = esc switch
                        {
                            'n' => '\n',
                            't' => '\t',
                            'r' => '\r',
                            '0' => '\0',
                            _ => esc,
                        };
                    }

                    text.Append(ch);
                }

                if (c == '"')
                {
                    tokens.Add(new Token(TokenKind.String, text.ToString(), line, startCol));
                }
                else if (text.Length == 1)
                {
                    tokens.Add(new Token(TokenKind.Number, ((int)text[0]).ToString(System.Globalization.CultureInfo.InvariantCulture), line, startCol));
                }
                else
                {
                    throw new CParseException(line, startCol, "char literal needs exactly one character.");
                }
            }
            else
            {
                string? op = Operators.FirstOrDefault(o => source.AsSpan(pos).StartsWith(o, StringComparison.Ordinal));
                if (op is null)
                {
                    throw new CParseException(line, col, $"unexpected character '{c}'.");
                }

                tokens.Add(new Token(TokenKind.Punct, op, line, col));
                pos += op.Length;
                col += op.Length;
            }
        }

        tokens.Add(new Token(TokenKind.End, string.Empty, line, col));
        return tokens;
    }

    /// <summary>Renderuje tokeny do testów snapshot (po jednym w linii).</summary>
    /// <param name="tokens">Tokeny.</param>
    /// <returns>Tekst do porównania.</returns>
    public static string Dump(IReadOnlyList<Token> tokens)
    {
        var text = new StringBuilder();
        foreach (Token token in tokens)
        {
            text.Append(token.Kind).Append(':').Append(token.Text)
                .Append('@').Append(token.Line).Append(':').Append(token.Column).Append('\n');
        }

        return text.ToString();
    }
}
