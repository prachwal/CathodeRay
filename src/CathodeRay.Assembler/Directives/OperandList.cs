using System.Diagnostics.CodeAnalysis;
using CathodeRay.Assembler.Syntax;

namespace CathodeRay.Assembler.Directives;

/// <summary>Podział operandu dyrektywy na elementy po przecinkach (z pominięciem przecinków w cudzysłowach).</summary>
public static class OperandList
{
    /// <summary>Dzieli operand na elementy (bez pustych: pusty element = błąd składni).</summary>
    /// <param name="operand">Tekst operandu.</param>
    /// <returns>Elementy bez spacji na brzegach.</returns>
    /// <exception cref="FormatException">Pusty element lub niezamknięty cudzysłów.</exception>
    public static IReadOnlyList<string> Split(string operand)
    {
        ArgumentNullException.ThrowIfNull(operand);
        var items = new List<string>();
        int start = 0;
        char? quote = null;
        for (int i = 0; i <= operand.Length; i++)
        {
            char c = i < operand.Length ? operand[i] : ',';
            if (quote is not null)
            {
                quote = c == quote ? null : quote;
            }
            else if (i < operand.Length && Expression.OpensQuote(operand, i))
            {
                quote = c;
            }
            else if (c == ',')
            {
                string item = operand[start..i].Trim();
                items.Add(item.Length > 0 ? item : throw new FormatException("empty value in list."));
                start = i + 1;
            }
        }

        return quote is null ? items : throw new FormatException("unterminated string.");
    }

    /// <summary>Zwraca zawartość łańcucha w cudzysłowach (<c>"..."</c> lub <c>'...'</c> dłuższego niż jeden znak).
    /// Pojedynczy znak w apostrofach to stała znakowa w wyrażeniu, nie łańcuch.</summary>
    /// <param name="item">Element listy.</param>
    /// <param name="text">Zawartość.</param>
    /// <returns>Czy element jest łańcuchem.</returns>
    public static bool TryUnquote(string item, [NotNullWhen(true)] out string? text)
    {
        ArgumentNullException.ThrowIfNull(item);
        bool quoted = item.Length >= 2 && item[0] is '"' or '\'' && item[^1] == item[0] && item.IndexOf(item[0], 1) == item.Length - 1;
        text = quoted && (item[0] == '"' || item.Length != 3) ? item[1..^1] : null;
        return text is not null;
    }
}
