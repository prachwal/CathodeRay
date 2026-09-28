using System.Globalization;

namespace CathodeRay;

/// <summary>Parser literałów liczbowych narzędzi: dziesiętne (<c>42</c>) i szesnastkowe (<c>$2A</c>, <c>0x2A</c>), bez znaku.</summary>
public static class NumberLiteral
{
    /// <summary>Próbuje sparsować literał.</summary>
    /// <param name="text">Tekst literału.</param>
    /// <param name="value">Wartość (nieujemna).</param>
    /// <returns>Czy tekst to poprawny literał.</returns>
    public static bool TryParse(string text, out int value)
    {
        ArgumentNullException.ThrowIfNull(text);
        bool parsed = text.StartsWith('$')
            ? int.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value)
            : text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? int.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value)
                : int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        return parsed && value >= 0;
    }
}
