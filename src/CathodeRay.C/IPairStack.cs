namespace CathodeRay.C;

/// <summary>CPU umie odkładać i zdejmować pary rejestrów komórek na stos (Z80/8080: <c>push/pop bc</c>, także całymi słowami).
/// Pary żywe za wołaniem (<see cref="CellMap.SavedAround"/>) ma tylko CPU z niepustymi <see cref="ByteIsa.CellPairs"/>.</summary>
internal interface IPairStack
{
    /// <summary>Odkłada parę rejestrów z <see cref="ByteIsa.CellPairs"/> na stos (bez zmiany A i flag).</summary>
    /// <param name="pair">Para, np. <c>bc</c>.</param>
    void PushPair(string pair);

    /// <summary>Zdejmuje parę rejestrów ze stosu (bez zmiany A i flag).</summary>
    /// <param name="pair">Para, np. <c>bc</c>.</param>
    void PopPair(string pair);

    /// <summary>Odkłada na stos słowo z pamięci (prolog ramki) jedną sekwencją CPU, bez zmiany A. Decyzja zależy tylko od
    /// <paramref name="word"/>, tak samo jak w <see cref="TryPopWord"/>, więc prolog i epilog grupują bajty identycznie.</summary>
    /// <param name="word">Słowo w pamięci (bajty sąsiednie).</param>
    /// <returns><see langword="true"/>, gdy sekwencja została wyemitowana.</returns>
    bool TryPushWord(Word word);

    /// <summary>Zdejmuje ze stosu słowo do pamięci (epilog ramki), bez zmiany A; para przeciwna do <see cref="TryPushWord"/>.</summary>
    /// <param name="word">Słowo w pamięci (bajty sąsiednie).</param>
    /// <param name="keepResult">Rejestr wyniku (<c>ReturnsInResultReg</c>) niesie już wynik funkcji i nie może się zmienić.</param>
    /// <returns><see langword="true"/>, gdy sekwencja została wyemitowana.</returns>
    bool TryPopWord(Word word, bool keepResult);
}
