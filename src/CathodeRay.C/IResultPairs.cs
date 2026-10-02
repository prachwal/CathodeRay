namespace CathodeRay.C;

/// <summary>CPU umie przenieść całe słowo do/z pary rejestru wyniku (Z80/8080: HL/DE), bez zmiany A. Wymaga
/// <see cref="IResultReg.ReturnsInResultReg"/>; gdy brak tej zdolności, selektor przenosi słowo bajtami.</summary>
internal interface IResultPairs
{
    /// <summary>Rejestr wyniku ← słowo (stała, para rejestrów albo pamięć obok siebie), bez zmiany A.</summary>
    /// <param name="value">Wartość.</param>
    /// <returns><see langword="false"/>, gdy słowa nie da się przenieść parą (selektor przeniesie je bajtami przez A).</returns>
    bool TryMoveToResultReg(Word value);

    /// <summary>Słowo ← rejestr wyniku, bez zmiany A.</summary>
    /// <param name="dst">Cel (para rejestrów albo pamięć obok siebie).</param>
    /// <returns><see langword="false"/>, gdy celu nie da się zapisać parą.</returns>
    bool TryMoveFromResultReg(Word dst);
}
