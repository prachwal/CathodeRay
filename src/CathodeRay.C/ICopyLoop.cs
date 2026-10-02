namespace CathodeRay.C;

/// <summary>CPU umie skopiować blok o długości w czasie wykonania jedną sekwencją (Z80: <c>ldir</c>).</summary>
internal interface ICopyLoop
{
    /// <summary>Kopia bloku (liczba bajtów w czasie wykonania) przez <c>ldir</c>: wołający (matcher pętli
    /// w selektorze) gwarantuje liczbę niezerową na wejściu, rozłączność par chroni push/pop w środku,
    /// a komórki Dst/Src/Count są martwe za pętlą (bez writebacku). Flagi po niej nieokreślone.</summary>
    /// <param name="dst">Cel (wskaźnik w pamięci albo parze).</param>
    /// <param name="src">Źródło (wskaźnik w pamięci albo parze).</param>
    /// <param name="count">Liczba bajtów (słowo w pamięci albo parze, niezerowe).</param>
    /// <returns><see langword="true"/>, gdy sekwencja została wyemitowana.</returns>
    bool TryCopyLoop(Word dst, Word src, Word count);
}
