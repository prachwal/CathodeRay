namespace CathodeRay.C;

/// <summary>Typ mini-C: 8-bit, 16-bit albo wskaźnik (do typu bazowego).</summary>
/// <param name="Kind">Rodzaj.</param>
/// <param name="Base">Typ bazowy wskaźnika (tylko dla <c>Pointer</c>).</param>
public sealed record CType(string Kind, CType? Base = null)
{
    /// <summary>8-bit bez znaku.</summary>
    public static CType UChar { get; } = new("uchar");

    /// <summary>16-bit ze znakiem.</summary>
    public static CType Int { get; } = new("int");

    /// <summary>Bez typu (wynik procedur).</summary>
    public static CType Void { get; } = new("void");

    /// <summary>Rozmiar w bajtach (wskaźnik: 2).</summary>
    public int Size => Kind == "uchar" ? 1 : 2;

    /// <summary>Wskaźnik.</summary>
    /// <param name="base">Typ bazowy.</param>
    /// <returns>Typ wskaźnikowy.</returns>
    public static CType Pointer(CType @base) => new("ptr", @base);

    /// <inheritdoc/>
    public override string ToString() => Kind == "ptr" ? Base + "*" : Kind;

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
