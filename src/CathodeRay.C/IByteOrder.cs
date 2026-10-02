namespace CathodeRay.C;

/// <summary>CPU trzyma bajty słowa w pamięci od najstarszego (6800).</summary>
internal interface IByteOrder
{
    /// <summary>Bajty słowa w pamięci od najstarszego.</summary>
    bool BigEndian { get; }
}
