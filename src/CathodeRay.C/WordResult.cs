namespace CathodeRay.C;

/// <summary>Wynik prymitywu słowa: czy wyemitowano sekwencję i czy wynik został w HL
/// (ścieżka świeżego wyniku do <c>Ret</c> w selektorze). Jawny zamiast mutowanej flagi.</summary>
/// <param name="Emitted">Prymityw wyemitował sekwencję.</param>
/// <param name="InHL">Wynik leży w HL (fałsz np. po zamianie do DE).</param>
internal readonly record struct WordResult(bool Emitted, bool InHL);
