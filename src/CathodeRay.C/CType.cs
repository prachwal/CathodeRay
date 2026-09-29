namespace CathodeRay.C;

/// <summary>Typ mini-C: 8-bit, 16-bit, wskaźnik albo tablica.</summary>
/// <param name="Kind">Rodzaj (<c>uchar</c>, <c>int</c>, <c>void</c>, <c>ptr</c>, <c>array</c>).</param>
/// <param name="Base">Typ bazowy wskaźnika/tablicy.</param>
/// <param name="Length">Długość tablicy (0 = nie-tablica).</param>
/// <param name="Info">Układ struktury (tylko <c>struct</c>).</param>
/// <param name="IsConst">Wartość tylko do odczytu (<c>const</c>).</param>
/// <param name="Sig">Sygnatura (tylko <c>fptr</c>, wskaźnik do funkcji).</param>
/// <param name="IsVolatile">Każdy odczyt i zapis musi zostać wykonany (<c>volatile</c>).</param>
public sealed record CType(string Kind, CType? Base = null, int Length = 0, StructInfo? Info = null, bool IsConst = false, FuncSig? Sig = null, bool IsVolatile = false)
{
    /// <summary>8-bit bez znaku.</summary>
    public static CType UChar { get; } = new("uchar");

    /// <summary>8-bit ze znakiem (<c>signed char</c>).</summary>
    public static CType SChar { get; } = new("schar");

    /// <summary>16-bit ze znakiem.</summary>
    public static CType Int { get; } = new("int");

    /// <summary>16-bit bez znaku.</summary>
    public static CType UInt { get; } = new("uint");

    /// <summary>32-bit ze znakiem.</summary>
    public static CType Long { get; } = new("long");

    /// <summary>32-bit bez znaku.</summary>
    public static CType ULong { get; } = new("ulong");

    /// <summary>Bez typu (wynik procedur).</summary>
    public static CType Void { get; } = new("void");

    /// <summary>Rozmiar w bajtach (wskaźnik: 2, tablica: N * rozmiar elementu).</summary>
    public int Size => Kind switch
    {
        "uchar" or "schar" => 1,
        "long" or "ulong" => 4,
        "array" => Length * (Base?.Size ?? 0),
        "struct" => Info?.Size ?? 0,
        _ => 2,
    };

    /// <summary>Typ całkowity (uchar, int, uint, long, ulong).</summary>
    public bool IsInteger => Kind is "uchar" or "schar" or "int" or "uint" or "long" or "ulong";

    /// <summary>Wskaźnik.</summary>
    /// <param name="base">Typ bazowy.</param>
    /// <returns>Typ wskaźnikowy.</returns>
    public static CType Pointer(CType @base) => new("ptr", @base);

    /// <summary>Wskaźnik do funkcji o danej sygnaturze (2 bajty jak wskaźnik).</summary>
    /// <param name="sig">Sygnatura.</param>
    /// <returns>Typ <c>fptr</c>.</returns>
    public static CType FuncPtr(FuncSig sig) => new("fptr", null, 0, null, false, sig);

    /// <summary>Struktura o danym układzie.</summary>
    /// <param name="info">Układ.</param>
    /// <returns>Typ strukturalny.</returns>
    public static CType Struct(StructInfo info) => new("struct", null, 0, info);

    /// <summary>Tablica (rozpada się na wskaźnik przy użyciu).</summary>
    /// <param name="base">Typ elementu.</param>
    /// <param name="length">Liczba elementów.</param>
    /// <returns>Typ tablicowy.</returns>
    public static CType Array(CType @base, int length) => new("array", @base, length);

    /// <summary>Wynik działania arytmetycznego na dwóch typach całkowitych: <c>ulong</c> &gt; <c>long</c> &gt; <c>uint</c> &gt; <c>int</c> &gt; <c>uchar</c>
    /// (<c>long</c> mieści każdą wartość <c>uint</c>).</summary>
    /// <param name="a">Pierwszy typ.</param>
    /// <param name="b">Drugi typ.</param>
    /// <returns>Typ wyniku.</returns>
    public static CType Promote(CType a, CType b) =>
        (a.Kind == "schar" && b.Kind is "schar" or "uchar") || (b.Kind == "schar" && a.Kind == "uchar") ? SChar
        : a.Kind == "ulong" || b.Kind == "ulong" ? ULong
        : a.Kind == "long" || b.Kind == "long" ? Long
        : a.Kind == "uint" || b.Kind == "uint" ? UInt
        : a.Kind == "int" || b.Kind == "int" ? Int
        : UChar;

    /// <summary>Rozpad tablicy na wskaźnik (jak w C).</summary>
    /// <returns>Wskaźnik do elementu dla tablic, ten sam typ wpp.</returns>
    public CType Decay() => Kind == "array" && Base is not null ? Pointer(Base) : this;

    /// <inheritdoc/>
    public override string ToString() => (IsConst ? "const " : string.Empty) + (IsVolatile ? "volatile " : string.Empty) + Kind switch
    {
        "ptr" => Base + "*",
        "array" => Base + "[" + Length + "]",
        "struct" => "struct " + Info?.Name,
        "fptr" => Sig!.Return + " (*)(" + string.Join(", ", Sig.Params) + ")",
        _ => Kind,
    };

    /// <summary>Parsuje nazwę typu ze źródła (<c>uchar/int/void</c>, wskaźniki w planie 18 tylko przez konwencję).</summary>
    /// <param name="name">Nazwa.</param>
    /// <returns>Typ.</returns>
    public static CType FromName(string name) => name switch
    {
        "uchar" => UChar,
        "schar" => SChar,
        "int" => Int,
        "uint" => UInt,
        "long" => Long,
        "ulong" => ULong,
        "void" => Void,
        _ => throw new ArgumentException($"Unknown type '{name}'.", nameof(name)),
    };
}
