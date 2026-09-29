namespace CathodeRay.C;

/// <summary>Typ mini-C: 8-bit, 16-bit, wskaźnik albo tablica.</summary>
/// <param name="Kind">Rodzaj (<c>uchar</c>, <c>int</c>, <c>void</c>, <c>ptr</c>, <c>array</c>).</param>
/// <param name="Base">Typ bazowy wskaźnika/tablicy.</param>
/// <param name="Length">Długość tablicy (0 = nie-tablica).</param>
public sealed record CType(string Kind, CType? Base = null, int Length = 0)
{
    /// <summary>8-bit bez znaku.</summary>
    public static CType UChar { get; } = new("uchar");

    /// <summary>16-bit ze znakiem.</summary>
    public static CType Int { get; } = new("int");

    /// <summary>Bez typu (wynik procedur).</summary>
    public static CType Void { get; } = new("void");

    /// <summary>Rozmiar w bajtach (wskaźnik: 2, tablica: N * rozmiar elementu).</summary>
    public int Size => Kind switch
    {
        "uchar" => 1,
        "array" => Length * (Base?.Size ?? 0),
        _ => 2,
    };

    /// <summary>Wskaźnik.</summary>
    /// <param name="base">Typ bazowy.</param>
    /// <returns>Typ wskaźnikowy.</returns>
    public static CType Pointer(CType @base) => new("ptr", @base);

    /// <summary>Tablica (rozpada się na wskaźnik przy użyciu).</summary>
    /// <param name="base">Typ elementu.</param>
    /// <param name="length">Liczba elementów.</param>
    /// <returns>Typ tablicowy.</returns>
    public static CType Array(CType @base, int length) => new("array", @base, length);

    /// <summary>Rozpad tablicy na wskaźnik (jak w C).</summary>
    /// <returns>Wskaźnik do elementu dla tablic, ten sam typ wpp.</returns>
    public CType Decay() => Kind == "array" && Base is not null ? Pointer(Base) : this;

    /// <inheritdoc/>
    public override string ToString() => Kind switch
    {
        "ptr" => Base + "*",
        "array" => Base + "[" + Length + "]",
        _ => Kind,
    };

    /// <summary>Parsuje nazwę typu ze źródła (<c>uchar/int/void</c>, wskaźniki w planie 18 tylko przez konwencję).</summary>
    /// <param name="name">Nazwa.</param>
    /// <returns>Typ.</returns>
    public static CType FromName(string name) => name switch
    {
        "uchar" => UChar,
        "int" => Int,
        "void" => Void,
        _ => throw new ArgumentException($"Unknown type '{name}'.", nameof(name)),
    };
}
