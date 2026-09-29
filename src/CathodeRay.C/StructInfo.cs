namespace CathodeRay.C;

/// <summary>Układ struktury (współdzielony przez wszystkie <see cref="CType"/> tej struktury;
/// wypełniany po utworzeniu, żeby struktura mogła wskazywać na siebie).</summary>
public sealed class StructInfo
{
    /// <summary>Tworzy pusty układ.</summary>
    /// <param name="name">Nazwa struktury (bez <c>struct </c>).</param>
    public StructInfo(string name) => Name = name;

    /// <summary>Nazwa struktury.</summary>
    public string Name { get; }

    /// <summary>Pola w kolejności deklaracji.</summary>
    public List<StructField> Fields { get; } = [];

    /// <summary>Rozmiar w bajtach (suma pól).</summary>
    public int Size { get; set; }

    /// <summary><c>union</c>: pola nakładają się od przesunięcia 0.</summary>
    public bool IsUnion { get; set; }

    /// <summary>Układ policzony.</summary>
    public bool Complete { get; set; }

    /// <summary>Szuka pola po nazwie.</summary>
    /// <param name="field">Nazwa pola.</param>
    /// <returns>Pole albo <see langword="null"/>.</returns>
    public StructField? Find(string field) => Fields.Find(f => f.Name == field);
}
