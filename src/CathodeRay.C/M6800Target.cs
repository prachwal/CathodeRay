namespace CathodeRay.C;

/// <summary>Cel Motorola 6800: generator bajtowy z akumulatorem A i rejestrem X, dane big-endian. Najczęściej używane komórki
/// i komórki umówione (<c>cc_arg1..3</c>, <c>cc_ret</c>, <c>cc_t*</c>) leżą na stronie bezpośredniej (<c>$0010..$00FF</c>, poniżej stosu
/// od <c>$0FFF</c> w dół), więc dostęp do nich ma 2 bajty zamiast 3.</summary>
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
    public override TargetLayout Layout { get; } = new(
        [.. FlatLayout.Areas, new TargetArea("C_ZP", 0x0010, 0x00F0)],
        [.. FlatLayout.Segments, new TargetSegment("ZP", "C_ZP")]);

    /// <inheritdoc/>
    internal override ByteIsa CreateIsa() => new M6800Isa();

    /// <inheritdoc/>
    internal override Ir.Module Tune(Ir.Module module, ByteIsa isa)
    {
        (Ir.Module tuned, HashSet<string> names) = ZeroPageAllocator.Run(module);
        ((M6800Isa)isa).AddZeroPage(names.Select(isa.Sym));
        return tuned;
    }
}
