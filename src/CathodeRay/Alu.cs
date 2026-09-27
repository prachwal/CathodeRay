namespace CathodeRay;

/// <summary>Operacje arytmetyczne 8-bit wspólne dla rdzeni CPU (flagi: przeniesienie i nadmiar).</summary>
public static class Alu
{
    /// <summary>Dodaje <paramref name="a"/> i <paramref name="b"/> z opcjonalnym przeniesieniem wejściowym.</summary>
    /// <param name="a">Pierwszy składnik.</param>
    /// <param name="b">Drugi składnik.</param>
    /// <param name="carryIn">Przeniesienie wejściowe (dodawane jako 1, gdy <see langword="true"/>).</param>
    /// <returns>Wartość 8-bit, przeniesienie wyjściowe oraz nadmiar ze znaku.</returns>
    public static (byte Value, bool Carry, bool Overflow) Add(byte a, byte b, bool carryIn = false)
    {
        var sum = a + b + (carryIn ? 1 : 0);
        var value = (byte)sum;
        var carry = sum > byte.MaxValue;
        var overflow = ((a ^ value) & (b ^ value) & 0x80) != 0;
        return (value, carry, overflow);
    }

    /// <summary>Odejmuje <paramref name="b"/> od <paramref name="a"/> z opcjonalnym pożyczeniem wejściowym.</summary>
    /// <param name="a">Odjemna.</param>
    /// <param name="b">Odjemnik.</param>
    /// <param name="borrowIn">Pożyczenie wejściowe (odejmowane jako 1, gdy <see langword="true"/>).</param>
    /// <returns>Wartość 8-bit, brak pożyczenia (carry) oraz nadmiar ze znaku.</returns>
    public static (byte Value, bool Carry, bool Overflow) Subtract(byte a, byte b, bool borrowIn = false)
    {
        var difference = a - b - (borrowIn ? 1 : 0);
        var value = (byte)difference;
        var carry = difference >= 0;
        var overflow = ((a ^ b) & (a ^ value) & 0x80) != 0;
        return (value, carry, overflow);
    }
}
