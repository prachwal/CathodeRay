namespace CathodeRay.C;

/// <summary>Cel Motorola 6800: generator bajtowy z akumulatorem A i rejestrem X, dane big-endian.</summary>
public sealed class M6800Target : ByteTarget
{
    /// <inheritdoc/>
    public override string Name => "6800";

    /// <inheritdoc/>
    public override string Description => "Motorola 6800";

    /// <inheritdoc/>
    public override string AssemblerCpu => "6800";

    /// <inheritdoc/>
    public override TargetByteOrder ByteOrder => TargetByteOrder.Big;

    /// <inheritdoc/>
    public override int? StackLimit => 2048;

    /// <inheritdoc/>
    internal override ByteIsa CreateIsa() => new M6800Isa();
}
