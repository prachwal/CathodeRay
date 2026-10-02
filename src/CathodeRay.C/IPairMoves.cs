namespace CathodeRay.C;

/// <summary>CPU umie skopiować słowo 16-bitowe jedną sekwencją, bez użycia A (Z80/8080: para rejestrów, 6800: <c>ldd</c>).</summary>
internal interface IPairMoves
{
    /// <summary>Kopia słowa 16-bitowego <c>dst ← src</c> jedną sekwencją CPU, bez użycia A (może zmienić rejestry adresowe i flagi).</summary>
    /// <param name="dst">Cel (pamięć).</param>
    /// <param name="src">Źródło: stała albo pamięć.</param>
    /// <returns><see langword="true"/>, gdy sekwencja została wyemitowana.</returns>
    bool TryMoveWord(Word dst, Word src);
}
