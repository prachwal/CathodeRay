namespace CathodeRay.C;

/// <summary>Kolejność bajtów słowa 16-bitowego w pamięci celu.</summary>
public enum TargetByteOrder
{
    /// <summary>Młodszy bajt pod niższym adresem (stub, 6502, 8080, Z80).</summary>
    Little,

    /// <summary>Starszy bajt pod niższym adresem (6800).</summary>
    Big,
}
