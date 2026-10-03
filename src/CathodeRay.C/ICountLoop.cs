namespace CathodeRay.C;

/// <summary>CPU umie pętlę licznikową na rozkazie typu <c>djnz</c> (Z80: <c>djnz e</c>:
/// dekrementacja B i skok, gdy nie zero). W poz. 5 tylko wykrywanie; emisja w poz. 6.</summary>
internal interface ICountLoop
{
    /// <summary>Wykrywa pętlę licznikową od podanej pozycji (jak <see cref="ICopyLoop"/>).</summary>
    /// <param name="function">Emitowana funkcja.</param>
    /// <param name="index">Pozycja kandydata (etykieta pętli).</param>
    /// <returns>Dopasowanie albo null.</returns>
    CountLoopMatch? MatchCountLoop(Ir.Function function, int index);
}

/// <summary>Dopasowana pętla licznikowa: licznik <c>uchar</c>, jedno <c>-1</c> w ciele,
/// wyjście tylko nagłówkiem, licznik po pętli nieużywany.</summary>
/// <param name="Counter">Symbol licznika (komórka W1).</param>
/// <param name="Top">Etykieta początku pętli.</param>
/// <param name="End">Etykieta końca pętli.</param>
/// <param name="After">Pozycja za etykietą końca.</param>
internal sealed record CountLoopMatch(string Counter, string Top, string End, int After);
