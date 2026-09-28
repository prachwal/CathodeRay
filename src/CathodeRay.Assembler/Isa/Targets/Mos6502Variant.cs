namespace CathodeRay.Assembler.Isa.Targets;

/// <summary>Wariant rodziny 6502 (odpowiednik <c>--cpu</c> w ca65).</summary>
public enum Mos6502Variant
{
    /// <summary>NMOS 6502, tylko udokumentowane instrukcje.</summary>
    Nmos,

    /// <summary>NMOS 6502 z nieudokumentowanymi instrukcjami (ca65 <c>6502X</c>).</summary>
    NmosIllegal,

    /// <summary>CMOS 65C02 (z rozszerzeniami WDC/Rockwell).</summary>
    Cmos,
}
