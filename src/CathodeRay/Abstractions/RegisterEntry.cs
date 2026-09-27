namespace CathodeRay.Abstractions;

/// <summary>Wpis widoku rejestru: definicja (nazwa/szerokość/rola) + bieżąca wartość.</summary>
/// <param name="Definition">Definicja rejestru.</param>
/// <param name="Value">Wartość.</param>
public readonly record struct RegisterEntry(RegisterDefinition Definition, ulong Value)
{
    /// <summary>Nazwa rejestru (z definicji).</summary>
    public string Name => Definition.Name;

    /// <summary>Szerokość w bitach (z definicji).</summary>
    public int WidthBits => Definition.WidthBits;

    /// <summary>Rola rejestru (z definicji).</summary>
    public RegisterRole Role => Definition.Role;

    /// <summary>Liczba cyfr szesnastkowych (z definicji).</summary>
    public int HexDigits => Definition.HexDigits;

    /// <summary>Formatuje wartość jako hex o szerokości rejestru.</summary>
    /// <returns>Zapis szesnastkowy.</returns>
    public string Format() => Definition.Format(Value);
}
