namespace CathodeRay.Assembler.Link;

/// <summary>Rodzaj relokacji pola (jak liczone w linkerze).</summary>
public enum RelocKind
{
    /// <summary>Absolutny bajt 0..255.</summary>
    Abs8,

    /// <summary>Absolutne słowo 0..65535 (little-endian celu).</summary>
    Abs16,

    /// <summary>Ze znakiem −128..127 (np. Z80 <c>(IX+d)</c>).</summary>
    Disp8,

    /// <summary>Względny skok: cel − adres następnej instrukcji (−128..127).</summary>
    Rel8,
}
