using System.Globalization;
using System.Text;

namespace CathodeRay.C;

/// <summary>Preprocesor mini-C: <c>#include "f"</c> / <c>&lt;f&gt;</c>, <c>#define</c> (obiektowe i funkcyjne),
/// <c>#undef</c>, <c>#if/#ifdef/#ifndef/#elif/#else/#endif</c>, <c>#error</c>, <c>#pragma once</c>.
/// Podstawianie jest tokenowe (napisy, znaki i komentarze zostają), makro nie rozwija się w samym sobie.
/// Dyrektywy zostawiają puste linie (numeracja linii bez zmian poza splicem <c>#include</c>).</summary>
public static class CPreprocessor
{
    private const int MaxExpansionDepth = 64;

    /// <summary>Przetwarza źródło.</summary>
    /// <param name="source">Tekst programu w C.</param>
    /// <param name="reader">Czyta plik (dla <c>&lt;f&gt;</c> nazwa z nawiasami kątowymi; null = brak).</param>
    /// <param name="defines">Makra z linii poleceń (nazwa → wartość).</param>
    /// <returns>Czysty tekst C.</returns>
    /// <exception cref="CPreprocessException">Błąd dyrektywy, brak pliku, cykl, <c>#error</c>.</exception>
    public static string Process(string source, Func<string, string?>? reader, IReadOnlyDictionary<string, string>? defines = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var state = new State(reader);
        foreach ((string name, string value) in defines ?? new Dictionary<string, string>())
        {
            state.Macros[name] = new Macro(null, value);
        }

        var output = new List<string>();
        RunFile(source, state, [], null, output);
        return string.Join('\n', output);
    }

    private static void RunFile(string source, State state, List<string> stack, string? file, List<string> output)
    {
        string[] lines = source.Split('\n');
        var conditions = new Stack<Condition>();
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            int joined = 0;
            while (line.EndsWith('\\') && i + 1 < lines.Length)
            {
                line = line[..^1] + lines[++i].TrimEnd('\r');
                joined++;
            }

            bool active = conditions.Count == 0 || conditions.Peek().Active;
            string trimmed = line.TrimStart();
            if (!state.InBlockComment && trimmed.StartsWith('#'))
            {
                Directive(trimmed[1..].TrimStart(), i + 1, state, stack, file, conditions, active, output);
                output.Add(string.Empty);
            }
            else
            {
                output.Add(active ? Expand(line, state, [], 0, i + 1) : string.Empty);
            }

            for (int j = 0; j < joined; j++)
            {
                output.Add(string.Empty);
            }
        }

        if (conditions.Count > 0)
        {
            throw new CPreprocessException(lines.Length, "unterminated #if.");
        }
    }

    private static void Directive(string text, int line, State state, List<string> stack, string? file, Stack<Condition> conditions, bool active, List<string> output)
    {
        int end = 0;
        while (end < text.Length && char.IsAsciiLetter(text[end]))
        {
            end++;
        }

        string name = text[..end];
        string rest = StripComment(text[end..]).Trim();
        switch (name)
        {
            case "if":
            case "ifdef":
            case "ifndef":
            {
                bool value = active && name switch
                {
                    "ifdef" => state.Macros.ContainsKey(rest),
                    "ifndef" => !state.Macros.ContainsKey(rest),
                    _ => Evaluate(rest, state, line) != 0,
                };
                conditions.Push(new Condition(active, value, value, false));
                return;
            }

            case "elif":
            {
                Condition top = conditions.Count > 0 ? conditions.Pop() : throw new CPreprocessException(line, "#elif without #if.");
                bool take = top.ParentActive && !top.Taken && Evaluate(rest, state, line) != 0;
                conditions.Push(top with { Active = take, Taken = top.Taken || take });
                return;
            }

            case "else":
            {
                Condition top = conditions.Count > 0 ? conditions.Pop() : throw new CPreprocessException(line, "#else without #if.");
                if (top.SawElse)
                {
                    throw new CPreprocessException(line, "duplicate #else.");
                }

                conditions.Push(top with { Active = top.ParentActive && !top.Taken, Taken = true, SawElse = true });
                return;
            }

            case "endif":
                if (conditions.Count == 0)
                {
                    throw new CPreprocessException(line, "#endif without #if.");
                }

                conditions.Pop();
                return;
        }

        if (!active)
        {
            return;
        }

        switch (name)
        {
            case "include":
                Include(rest, line, state, stack, output);
                return;
            case "define":
                Define(rest, line, state);
                return;
            case "undef":
                state.Macros.Remove(rest);
                return;
            case "error":
                throw new CPreprocessException(line, $"#error {rest}");
            case "pragma":
                if (rest == "once" && file is not null)
                {
                    state.Once.Add(file);
                }

                return;
            default:
                throw new CPreprocessException(line, $"unknown directive '#{text}'.");
        }
    }

    private static void Include(string rest, int line, State state, List<string> stack, List<string> output)
    {
        string name;
        if (rest.Length >= 2 && rest[0] == '"' && rest[^1] == '"')
        {
            name = rest[1..^1];
        }
        else if (rest.Length >= 2 && rest[0] == '<' && rest[^1] == '>')
        {
            name = rest;
        }
        else
        {
            throw new CPreprocessException(line, "#include needs \"file\" or <file>.");
        }

        if (state.Reader is null)
        {
            throw new CPreprocessException(line, "#include needs a file reader.");
        }

        if (stack.Contains(name, StringComparer.Ordinal))
        {
            throw new CPreprocessException(line, $"include cycle '{name}'.");
        }

        if (state.Once.Contains(name))
        {
            return;
        }

        string content = state.Reader(name) ?? throw new CPreprocessException(line, $"include file '{name}' not found.");
        stack.Add(name);
        RunFile(content, state, stack, name, output);
        stack.RemoveAt(stack.Count - 1);
    }

    private static void Define(string rest, int line, State state)
    {
        int end = 0;
        while (end < rest.Length && (char.IsAsciiLetterOrDigit(rest[end]) || rest[end] == '_'))
        {
            end++;
        }

        string name = rest[..end];
        if (name.Length == 0 || char.IsAsciiDigit(name[0]))
        {
            throw new CPreprocessException(line, $"#define needs a name, got '{rest}'.");
        }

        if (Lexer.IsKeyword(name))
        {
            throw new CPreprocessException(line, $"macro name '{name}' is a keyword.");
        }

        string[]? parameters = null;
        if (end < rest.Length && rest[end] == '(')
        {
            int close = rest.IndexOf(')', end);
            if (close < 0)
            {
                throw new CPreprocessException(line, $"#define '{name}': missing ')'.");
            }

            parameters = [.. rest[(end + 1)..close].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
            end = close + 1;
        }

        var macro = new Macro(parameters, rest[end..].Trim());
        if (state.Macros.TryGetValue(name, out Macro? existing) && !existing.Equals(macro))
        {
            throw new CPreprocessException(line, $"macro '{name}' redefined differently.");
        }

        state.Macros[name] = macro;
    }

    private static string StripComment(string text)
    {
        int at = text.IndexOf("//", StringComparison.Ordinal);
        return at < 0 ? text : text[..at];
    }

    /// <summary>Rozwija makra w tekście; napisy, znaki i komentarze zostają. <paramref name="hidden"/> to makra
    /// będące w trakcie rozwijania (nie rozwijają się ponownie).</summary>
    private static string Expand(string text, State state, HashSet<string> hidden, int depth, int line)
    {
        if (depth > MaxExpansionDepth)
        {
            throw new CPreprocessException(line, "macro expansion too deep.");
        }

        var result = new StringBuilder();
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (state.InBlockComment)
            {
                int close = text.IndexOf("*/", i, StringComparison.Ordinal);
                if (close < 0)
                {
                    result.Append(text, i, text.Length - i);
                    return result.ToString();
                }

                result.Append(text, i, close + 2 - i);
                i = close + 2;
                state.InBlockComment = false;
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                result.Append(text, i, text.Length - i);
                return result.ToString();
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                state.InBlockComment = true;
                result.Append("/*");
                i += 2;
                continue;
            }

            if (c is '"' or '\'')
            {
                int stop = SkipLiteral(text, i);
                result.Append(text, i, stop - i);
                i = stop;
                continue;
            }

            if (char.IsAsciiLetter(c) || c == '_')
            {
                int stop = i;
                while (stop < text.Length && (char.IsAsciiLetterOrDigit(text[stop]) || text[stop] == '_'))
                {
                    stop++;
                }

                string ident = text[i..stop];
                if (!state.Macros.TryGetValue(ident, out Macro? macro) || hidden.Contains(ident))
                {
                    result.Append(ident);
                    i = stop;
                    continue;
                }

                var inner = new HashSet<string>(hidden) { ident };
                if (macro.Parameters is null)
                {
                    result.Append(Expand(macro.Body, state, inner, depth + 1, line));
                    i = stop;
                    continue;
                }

                int open = stop;
                while (open < text.Length && char.IsWhiteSpace(text[open]))
                {
                    open++;
                }

                if (open >= text.Length || text[open] != '(')
                {
                    result.Append(ident);
                    i = stop;
                    continue;
                }

                List<string> args = ReadArguments(text, open, out int after, line, ident);
                if (args.Count == 1 && args[0].Length == 0 && macro.Parameters.Length == 0)
                {
                    args.Clear();
                }

                if (args.Count != macro.Parameters.Length)
                {
                    throw new CPreprocessException(line, $"macro '{ident}' takes {macro.Parameters.Length} arguments, got {args.Count}.");
                }

                string[] expandedArgs = [.. args.Select(arg => Expand(arg, state, hidden, depth + 1, line))];
                string body = Substitute(macro.Body, macro.Parameters, expandedArgs, [.. args]);
                result.Append(Expand(body, state, inner, depth + 1, line));
                i = after;
                continue;
            }

            result.Append(c);
            i++;
        }

        return result.ToString();
    }

    private static List<string> ReadArguments(string text, int open, out int after, int line, string macro)
    {
        var args = new List<string>();
        int depth = 0;
        int start = open + 1;
        int i = open;
        while (i < text.Length)
        {
            char c = text[i];
            if (c is '"' or '\'')
            {
                i = SkipLiteral(text, i);
                continue;
            }

            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    args.Add(text[start..i].Trim());
                    after = i + 1;
                    return args;
                }
            }
            else if (c == ',' && depth == 1)
            {
                args.Add(text[start..i].Trim());
                start = i + 1;
            }

            i++;
        }

        throw new CPreprocessException(line, $"macro '{macro}': unterminated argument list.");
    }

    /// <summary>Podstawia argumenty za parametry. Argument obok <c>#</c> albo <c>##</c> jest surowy (bez rozwijania makr):
    /// <c>#p</c> daje napis z tekstem argumentu, <c>a ## b</c> skleja sąsiednie tokeny.</summary>
    private static string Substitute(string body, string[] parameters, string[] args, string[] rawArgs)
    {
        var result = new StringBuilder();
        int i = 0;
        while (i < body.Length)
        {
            char c = body[i];
            if (c is '"' or '\'')
            {
                int stop = SkipLiteral(body, i);
                result.Append(body, i, stop - i);
                i = stop;
                continue;
            }

            if (c == '#' && i + 1 < body.Length && body[i + 1] == '#')
            {
                // sklejanie: bez białych znaków po obu stronach, następny parametr surowy
                while (result.Length > 0 && char.IsWhiteSpace(result[^1]))
                {
                    result.Length--;
                }

                i += 2;
                while (i < body.Length && char.IsWhiteSpace(body[i]))
                {
                    i++;
                }

                if (i < body.Length && (char.IsAsciiLetter(body[i]) || body[i] == '_'))
                {
                    int stop = i;
                    while (stop < body.Length && (char.IsAsciiLetterOrDigit(body[stop]) || body[stop] == '_'))
                    {
                        stop++;
                    }

                    string pasted = body[i..stop];
                    int pastedIndex = Array.IndexOf(parameters, pasted);
                    result.Append(pastedIndex >= 0 ? rawArgs[pastedIndex] : pasted);
                    i = stop;
                }

                continue;
            }

            if (c == '#')
            {
                int at = i + 1;
                while (at < body.Length && char.IsWhiteSpace(body[at]))
                {
                    at++;
                }

                int stop = at;
                while (stop < body.Length && (char.IsAsciiLetterOrDigit(body[stop]) || body[stop] == '_'))
                {
                    stop++;
                }

                int paramIndex = stop > at ? Array.IndexOf(parameters, body[at..stop]) : -1;
                if (paramIndex >= 0)
                {
                    result.Append(Stringize(rawArgs[paramIndex]));
                    i = stop;
                    continue;
                }
            }

            if (char.IsAsciiLetter(c) || c == '_')
            {
                int stop = i;
                while (stop < body.Length && (char.IsAsciiLetterOrDigit(body[stop]) || body[stop] == '_'))
                {
                    stop++;
                }

                string ident = body[i..stop];
                int index = Array.IndexOf(parameters, ident);
                int after = stop;
                while (after < body.Length && char.IsWhiteSpace(body[after]))
                {
                    after++;
                }

                bool beforePaste = after + 1 < body.Length && body[after] == '#' && body[after + 1] == '#';
                result.Append(index >= 0 ? (beforePaste ? rawArgs[index] : args[index]) : ident);
                i = stop;
                continue;
            }

            result.Append(c);
            i++;
        }

        return result.ToString();
    }

    /// <summary>Tekst argumentu jako literał napisowy (spacje zwinięte, <c>"</c> i <c>\</c> w literałach poprzedzone <c>\</c>).</summary>
    private static string Stringize(string argument)
    {
        var text = new StringBuilder("\"");
        int i = 0;
        bool space = false;
        while (i < argument.Length)
        {
            char c = argument[i];
            if (char.IsWhiteSpace(c))
            {
                space = text.Length > 1;
                i++;
                continue;
            }

            if (space)
            {
                text.Append(' ');
                space = false;
            }

            if (c is '"' or '\'')
            {
                int stop = SkipLiteral(argument, i);
                foreach (char ch in argument[i..stop])
                {
                    if (ch is '"' or '\\')
                    {
                        text.Append('\\');
                    }

                    text.Append(ch);
                }

                i = stop;
                continue;
            }

            text.Append(c);
            i++;
        }

        return text.Append('"').ToString();
    }

    private static int SkipLiteral(string text, int start)
    {
        char quote = text[start];
        int i = start + 1;
        while (i < text.Length)
        {
            if (text[i] == '\\')
            {
                i += 2;
                continue;
            }

            if (text[i] == quote)
            {
                return i + 1;
            }

            i++;
        }

        return text.Length;
    }

    /// <summary>Warunek <c>#if</c>: <c>defined</c>, makra, potem stałe całkowite (nieznane nazwy = 0).</summary>
    private static long Evaluate(string expression, State state, int line)
    {
        var withDefined = new StringBuilder();
        int i = 0;
        while (i < expression.Length)
        {
            if (char.IsAsciiLetter(expression[i]) || expression[i] == '_')
            {
                int stop = i;
                while (stop < expression.Length && (char.IsAsciiLetterOrDigit(expression[stop]) || expression[stop] == '_'))
                {
                    stop++;
                }

                string ident = expression[i..stop];
                if (ident == "defined")
                {
                    int at = stop;
                    while (at < expression.Length && char.IsWhiteSpace(expression[at]))
                    {
                        at++;
                    }

                    bool paren = at < expression.Length && expression[at] == '(';
                    if (paren)
                    {
                        at++;
                    }

                    while (at < expression.Length && char.IsWhiteSpace(expression[at]))
                    {
                        at++;
                    }

                    int nameStart = at;
                    while (at < expression.Length && (char.IsAsciiLetterOrDigit(expression[at]) || expression[at] == '_'))
                    {
                        at++;
                    }

                    string target = expression[nameStart..at];
                    while (paren && at < expression.Length && expression[at] != ')')
                    {
                        at++;
                    }

                    withDefined.Append(state.Macros.ContainsKey(target) ? '1' : '0');
                    i = paren ? at + 1 : at;
                    continue;
                }

                withDefined.Append(ident);
                i = stop;
                continue;
            }

            withDefined.Append(expression[i]);
            i++;
        }

        string expanded = Expand(withDefined.ToString(), state, [], 0, line);
        var parser = new ConditionParser(expanded, line);
        return parser.Parse();
    }

    private sealed record Macro(string[]? Parameters, string Body)
    {
        public bool Equals(Macro? other) =>
            other is not null && Body == other.Body && (Parameters is null ? other.Parameters is null : other.Parameters is not null && Parameters.SequenceEqual(other.Parameters));

        public override int GetHashCode() => Body.GetHashCode(StringComparison.Ordinal);
    }

    private sealed record Condition(bool ParentActive, bool Active, bool Taken, bool SawElse);

    private sealed class State(Func<string, string?>? reader)
    {
        public Func<string, string?>? Reader { get; } = reader;

        public Dictionary<string, Macro> Macros { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Once { get; } = new(StringComparer.Ordinal);

        public bool InBlockComment { get; set; }
    }

    private sealed class ConditionParser(string text, int line)
    {
        private int _pos;

        public long Parse()
        {
            long value = Ternary();
            Skip();
            return _pos < text.Length ? throw new CPreprocessException(line, $"bad #if expression near '{text[_pos..]}'.") : value;
        }

        private void Skip()
        {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos]))
            {
                _pos++;
            }
        }

        private bool Take(string op)
        {
            Skip();
            if (string.CompareOrdinal(text, _pos, op, 0, op.Length) != 0)
            {
                return false;
            }

            // "&" nie zjada "&&", "|" nie zjada "||", "<" nie zjada "<<" ani "<="
            char next = _pos + op.Length < text.Length ? text[_pos + op.Length] : '\0';
            if ((op == "&" && next == '&') || (op == "|" && next == '|') || (op is "<" or ">" && (next == op[0] || next == '=')))
            {
                return false;
            }

            _pos += op.Length;
            return true;
        }

        private long Ternary()
        {
            long condition = LogicalOr();
            if (!Take("?"))
            {
                return condition;
            }

            long then = Ternary();
            if (!Take(":"))
            {
                throw new CPreprocessException(line, "#if: expected ':'.");
            }

            long otherwise = Ternary();
            return condition != 0 ? then : otherwise;
        }

        private long LogicalOr()
        {
            long left = LogicalAnd();
            while (Take("||"))
            {
                long right = LogicalAnd();
                left = left != 0 || right != 0 ? 1 : 0;
            }

            return left;
        }

        private long LogicalAnd()
        {
            long left = BitOr();
            while (Take("&&"))
            {
                long right = BitOr();
                left = left != 0 && right != 0 ? 1 : 0;
            }

            return left;
        }

        private long BitOr()
        {
            long left = BitXor();
            while (Take("|"))
            {
                left |= BitXor();
            }

            return left;
        }

        private long BitXor()
        {
            long left = BitAnd();
            while (Take("^"))
            {
                left ^= BitAnd();
            }

            return left;
        }

        private long BitAnd()
        {
            long left = Equality();
            while (Take("&"))
            {
                left &= Equality();
            }

            return left;
        }

        private long Equality()
        {
            long left = Relational();
            while (true)
            {
                if (Take("=="))
                {
                    left = left == Relational() ? 1 : 0;
                }
                else if (Take("!="))
                {
                    left = left != Relational() ? 1 : 0;
                }
                else
                {
                    return left;
                }
            }
        }

        private long Relational()
        {
            long left = Shift();
            while (true)
            {
                if (Take("<="))
                {
                    left = left <= Shift() ? 1 : 0;
                }
                else if (Take(">="))
                {
                    left = left >= Shift() ? 1 : 0;
                }
                else if (Take("<"))
                {
                    left = left < Shift() ? 1 : 0;
                }
                else if (Take(">"))
                {
                    left = left > Shift() ? 1 : 0;
                }
                else
                {
                    return left;
                }
            }
        }

        private long Shift()
        {
            long left = Additive();
            while (true)
            {
                if (Take("<<"))
                {
                    left <<= (int)Additive();
                }
                else if (Take(">>"))
                {
                    left >>= (int)Additive();
                }
                else
                {
                    return left;
                }
            }
        }

        private long Additive()
        {
            long left = Multiplicative();
            while (true)
            {
                if (Take("+"))
                {
                    left += Multiplicative();
                }
                else if (Take("-"))
                {
                    left -= Multiplicative();
                }
                else
                {
                    return left;
                }
            }
        }

        private long Multiplicative()
        {
            long left = Unary();
            while (true)
            {
                if (Take("*"))
                {
                    left *= Unary();
                }
                else if (Take("/"))
                {
                    long right = Unary();
                    left = right == 0 ? throw new CPreprocessException(line, "#if: division by zero.") : left / right;
                }
                else if (Take("%"))
                {
                    long right = Unary();
                    left = right == 0 ? throw new CPreprocessException(line, "#if: division by zero.") : left % right;
                }
                else
                {
                    return left;
                }
            }
        }

        private long Unary()
        {
            if (Take("!"))
            {
                return Unary() == 0 ? 1 : 0;
            }

            if (Take("~"))
            {
                return ~Unary();
            }

            if (Take("-"))
            {
                return -Unary();
            }

            if (Take("+"))
            {
                return Unary();
            }

            if (Take("("))
            {
                long inner = Ternary();
                return Take(")") ? inner : throw new CPreprocessException(line, "#if: expected ')'.");
            }

            Skip();
            int start = _pos;
            if (_pos < text.Length && text[_pos] == '\'' && _pos + 2 < text.Length && text[_pos + 2] == '\'')
            {
                _pos += 3;
                return text[start + 1];
            }

            while (_pos < text.Length && (char.IsAsciiLetterOrDigit(text[_pos]) || text[_pos] == '_'))
            {
                _pos++;
            }

            string token = text[start.._pos];
            if (token.Length == 0)
            {
                throw new CPreprocessException(line, $"bad #if expression near '{text[start..]}'.");
            }

            if (char.IsAsciiDigit(token[0]))
            {
                bool hex = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
                return long.TryParse(hex ? token[2..] : token, hex ? NumberStyles.HexNumber : NumberStyles.None, CultureInfo.InvariantCulture, out long number)
                    ? number
                    : throw new CPreprocessException(line, $"#if: bad number '{token}'.");
            }

            return 0;
        }
    }
}
