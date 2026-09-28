namespace CathodeRay.Assembler.Syntax;

/// <summary>Zapisy literałów liczbowych akceptowane przez dialekt (liczby dziesiętne zawsze).</summary>
[Flags]
public enum NumberFormats
{
    /// <summary>Tylko dziesiętne.</summary>
    Decimal = 0,

    /// <summary>Motorola/MOS: <c>$FF</c>, <c>%1010</c>.</summary>
    Motorola = 1,

    /// <summary>Intel: <c>0FFH</c>, <c>1010B</c>, <c>17Q</c>/<c>17O</c>, <c>12D</c>.</summary>
    Intel = 2,

    /// <summary>C: <c>0xFF</c>, <c>0b1010</c>.</summary>
    CStyle = 4,
}
