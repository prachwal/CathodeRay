namespace CathodeRay.C;

/// <summary>Cel Intel 8080: ten sam generator co Z80, ale tylko instrukcje 8080 (<see cref="Intel8080Isa"/>).</summary>
public sealed class Intel8080Target : ByteTarget
{
    /// <inheritdoc/>
    public override string Name => "8080";

    /// <inheritdoc/>
    public override string Description => "Intel 8080";

    /// <inheritdoc/>
    public override string AssemblerCpu => "8080";

    /// <inheritdoc/>
    public override int? StackLimit => 2048;

    /// <inheritdoc/>
    internal override ByteIsa CreateIsa() => new Intel8080Isa();

    /// <inheritdoc/>
    internal override Ir.Module Tune(Ir.Module module, ByteIsa isa)
    {
        isa.AssignRegisters(RegisterAllocator.Run(module, isa));
        return module;
    }
}
