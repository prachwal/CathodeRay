namespace CathodeRay.Assembler.Isa;

/// <summary>Element układu bajtów instrukcji: stały bajt albo odwołanie do pola operandu.</summary>
/// <param name="Literal">Bajt (dla elementu stałego).</param>
/// <param name="Field">Indeks pola we wzorcu operandu albo -1 dla bajtu stałego.</param>
public readonly record struct EncodingPart(byte Literal, int Field)
{
    /// <summary>Czy element odwołuje się do pola.</summary>
    public bool IsField => Field >= 0;

    /// <summary>Stały bajt.</summary>
    /// <param name="value">Bajt.</param>
    /// <returns>Element.</returns>
    public static EncodingPart Byte(byte value) => new(value, -1);

    /// <summary>Pole operandu.</summary>
    /// <param name="index">Indeks pola we wzorcu.</param>
    /// <returns>Element.</returns>
    public static EncodingPart Slot(int index) => new(0, index);
}
