namespace CathodeRay.C;

/// <summary>CPU przenosi argumenty ABI v2 przez rejestry (6502: arg0 w A/X) i parkuje A na czas pushy ramki.</summary>
internal interface IRegArgs
{
    /// <summary>Komórka do przechowania A na czas pushy ramki (prolog callee-saved czyta komórki przez A, więc intake
    /// argumentów z rejestrów musi iść po nich; X i pary pushy przeżywają).</summary>
    string? EntryParkCell { get; }

    /// <summary>Sam wskaźnik wołania pośredniego do miejsca docelowego (bez skoku); selektor stawia go
    /// przed argumentami v2 w rejestrach (ich ustawianie niszczy A).</summary>
    /// <param name="cell">Symbol komórki z adresem.</param>
    void SetupFp(string cell);

    /// <summary>A ← rejestr argumentu (intake v2; wołane, gdy A jeszcze go nie trzyma — selektor dba o kolejność).</summary>
    /// <param name="reg">Rejestr argumentu z modelu.</param>
    void FetchArg(string reg);

    /// <summary>Rejestr argumentu ← A (wołanie v2; wołane tuż po załadowaniu wartości do A).</summary>
    /// <param name="reg">Rejestr argumentu z modelu.</param>
    void StoreArg(string reg);
}
