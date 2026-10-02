namespace CathodeRay.C;

/// <summary>CPU ma składnię bajtu adresu symbolu jako stałej asemblera (relokacje Lo8/Hi8), np. 6502 <c>&lt;(expr)</c>/<c>&gt;(expr)</c>.</summary>
internal interface IAddressByte
{
    /// <summary>Bajt adresu symbolu jako stała asemblera (relokacje Lo8/Hi8) albo <see langword="null"/>, gdy CPU nie ma
    /// takiej składni (wtedy adres leży w komórce danych).</summary>
    /// <param name="expression">Wyrażenie adresu (symbol ± stała).</param>
    /// <param name="index">0 = młodszy, 1 = starszy.</param>
    /// <returns>Tekst stałej albo null.</returns>
    string? AddressByte(string expression, int index);
}
