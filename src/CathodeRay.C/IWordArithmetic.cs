namespace CathodeRay.C;

/// <summary>CPU umie dodawać/odejmować słowa 16- i 32-bitowe jedną sekwencją (Z80: <c>add hl,</c> + <c>ex</c>; <c>adc</c> łańcuchem).</summary>
internal interface IWordArithmetic
{
    /// <summary>Dodawanie albo odejmowanie słów 16-bitowych <c>dst ← a ± b</c> jedną sekwencją CPU, bez zmiany A (może zmienić
    /// rejestry adresowe i flagi; flagi po niej są nieokreślone, selektor ich nie używa).</summary>
    /// <param name="dst">Cel (pamięć).</param>
    /// <param name="a">Lewy operand: stała albo pamięć.</param>
    /// <param name="b">Prawy operand: stała albo pamięć.</param>
    /// <param name="subtract"><see langword="true"/>: <c>a - b</c>.</param>
    /// <returns>Wynik jawny (<see cref="WordResult"/>).</returns>
    WordResult TryAddWord(Word dst, Word a, Word b, bool subtract);

    /// <summary>Dodawanie albo odejmowanie liczb 32-bitowych <c>dst ← a ± b</c> podanych jako połówki (młodsza, starsza) jedną
    /// sekwencją CPU z przeniesieniem między połówkami, bez zmiany A (flagi po niej nieokreślone).</summary>
    /// <param name="dst">Cel (pamięć): młodsza i starsza połowa.</param>
    /// <param name="a">Lewy operand: połówki (stałe albo pamięć).</param>
    /// <param name="b">Prawy operand: połówki (stałe albo pamięć).</param>
    /// <param name="subtract"><see langword="true"/>: <c>a - b</c>.</param>
    /// <returns><see langword="true"/>, gdy sekwencja została wyemitowana.</returns>
    bool TryAddLong((Word Lo, Word Hi) dst, (Word Lo, Word Hi) a, (Word Lo, Word Hi) b, bool subtract);
}
