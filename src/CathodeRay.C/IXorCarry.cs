namespace CathodeRay.C;

/// <summary>Xor (i or/and) nie rusza przeniesienia (6502/6800): bias najstarszego bajtu może iść wprost między
/// odejmowanie młodszych bajtów a sbc.</summary>
internal interface IXorCarry
{
    /// <summary>Xor (i or/and) nie rusza przeniesienia.</summary>
    bool XorPreservesCarry { get; }
}
