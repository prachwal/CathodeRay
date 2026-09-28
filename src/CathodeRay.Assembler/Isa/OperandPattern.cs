using System.Text;
using System.Text.RegularExpressions;

namespace CathodeRay.Assembler.Isa;

/// <summary>Kształt operandu instrukcji: literały (rejestry, nawiasy, <c>#</c>) i sloty na wyrażenia.
/// Sloty: <c>{b}</c> bajt, <c>{w}</c> słowo, <c>{r}</c> skok względny 8-bit. Literały bez rozróżniania wielkości liter i spacji.
/// Slot nie obejmuje przecinka spoza apostrofów: przecinek rozdziela części operandu we wszystkich obsługiwanych składniach.
/// Przykłady: <c>#{b}</c>, <c>({b}),Y</c>, <c>{w},X</c>, <c>B,{b}</c>, pusty = brak operandu.</summary>
public sealed class OperandPattern
{
    private readonly Regex _regex;

    private OperandPattern(string template, IReadOnlyList<FieldKind> fields, int literalWeight, Regex regex)
    {
        Template = template;
        Fields = fields;
        LiteralWeight = literalWeight;
        _regex = regex;
    }

    /// <summary>Wzorzec źródłowy (np. <c>({b}),Y</c>).</summary>
    public string Template { get; }

    /// <summary>Sloty w kolejności występowania.</summary>
    public IReadOnlyList<FieldKind> Fields { get; }

    /// <summary>Liczba znaków literalnych (bez spacji); więcej = wzorzec bardziej szczegółowy.</summary>
    public int LiteralWeight { get; }

    /// <summary>Parsuje wzorzec.</summary>
    /// <param name="template">Wzorzec; pusty lub <see langword="null"/> = brak operandu.</param>
    /// <returns>Wzorzec.</returns>
    public static OperandPattern Parse(string? template)
    {
        template ??= string.Empty;
        var fields = new List<FieldKind>();
        var regex = new StringBuilder(@"^\s*");
        int weight = 0;
        for (int i = 0; i < template.Length; i++)
        {
            char c = template[i];
            if (c == '{')
            {
                int end = template.IndexOf('}', i);
                fields.Add(template[(i + 1)..end] switch
                {
                    "b" => FieldKind.Byte,
                    "w" => FieldKind.Word,
                    "r" => FieldKind.Relative8,
                    string other => throw new FormatException($"Unknown slot '{{{other}}}' in pattern '{template}'."),
                });
                regex.Append(@"\s*((?:'[^']*'|[^,'])+?)\s*");
                i = end;
            }
            else if (char.IsWhiteSpace(c))
            {
                regex.Append(@"\s*");
            }
            else
            {
                regex.Append(Regex.Escape(c.ToString())).Append(@"\s*");
                weight++;
            }
        }

        regex.Append('$');
        return new OperandPattern(
            template, fields, weight, new Regex(regex.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    }

    /// <summary>Dopasowuje tekst operandu i zwraca teksty wyrażeń dla slotów.</summary>
    /// <param name="operand">Tekst operandu lub <see langword="null"/>.</param>
    /// <param name="captures">Wyrażenia w kolejności slotów.</param>
    /// <returns>Czy operand pasuje.</returns>
    public bool TryMatch(string? operand, out string[] captures)
    {
        Match match = _regex.Match(operand ?? string.Empty);
        captures = match.Success ? [.. match.Groups.Values.Skip(1).Select(static g => g.Value)] : [];
        return match.Success;
    }

    /// <inheritdoc/>
    public override string ToString() => Template;
}
