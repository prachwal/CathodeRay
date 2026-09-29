namespace CathodeRay.C;

/// <summary>Cel MOS 6502 (i 65C02 z tym samym kodem): komórki w pamięci absolutnej, wskaźniki przez parę na stronie
/// zerowej <c>(__p),Y</c>, argumenty w <c>cc_argN</c>, wołanie pośrednie przez <c>JMP (cc_fp)</c>.</summary>
public sealed class Mos6502Target : ByteTarget
{
    private readonly bool _cmos;

    /// <summary>Tworzy cel.</summary>
    /// <param name="cmos">65C02 zamiast NMOS.</param>
    public Mos6502Target(bool cmos = false) => _cmos = cmos;

    /// <inheritdoc/>
    public override string Name => _cmos ? "65c02" : "6502";

    /// <inheritdoc/>
    public override string Description => _cmos ? "WDC 65C02" : "MOS 6502";

    /// <inheritdoc/>
    public override string AssemblerCpu => Name;

    /// <inheritdoc/>
    public override TargetLayout Layout { get; } = new(
        [.. FlatLayout.Areas, new TargetArea("C_ZP", 0x0010, 0x00F0)],
        [.. FlatLayout.Segments, new TargetSegment("ZP", "C_ZP")]);

    /// <inheritdoc/>
    protected override IReadOnlyList<StdModule> AssemblyRuntime { get; } =
    [
        StdLib.TargetModule("6502", "rt_mul.s", "__cc_mul"),
        StdLib.TargetModule("6502", "rt_div.s", "__cc_divu", "__cc_modu"),
    ];

    /// <inheritdoc/>
    internal override ByteIsa CreateIsa() => new Mos6502Isa();

    /// <inheritdoc/>
    internal override Ir.Module Tune(Ir.Module module, ByteIsa isa)
    {
        (Ir.Module tuned, HashSet<string> names) = ZeroPageAllocator.Run(module);
        ((Mos6502Isa)isa).AddZeroPage(names.Select(isa.Sym));
        return tuned;
    }
}
