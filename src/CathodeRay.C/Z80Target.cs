namespace CathodeRay.C;

/// <summary>Cel Zilog Z80: generator bajtowy z rejestrem A i HL jako adresowym (zob. <see cref="Z80Isa"/>).</summary>
public sealed class Z80Target : ByteTarget
{
    /// <inheritdoc/>
    public override string Name => "z80";

    /// <inheritdoc/>
    public override string Description => "Zilog Z80";

    /// <inheritdoc/>
    public override string AssemblerCpu => "z80";

    /// <inheritdoc/>
    public override int? StackLimit => 2048;

    /// <inheritdoc/>
    internal override ByteIsa CreateIsa() => new Z80Isa();
}
