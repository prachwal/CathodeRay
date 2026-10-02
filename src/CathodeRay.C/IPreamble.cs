namespace CathodeRay.C;

/// <summary>CPU dopisuje linie na początku modułu (definicje pomocnicze, np. adresy strony zerowej).</summary>
internal interface IPreamble
{
    /// <summary>Linie na początku modułu (definicje pomocnicze CPU), np. adresy strony zerowej.</summary>
    /// <returns>Tekst albo pusty.</returns>
    string Preamble();
}
