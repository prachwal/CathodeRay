namespace CathodeRay.Assembler.Link;

/// <summary>Rodzaj relokacji pola (jak liczone w linkerze).</summary>
public enum RelocKind
{
    /// <summary>Absolutny bajt 0..255.</summary>
    Abs8,

    /// <summary>Absolutne słowo 0..65535 (kolejność bajtów wg CPU obiektu: <see cref="AssemblerTarget.Endianness"/>).</summary>
    Abs16,

    /// <summary>Ze znakiem −128..127 (np. Z80 <c>(IX+d)</c>).</summary>
    Disp8,

    /// <summary>Względny skok: cel − adres następnej instrukcji (−128..127).</summary>
    Rel8,

    /// <summary>Młodszy bajt adresu (<c>#&lt;sym</c>).</summary>
    Lo8,

    /// <summary>Starszy bajt adresu (<c>#&gt;sym</c>).</summary>
    Hi8,
}
