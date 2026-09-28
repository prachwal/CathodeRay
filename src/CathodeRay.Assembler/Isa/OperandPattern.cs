using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CathodeRay.Assembler.Isa;

/// <summary>Kształt operandu instrukcji: literały (rejestry, nawiasy, <c>#</c>) i sloty na wyrażenia.
/// Sloty: <c>{b}</c> bajt, <c>{w}</c> słowo, <c>{r}</c> skok względny 8-bit, <c>{d}</c> przesunięcie ze znakiem
/// (w <c>(IX{d})</c> slot łapie razem ze znakiem: <c>+5</c>, <c>-1</c>), <c>{c=N}</c> stała wybierająca opcode
/// (np. numer bitu w Z80 <c>BIT {c=3},A</c>): musi być znana w pierwszym przebiegu, nie trafia do kodu. Literały bez rozróżniania wielkości liter i spacji.
/// Slot nie obejmuje przecinka spoza apostrofów: przecinek rozdziela części operandu we wszystkich obsługiwanych składniach.
/// Przykłady: <c>#{b}</c>, <c>({b}),Y</c>, <c>{w},X</c>, <c>B,{b}</c>, pusty = brak operandu.</summary>
public sealed class OperandPattern
{
    private readonly Regex _regex;

    private OperandPattern(string template, IReadOnlyList<FieldKind> fields, IReadOnlyList<int?> constants, int literalWeight, Regex regex)
    {
        Template = template;
        Fields = fields;
        Constants = constants;
        LiteralWeight = literalWeight;
        _regex = regex;
    }

    /// <summary>Wzorzec źródłowy (np. <c>({b}),Y</c>).</summary>
    public string Template { get; }

    /// <summary>Sloty w kolejności występowania.</summary>
    public IReadOnlyList<FieldKind> Fields { get; }

    /// <summary>Wymagana wartość dla slotów <see cref="FieldKind.Constant"/> (<see langword="null"/> dla pozostałych), równolegle do <see cref="Fields"/>.</summary>
    public IReadOnlyList<int?> Constants { get; }

    /// <summary>Liczba znaków literalnych (bez spacji); więcej = wzorzec bardziej szczegółowy.</summary>
    public int LiteralWeight { get; }

    /// <summary>Parsuje wzorzec.</summary>
    /// <param name="template">Wzorzec; pusty lub <see langword="null"/> = brak operandu.</param>
    /// <returns>Wzorzec.</returns>
    public static OperandPattern Parse(string? template)
    {
        template ??= string.Empty;
        var fields = new List<FieldKind>();
        var constants = new List<int?>();
        var regex = new StringBuilder(@"^\s*");
        int weight = 0;
        for (int i = 0; i < template.Length; i++)
        {
            char c = template[i];
            if (c == '{')
            {
                int end = template.IndexOf('}', i);
                string slot = template[(i + 1)..end];
                constants.Add(slot.StartsWith("c=", StringComparison.Ordinal)
                    ? int.Parse(slot.AsSpan(2), NumberStyles.Integer, CultureInfo.InvariantCulture)
                    : null);
                fields.Add(slot switch
                {
                    "b" => FieldKind.Byte,
                    "w" => FieldKind.Word,
                    "r" => FieldKind.Relative8,
                    "d" => FieldKind.Displacement8,
                    _ when constants[^1] is not null => FieldKind.Constant,
                    _ => throw new FormatException($"Unknown slot '{{{slot}}}' in pattern '{template}'."),
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
            template, fields, constants, weight, new Regex(regex.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
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
