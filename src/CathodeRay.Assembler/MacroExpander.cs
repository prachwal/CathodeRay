using System.Text.RegularExpressions;
using CathodeRay.Assembler.Directives;
using CathodeRay.Assembler.Syntax;

namespace CathodeRay.Assembler;

/// <summary>Przedprzebiegowa ekspansja makr (po includach, przed pass 1): zbiera definicje
/// (<c>.macro</c>/<c>MACRO</c>), usuwa je ze strumienia i wstawia rozwinięcia w miejscu wywołań
/// z podstawieniem tokenowym parametrów i unikalizacją <c>.local</c>/<c>LOCAL</c>.
/// Definicje są bezwarunkowe (otaczający <c>.if</c> ich nie wyłącza — odstępstwo udokumentowane).</summary>
internal static partial class MacroExpander
{
    private const int MaxDepth = 32;

    /// <summary>Rozwija makra w liniach.</summary>
    /// <param name="lines">Linie po ekspansji includów.</param>
    /// <param name="dialect">Dialekt (nazwy makr w wywołaniach liczone jak słowa kluczowe).</param>
    /// <param name="isKeyword">Czy słowo jest mnemonikiem/dyrektywą.</param>
    /// <returns>Linie bez definicji, z rozwinięciami.</returns>
    public static IReadOnlyList<SourceLine> Expand(
        IReadOnlyList<SourceLine> lines, SyntaxDialect dialect, Func<string, bool> isKeyword)
    {
        var names = CollectNames(lines, dialect);
        Func<string, bool> keywords = word => isKeyword(word) || names.Contains(word);
        var reparsed = lines
            .Select(line => LineParser.Parse(line.Number, line.Text, dialect, keywords, line.File))
            .ToList();
        var (macros, code) = CollectDefinitions(reparsed, dialect);
        int counter = 0;
        return ExpandCalls(code, macros, dialect, ref counter, depth: 0, stack: []);
    }

    private static HashSet<string> CollectNames(IReadOnlyList<SourceLine> lines, SyntaxDialect dialect)
    {
        var names = new HashSet<string>(dialect.SymbolComparer);
        foreach (SourceLine line in lines)
        {
            if (IsKind(line, dialect, MacroKind.Macro) && MacroName(line) is { } name)
            {
                names.Add(name);
            }
        }

        return names;
    }

    private static (Dictionary<string, MacroDefinition> Macros, List<SourceLine> Code) CollectDefinitions(IReadOnlyList<SourceLine> lines, SyntaxDialect dialect)
    {
        var macros = new Dictionary<string, MacroDefinition>(dialect.SymbolComparer);
        var code = new List<SourceLine>();
        for (int i = 0; i < lines.Count; i++)
        {
            SourceLine line = lines[i];
            if (!IsKind(line, dialect, MacroKind.Macro))
            {
                if (IsKind(line, dialect, MacroKind.EndMacro))
                {
                    throw At(line, "'.endmacro' without '.macro'.");
                }

                if (IsKind(line, dialect, MacroKind.Local))
                {
                    throw At(line, "'.local' outside macro definition.");
                }

                code.Add(line);
                continue;
            }

            if (line.Label is not null && !IsLabelForm(line))
            {
                throw At(line, "label on '.macro' is not allowed.");
            }

            string name = MacroName(line) ?? throw At(line, "'.macro' needs a name.");
            if (!Expression.IsIdentifier(name))
            {
                throw At(line, $"invalid macro name '{name}'.");
            }

            (string[] parameters, Dictionary<string, string> defaults) = MacroParams(line, name);
            var body = new List<SourceLine>();
            i++;
            for (; i < lines.Count; i++)
            {
                SourceLine inner = lines[i];
                if (IsKind(inner, dialect, MacroKind.Macro))
                {
                    throw At(inner, "nested '.macro' is not allowed.");
                }

                if (IsKind(inner, dialect, MacroKind.EndMacro))
                {
                    break;
                }

                body.Add(inner);
            }

            if (i >= lines.Count)
            {
                throw At(line, $"unterminated '.macro' (opened here).");
            }

            if (macros.ContainsKey(name))
            {
                throw At(line, $"macro '{name}' is already defined.");
            }

            macros[name] = new MacroDefinition(name, parameters, defaults, body, line);
        }

        return (macros, code);
    }

    private static IReadOnlyList<SourceLine> ExpandCalls(
        IReadOnlyList<SourceLine> lines,
        Dictionary<string, MacroDefinition> macros,
        SyntaxDialect dialect,
        ref int counter,
        int depth,
        List<string> stack)
    {
        var output = new List<SourceLine>();
        foreach (SourceLine line in lines)
        {
            if (line.Keyword is null || !macros.TryGetValue(line.Keyword, out MacroDefinition? macro))
            {
                output.Add(line);
                continue;
            }

            if (depth >= MaxDepth)
            {
                throw At(line, $"macro recursion too deep (>{MaxDepth}): {string.Join(" -> ", stack)} -> {macro.Name}.");
            }

            string[] args = SplitArgs(line.Operand);
            if (args.Length > macro.Params.Length)
            {
                throw At(line, $"too many macro parameters for '{macro.Name}' (takes {macro.Params.Length}, got {args.Length}).");
            }

            while (args.Length < macro.Params.Length)
            {
                args = [.. args, string.Empty];
            }

            counter++;
            var map = new Dictionary<string, string>(dialect.SymbolComparer);
            for (int p = 0; p < macro.Params.Length; p++)
            {
                string provided = p < args.Length ? args[p] : string.Empty;
                map[macro.Params[p]] = provided.Length > 0 ? provided
                    : macro.Defaults.TryGetValue(macro.Params[p], out string? fallback) ? fallback : string.Empty;
            }

            var locals = new List<string>();
            foreach (SourceLine bodyLine in macro.Body)
            {
                if (IsKind(bodyLine, dialect, MacroKind.Local))
                {
                    locals.AddRange(SplitArgs(bodyLine.Operand).Where(static a => a.Length > 0));
                }
            }

            foreach (string local in locals)
            {
                if (map.ContainsKey(local))
                {
                    throw At(line, $"macro '{macro.Name}': '{local}' is both a parameter and a .local.");
                }

                map[local] = $"__M{counter}_{local}";
            }

            var expanded = new List<SourceLine>();
            foreach (SourceLine bodyLine in macro.Body)
            {
                if (IsKind(bodyLine, dialect, MacroKind.Local))
                {
                    continue;
                }

                expanded.Add(Substitute(bodyLine, line, map, macro));
            }

            if (line.Label is not null)
            {
                output.Add(new SourceLine(line.Number, $"{line.Label}:", line.Label, null, null, line.File, new MacroUse(macro.Name, macro.DefinedAt.File, macro.DefinedAt.Number)));
            }

            stack.Add(macro.Name);
            output.AddRange(ExpandCalls(expanded, macros, dialect, ref counter, depth + 1, stack));
            stack.RemoveAt(stack.Count - 1);
        }

        return output;
    }

    private static SourceLine Substitute(SourceLine body, SourceLine call, Dictionary<string, string> map, MacroDefinition macro)
    {
        string? label = body.Label is null ? null : ReplaceAll(body.Label, map);
        string? keyword = body.Keyword is null ? null : ReplaceAll(body.Keyword, map);
        string? operand = body.Operand is null ? null : ReplaceAll(body.Operand, map);
        string text = (label is null ? string.Empty : label + ": ") + (keyword ?? string.Empty)
            + (operand is null ? string.Empty : " " + operand);
        return new SourceLine(call.Number, text.Trim(), label, keyword, operand, call.File, new MacroUse(macro.Name, macro.DefinedAt.File, macro.DefinedAt.Number));
    }

    private static string ReplaceAll(string text, Dictionary<string, string> map)
    {
        foreach ((string from, string to) in map)
        {
            text = Regex.Replace(text, $"(?<![A-Za-z0-9_?@]){Regex.Escape(from)}(?![A-Za-z0-9_?@])", to);
        }

        return text;
    }

    private static bool IsKind(SourceLine line, SyntaxDialect dialect, MacroKind kind) =>
        line.Keyword is not null
        && dialect.Directives.TryGetValue(line.Keyword, out IDirective? directive)
        && directive is MacroDirective macro
        && macro.Kind == kind;

    private static bool IsLabelForm(SourceLine line) =>
        line is { Label: not null, Keyword: not null } && !line.Keyword.StartsWith('.');

    private static string? MacroName(SourceLine line)
    {
        if (line.Label is not null)
        {
            return line.Label;
        }

        string? operand = line.Operand?.Trim();
        if (operand is null)
        {
            return null;
        }

        int end = 0;
        while (end < operand.Length && !char.IsWhiteSpace(operand[end]) && operand[end] != ',')
        {
            end++;
        }

        return operand[..end];
    }

    private static (string[] Params, Dictionary<string, string> Defaults) MacroParams(SourceLine line, string name)
    {
        string rest = line.Label is not null
            ? line.Operand ?? string.Empty
            : line.Operand!.Trim()[name.Length..];
        var parameters = new List<string>();
        var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string part in SplitArgs(rest))
        {
            if (part.Length == 0)
            {
                continue;
            }

            string param = part;
            string? fallback = null;
            int equals = TopLevelEquals(part);
            if (equals >= 0)
            {
                param = part[..equals].Trim();
                fallback = part[(equals + 1)..].Trim();
                if (fallback.Length == 0)
                {
                    throw At(line, $"empty default for macro parameter '{param}'.");
                }
            }

            if (!Expression.IsIdentifier(param))
            {
                throw At(line, $"invalid macro parameter '{param}'.");
            }

            parameters.Add(param);
            if (fallback is not null)
            {
                defaults[param] = fallback;
            }
        }

        if (parameters.Count != parameters.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            throw At(line, $"duplicate macro parameter in '{line.Text.Trim()}'.");
        }

        return ([.. parameters], defaults);
    }

    private static int TopLevelEquals(string text)
    {
        int depth = 0;
        char? quote = null;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quote is not null)
            {
                quote = c == quote ? null : quote;
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c is '(' or '[')
            {
                depth++;
            }
            else if (c is ')' or ']')
            {
                depth--;
            }
            else if (c == '=' && depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static string[] SplitArgs(string? operand)
    {
        if (operand is null)
        {
            return [];
        }

        var parts = new List<string>();
        int depth = 0;
        char? quote = null;
        int start = 0;
        for (int i = 0; i < operand.Length; i++)
        {
            char c = operand[i];
            if (quote is not null)
            {
                quote = c == quote ? null : quote;
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c is '(' or '[')
            {
                depth++;
            }
            else if (c is ')' or ']')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                parts.Add(operand[start..i].Trim());
                start = i + 1;
            }
        }

        parts.Add(operand[start..].Trim());
        return [.. parts];
    }

    private static AssemblerException At(SourceLine line, string message) =>
        new(line.Number, message, line.File);

    private sealed record MacroDefinition(string Name, string[] Params, Dictionary<string, string> Defaults, IReadOnlyList<SourceLine> Body, SourceLine DefinedAt);
}
