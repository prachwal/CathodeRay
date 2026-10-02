namespace CathodeRay.C;

/// <summary>CPU ma adresowanie indeksowane z 8-bitowym rejestrem indeksowym (<see cref="Ir.LoadIdx"/>/<see cref="Ir.StoreIdx"/>).</summary>
internal interface IIndexed
{
    /// <summary>CPU ma adresowanie indeksowane z 8-bitowym rejestrem indeksowym.</summary>
    bool SupportsIndexed { get; }

    /// <summary>Ładuje rejestr indeksowy młodszym bajtem indeksu przesuniętym w lewo o <paramref name="shift"/>; może zniszczyć A.</summary>
    /// <param name="index">Adres młodszego bajtu indeksu.</param>
    /// <param name="shift">Przesunięcie.</param>
    void IndexSetup(string index, int shift);

    /// <summary>A ← bajt spod <c>adres + indeks</c>.</summary>
    /// <param name="address">Adres bazowy.</param>
    void IndexLoad(string address);

    /// <summary>Bajt spod <c>adres + indeks</c> ← A.</summary>
    /// <param name="address">Adres bazowy.</param>
    void IndexStore(string address);
}
