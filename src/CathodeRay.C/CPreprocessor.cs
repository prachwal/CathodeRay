using System.Text;
using System.Text.RegularExpressions;

namespace CathodeRay.C;

/// <summary>Preprocesor mini-C: czyste mapowanie 1:1 dyrektyw C na dyrektywy
/// kropkowe (<c>#include "f"</c> → <c>.include "f"</c>, <c>#define X v</c> →
/// <c>.define X v</c>) oraz rozwijanie dyrektyw kropkowych (splice pliku,
/// makra tekstowe). Funkcyjne i nieznane dyrektywy: błąd. Bez zmian liczby linii.</summary>
public static class CPreprocessor
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "uchar", "int", "void", "if", "else", "while", "for", "return",
    };

    /// <summary>Mapuje dyrektywy <c>#</c> na kropkowe (1:1, bez IO).</summary>
    /// <param name="source">Tekst programu w C.</param>
    /// <returns>Tekst z dyrektywami kropkowymi.</returns>
    /// <exception cref="CPreprocessException">Funkcyjna lub nieznana dyrektywa.</exception>
    public static string MapDirectives(string source)
    {
        string[] lines = source.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            string stripped = line.TrimStart();
            if (!stripped.StartsWith('#'))
            {
                lines[i] = line;
                continue;
            }

            string after = stripped[1..].TrimStart();
            if (after.StartsWith("include", StringComparison.Ordinal) && IsDirectiveEnd(after, "include"))
            {
                string rest = after["include".Length..].Trim();
                if (rest.Length >= 2 && rest[0] == '"' && rest[^1] == '"')
                {
                    lines[i] = line[..(line.Length - stripped.Length)] + ".include " + rest;
                    continue;
                }

                throw new CPreprocessException(i + 1, "#include needs \"file\".");
            }

            if (after.StartsWith("define", StringComparison.Ordinal) && IsDirectiveEnd(after, "define"))
            {
                string rest = after["define".Length..].TrimStart();
                (string name, string value) = SplitDefine(rest, i + 1);
                lines[i] = line[..(line.Length - stripped.Length)] + ".define " + name + " " + value;
                continue;
            }

            throw new CPreprocessException(i + 1, $"unknown directive '{stripped}'.");
        }

        return string.Join('\n', lines);
    }

    /// <summary>Rozwija dyrektywy kropkowe: splice <c>.include</c>, makra <c>.define</c>.</summary>
    /// <param name="source">Tekst (po <see cref="MapDirectives"/>).</param>
    /// <param name="reader">Czyta plik (null = brak).</param>
    /// <returns>Czysty tekst C.</returns>
    /// <exception cref="CPreprocessException">Błąd dyrektywy, brak pliku, cykl, rekurencja.</exception>
    public static string Expand(string source, Func<string, string?>? reader)
    {
        var macros = new Dictionary<string, string>(StringComparer.Ordinal);
        var output = new List<string>();
        ExpandLines(source.Split('\n'), reader, macros, [], output);
        return string.Join('\n', output);
    }

    private static void ExpandLines(
        string[] lines,
        Func<string, string?>? reader,
        Dictionary<string, string> macros,
        List<string> stack,
        List<string> output)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            string stripped = line.TrimStart();
            if (stripped.StartsWith(".include", StringComparison.Ordinal) && IsDirectiveEnd(stripped, ".include"))
            {
                string rest = stripped[".include".Length..].Trim();
                if (rest.Length < 2 || rest[0] != '"' || rest[^1] != '"')
                {
                    throw new CPreprocessException(i + 1, ".include needs \"file\".");
                }

                if (reader is null)
                {
                    throw new CPreprocessException(i + 1, ".include needs a file reader.");
                }

                string name = rest[1..^1];
                if (stack.Contains(name, StringComparer.Ordinal))
                {
                    throw new CPreprocessException(i + 1, $"include cycle '{name}'.");
                }

                string? content = reader(name);
                if (content is null)
                {
                    throw new CPreprocessException(i + 1, $"include file '{name}' not found.");
                }

                stack.Add(name);
                ExpandLines(MapDirectives(content).Split('\n'), reader, macros, stack, output);
                stack.RemoveAt(stack.Count - 1);
                continue;
            }

            if (stripped.StartsWith(".define", StringComparison.Ordinal) && IsDirectiveEnd(stripped, ".define"))
            {
                string rest = stripped[".define".Length..].TrimStart();
                (string name, string value) = SplitDefine(rest, i + 1);
                if (Keywords.Contains(name))
                {
                    throw new CPreprocessException(i + 1, $"macro name '{name}' is a keyword.");
                }

                if (!macros.TryAdd(name, value))
                {
                    throw new CPreprocessException(i + 1, $"macro '{name}' already defined.");
                }

                output.Add(string.Empty);
                continue;
            }

            output.Add(Substitute(line, macros, i + 1));
        }
    }

    private static (string Name, string Value) SplitDefine(string rest, int line)
    {
        int end = 0;
        while (end < rest.Length && (char.IsAsciiLetterOrDigit(rest[end]) || rest[end] == '_'))
        {
            end++;
        }

        string name = rest[..end];
        if (name.Length == 0 || char.IsAsciiDigit(name[0]))
        {
            throw new CPreprocessException(line, $".define needs a name, got '{rest}'.");
        }

        if (end < rest.Length && rest[end] == '(')
        {
            throw new CPreprocessException(line, "function-like macros are not supported (use plain .define).");
        }

        string value = rest[end..].Trim();
        if (value.Length == 0)
        {
            throw new CPreprocessException(line, $".define '{name}' needs a value.");
        }

        return (name, value);
    }

    private static string Substitute(string line, Dictionary<string, string> macros, int lineno)
    {
        for (int pass = 0; pass < 100; pass++)
        {
            bool changed = false;
            foreach ((string name, string value) in macros)
            {
                string replaced = MacroPattern(name).Replace(line, _ => value);
                changed |= replaced != line;
                line = replaced;
            }

            if (!changed)
            {
                foreach (string name in macros.Keys)
                {
                    if (MacroPattern(name).IsMatch(line))
                    {
                        throw new CPreprocessException(lineno, $"recursive macro '{name}'.");
                    }
                }

                return line;
            }
        }

        throw new CPreprocessException(lineno, "macro expansion too deep.");
    }

    private static bool IsDirectiveEnd(string text, string directive) =>
        text.Length == directive.Length || char.IsWhiteSpace(text[directive.Length]);

    private static Regex MacroPattern(string name) => new(@"\b" + Regex.Escape(name) + @"\b");
}
