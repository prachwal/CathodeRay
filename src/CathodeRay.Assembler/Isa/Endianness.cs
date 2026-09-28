namespace CathodeRay.Assembler.Isa;

/// <summary>Kolejność bajtów słów w kodzie maszynowym.</summary>
public enum Endianness
{
    /// <summary>Młodszy bajt pierwszy (6502, 8080, Z80).</summary>
    Little,

    /// <summary>Starszy bajt pierwszy (6800).</summary>
    Big,
}
