namespace CathodeRay.C;

/// <summary>Cel Motorola 6809: mały wariant big-endian modelowany na 6800.</summary>
public sealed class M6809Target : M6800Target
{
    /// <inheritdoc/>
    public override string Name => "6809";

    /// <inheritdoc/>
    public override string Description => "Motorola 6809";

    /// <inheritdoc/>
    public override string AssemblerCpu => "6809";

    internal override ByteIsa CreateIsa() => new M6809Isa();
}
