using System.Runtime.CompilerServices;

namespace CathodeRay;

/// <summary>Operacje arytmetyczne 8-bit wspólne dla rdzeni CPU (flagi: przeniesienie i nadmiar).</summary>
public static class Alu
{
    /// <summary>Dodaje <paramref name="a"/> i <paramref name="b"/> z opcjonalnym przeniesieniem wejściowym.</summary>
    /// <param name="a">Pierwszy składnik.</param>
    /// <param name="b">Drugi składnik.</param>
    /// <param name="carryIn">Przeniesienie wejściowe (dodawane jako 1, gdy <see langword="true"/>).</param>
    /// <returns>Wartość 8-bit, przeniesienie wyjściowe oraz nadmiar ze znaku.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AluResult Add(byte a, byte b, bool carryIn = false)
    {
        var sum = a + b + (carryIn ? 1 : 0);
        var value = (byte)sum;
        return new AluResult(value, sum > byte.MaxValue, ((a ^ value) & (b ^ value) & 0x80) != 0);
    }

    /// <summary>Odejmuje <paramref name="b"/> od <paramref name="a"/> z opcjonalnym pożyczeniem wejściowym.</summary>
    /// <param name="a">Odjemna.</param>
    /// <param name="b">Odjemnik.</param>
    /// <param name="borrowIn">Pożyczenie wejściowe (odejmowane jako 1, gdy <see langword="true"/>).</param>
    /// <returns>Wartość 8-bit, brak pożyczenia (carry) oraz nadmiar ze znaku.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AluResult Subtract(byte a, byte b, bool borrowIn = false)
    {
        var difference = a - b - (borrowIn ? 1 : 0);
        var value = (byte)difference;
        return new AluResult(value, difference >= 0, ((a ^ b) & (a ^ value) & 0x80) != 0);
    }
}
