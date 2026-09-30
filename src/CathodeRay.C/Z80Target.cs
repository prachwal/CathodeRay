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
    protected override IReadOnlyList<StdModule> AssemblyRuntime { get; } =
    [
        StdLib.TargetModule("z80", "rt_mul.s", "__cc_mul"),
        StdLib.TargetModule("z80", "rt_div.s", "__cc_divu", "__cc_modu"),
    ];

    /// <inheritdoc/>
    internal override ByteIsa CreateIsa() => new Z80Isa();

    /// <inheritdoc/>
    internal override Ir.Module Tune(Ir.Module module, ByteIsa isa)
    {
        ((Z80Isa)isa).AssignRegisters(RegisterAllocator.Run(module));
        return module;
    }
}
