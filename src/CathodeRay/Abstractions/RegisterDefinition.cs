using System.Globalization;

namespace CathodeRay.Abstractions;

/// <summary>Definicja rejestru: nazwa, szerokość w bitach i rola. Statyczny opis architektury, niezależny od wartości.</summary>
/// <param name="Name">Nazwa (np. "A", "PC").</param>
/// <param name="WidthBits">Szerokość w bitach (4/8/16/32/64).</param>
/// <param name="Role">Rola rejestru.</param>
public readonly record struct RegisterDefinition(string Name, int WidthBits, RegisterRole Role)
{
    /// <summary>Liczba cyfr szesnastkowych potrzebna do zapisu wartości (co najmniej 1).</summary>
    public int HexDigits => Math.Max(1, (WidthBits + 3) / 4);

    /// <summary>Czy rejestr jest 8-bitowy (formatowanie jak 2 cyfry hex).</summary>
    public bool IsEightBit => WidthBits == 8;

    /// <summary>Formatuje wartość jako hex o szerokości rejestru (np. "1F").</summary>
    /// <param name="value">Wartość.</param>
    /// <returns>Zapis szesnastkowy.</returns>
    public string Format(ulong value) => value.ToString($"X{HexDigits}", CultureInfo.InvariantCulture);
}
