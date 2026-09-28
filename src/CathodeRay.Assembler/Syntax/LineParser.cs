using System.Text.RegularExpressions;

namespace CathodeRay.Assembler.Syntax;

/// <summary>Rozbiór linii według dialektu: <c>[etykieta[:]] [słowo [operand]] [; komentarz]</c>,
/// przypisania <c>nazwa = wyr</c> / <c>nazwa EQU wyr</c> oraz <c>*= wyr</c> (MOS).</summary>
internal static partial class LineParser
{
    public static SourceLine Parse(int number, string text, SyntaxDialect dialect, Func<string, bool> isKeyword)
    {
        string code = StripComment(text).TrimEnd();
        string? label = null;

        Match colon = ColonLabel().Match(code);
        if (colon.Success)
        {
            label = colon.Groups[1].Value;
            code = code[colon.Length..];
        }
        else if (dialect.LabelsWithoutColon && code.Length > 0 && Expression.IsIdentifierStart(code[0]))
        {
            string first = FirstToken(code);
            if (!isKeyword(first) && !IsAssignmentKeyword(dialect, NextToken(code, first.Length)))
            {
                label = first;
                code = code[first.Length..];
            }
        }

        code = code.Trim();
        if (code.Length == 0)
        {
            return new SourceLine(number, text, label, null, null);
        }

        if (dialect.OrgByAssignment && dialect.ProgramCounter is char pc && OrgAssignment(pc).Match(code) is { Success: true } org)
        {
            return new SourceLine(number, text, label, "*=", Operand(code[org.Length..]));
        }

        string keyword = FirstToken(code);
        string rest = code[keyword.Length..].Trim();
        if (label is null && Expression.IsIdentifier(keyword) && IsAssignmentKeyword(dialect, FirstToken(rest)))
        {
            string word = FirstToken(rest);
            return new SourceLine(number, text, keyword, SourceLine.Assignment, Operand(rest[word.Length..]));
        }

        return new SourceLine(number, text, label, keyword, Operand(rest));
    }

    private static bool IsAssignmentKeyword(SyntaxDialect dialect, string token) =>
        token.Length > 0 && dialect.AssignmentKeywords.Contains(token);

    private static string? Operand(string text) => text.Trim() is { Length: > 0 } trimmed ? trimmed : null;

    private static string FirstToken(string text)
    {
        text = text.TrimStart();
        if (text.StartsWith('='))
        {
            return "=";
        }

        int end = 0;
        while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '=')
        {
            end++;
        }

        return text[..end];
    }

    private static string NextToken(string text, int after) => FirstToken(text[after..]);

    private static string StripComment(string text)
    {
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
            else if (c == ';')
            {
                return text[..i];
            }
        }

        return text;
    }

    private static Regex OrgAssignment(char pc) => pc == '*' ? StarAssignment() : new Regex($@"^\{pc}\s*=(?!=)");

    [GeneratedRegex(@"^\s*([A-Za-z_?@][A-Za-z0-9_?@]*):(?!=)")]
    private static partial Regex ColonLabel();

    [GeneratedRegex(@"^\*\s*=(?!=)")]
    private static partial Regex StarAssignment();
}
