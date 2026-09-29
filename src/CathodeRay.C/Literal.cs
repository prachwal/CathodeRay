using System.Globalization;

namespace CathodeRay.C;

/// <summary>Stała całkowita ze źródła: wartość i typ. Bez przyrostka do 255 to <c>uchar</c>, do 65535 <c>int</c>; przyrostek
/// <c>U</c> daje <c>uint</c>, <c>L</c> albo wartość powyżej 65535 — <c>long</c> (<c>ulong</c> z <c>U</c> lub powyżej 0x7FFFFFFF).</summary>
/// <param name="Value">Wartość (0..0xFFFFFFFF; 64-bitowa jako <see cref="long"/> z zawiniętym bitem znaku).</param>
/// <param name="Type">Typ stałej.</param>
internal readonly record struct Literal(long Value, CType Type)
{
    /// <summary>Stała 32-bitowa (nie składa się jej w 16-bitowej arytmetyce kompilatora); dla <c>float</c> wartość to bity IEEE-754.</summary>
    public bool IsLong => Type.Size >= 4;

    /// <summary>Parsuje liczbę z opcjonalnym przyrostkiem.</summary>
    /// <param name="text">Tekst tokenu (<c>123</c>, <c>0xFF</c>, <c>100000L</c>, <c>7u</c>).</param>
    /// <param name="literal">Wynik.</param>
    /// <returns><see langword="false"/>, gdy tekst nie jest poprawną liczbą albo przekracza 64 bity.</returns>
    public static bool TryParse(string text, out Literal literal)
    {
        literal = default;
        bool isHex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (!isHex && (text.Contains('.', StringComparison.Ordinal) || text.Contains('e', StringComparison.OrdinalIgnoreCase)))
        {
            string number = text.TrimEnd('f', 'F', 'l', 'L');
            if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double real) || double.IsInfinity((float)real))
            {
                return false;
            }

            literal = new Literal((uint)BitConverter.SingleToInt32Bits((float)real), CType.Float);
            return true;
        }

        int end = text.Length;
        bool hasL = false;
        bool hasU = false;
        int longs = 0;
        while (end > 0 && text[end - 1] is 'l' or 'L' or 'u' or 'U')
        {
            hasL |= text[end - 1] is 'l' or 'L';
            longs += text[end - 1] is 'l' or 'L' ? 1 : 0;
            hasU |= text[end - 1] is 'u' or 'U';
            end--;
        }

        string digits = text[..end];
        bool hex = digits.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (!ulong.TryParse(hex ? digits[2..] : digits, hex ? NumberStyles.HexNumber : NumberStyles.None, CultureInfo.InvariantCulture, out ulong value)
            || (hex ? digits.Length == 2 : digits.Length == 0))
        {
            return false;
        }

        CType type;
        if (longs >= 2 || value > uint.MaxValue)
        {
            type = hasU || value > long.MaxValue ? CType.ULLong : CType.LLong;
        }
        else if (!hasL && value <= ushort.MaxValue)
        {
            type = hasU ? CType.UInt : value <= byte.MaxValue ? CType.UChar : CType.Int;
        }
        else
        {
            type = hasU || value > int.MaxValue ? CType.ULong : CType.Long;
        }

        literal = new Literal((long)value, type);
        return true;
    }
}
