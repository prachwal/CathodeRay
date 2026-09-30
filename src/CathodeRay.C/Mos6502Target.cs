namespace CathodeRay.C;

/// <summary>Cel MOS 6502 (i warianty: 65C02, NES, 6510 — ten sam selektor): komórki w pamięci absolutnej, wskaźniki przez parę na stronie
/// zerowej <c>(__p),Y</c>, argumenty w <c>cc_argN</c>, wołanie pośrednie przez <c>JMP (cc_fp)</c>. Selektor nigdy nie używa trybu
/// dziesiętnego, więc 2A03 (NES, ignoruje flagę D) bierze ten sam kod; strona zerowa zaczyna się od $10, więc port I/O 6510 ($00/$01) jest wolny.</summary>
public sealed class Mos6502Target : ByteTarget
{
    private readonly bool _cmos;

    private readonly bool _nes;

    private readonly bool _ioPort;

    /// <summary>Tworzy cel.</summary>
    /// <param name="cmos">65C02 zamiast NMOS.</param>
    /// <param name="nes">NES (2A03): jak NMOS, nazwa <c>nes</c>.</param>
    /// <param name="ioPort">6510 (C64): jak NMOS, nazwa <c>6510</c>.</param>
    public Mos6502Target(bool cmos = false, bool nes = false, bool ioPort = false)
    {
        if (nes && cmos)
        {
            throw new ArgumentException("NES to NMOS bez BCD, nie 65C02.");
        }

        _cmos = cmos;
        _nes = nes;
        _ioPort = ioPort;
    }

    /// <inheritdoc/>
    public override string Name => _nes ? "nes" : _ioPort ? "6510" : _cmos ? "65c02" : "6502";

    /// <inheritdoc/>
    public override string Description => _nes ? "Nintendo NES (2A03, 6502 bez BCD)"
        : _ioPort ? "MOS 6510 (C64, port I/O $00/$01)"
        : _cmos ? "WDC 65C02" : "MOS 6502";

    /// <inheritdoc/>
    public override string AssemblerCpu => (_nes || _ioPort) ? "6502" : Name;

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
