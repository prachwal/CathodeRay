namespace CathodeRay.Assembler.Isa.Targets;

/// <summary>Wariant zestawu instrukcji Z80.</summary>
public enum Z80Variant
{
    /// <summary>Tylko instrukcje udokumentowane przez Zilog.</summary>
    Documented,

    /// <summary>Z instrukcjami nieudokumentowanymi (IXH/IXL, SLL, OUT (C),0, warianty DDCB z kopią do rejestru).</summary>
    Undocumented,
}
