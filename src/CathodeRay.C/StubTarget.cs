namespace CathodeRay.C;

/// <summary>Cel „stub”: minimalny procesor akumulatorowy (A, X, tylko adresowanie absolutne; wskaźniki przez
/// łatanie operandów). Dobór instrukcji: <see cref="StubSelector"/>; optymalizator okienkowy (<see cref="Peephole"/>) jest jego prywatnym przebiegiem.</summary>
public sealed class StubTarget : ICTarget
{
    /// <inheritdoc/>
    public string Name => "stub";

    /// <inheritdoc/>
    public string Description => "ISA zaślepki CathodeRay (akumulator A, indeks X)";

    /// <inheritdoc/>
    public string AssemblerCpu => "stub";

    /// <inheritdoc/>
    public TargetByteOrder ByteOrder => TargetByteOrder.Little;

    /// <inheritdoc/>
    public int? StackLimit => 256;

    /// <inheritdoc/>
    public TargetLayout Layout { get; } = new(
        [
            new TargetArea("C_CODE", 0x1000, 0x5F00),
            new TargetArea("C_INIT", 0x6F00, 0x100),
            new TargetArea("C_BSS", 0x7000, 0x1000),
            new TargetArea("C_DATA", 0x8000, 0x8000),
        ],
        [
            new TargetSegment("CODE", "C_CODE"),
            new TargetSegment("INIT", "C_INIT"),
            new TargetSegment("BSS", "C_BSS"),
            new TargetSegment("DATA", "C_DATA"),
        ]);

    /// <inheritdoc/>
    public string Crt0 => C.Crt0.Source;

    /// <inheritdoc/>
    public IReadOnlyList<StdModule> RuntimeModules => [.. StdLib.Modules.Where(static m => m.IsAssembly)];

    /// <inheritdoc/>
    public string Emit(Ir.Module module, bool optimize)
    {
        ArgumentNullException.ThrowIfNull(module);
        return new StubSelector(WideLegalizer.Run(Legalizer.Run(module, wide: true), ByteOrder)).Emit(optimize);
    }
}
