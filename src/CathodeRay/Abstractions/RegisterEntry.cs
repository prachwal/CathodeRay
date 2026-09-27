using System.Globalization;

namespace CathodeRay.Abstractions;

/// <summary>Pojedynczy rejestr w widoku: nazwa, wartość i szerokość w bitach.</summary>
/// <param name="Name">Nazwa (np. "A", "PC").</param>
/// <param name="Value">Wartość.</param>
/// <param name="WidthBits">Szerokość w bitach (4/8/16/32/64).</param>
public readonly record struct RegisterEntry(string Name, ulong Value, int WidthBits)
{
    /// <summary>Liczba cyfr szesnastkowych potrzebna do zapisu wartości (co najmniej 1).</summary>
    public int HexDigits => Math.Max(1, (WidthBits + 3) / 4);

    /// <summary>Formatuje wartość jako hex o szerokości rejestru (np. "1F").</summary>
    /// <returns>Zapis szesnastkowy.</returns>
    public string Format() => Value.ToString($"X{HexDigits}", CultureInfo.InvariantCulture);
}
