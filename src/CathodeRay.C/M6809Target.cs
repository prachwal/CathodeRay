namespace CathodeRay.C;

/// <summary>Cel Motorola 6809: big-endian; ABI v1 domyślne, ABI v2 opt-in dla argumentów słownych w rejestrach.</summary>
public sealed class M6809Target : M6800Target
{
    private const string IndirectCallSource = """
        .global __icall
        .global cc_fp
        .segment "CODE"
        __icall: jsr [cc_fp]
                 rts
        .segment "ZP"
        cc_fp: .res 2
        """;

    private static readonly IReadOnlyList<StdModule> AssemblyModules =
    [
        new("icall.s", IndirectCallSource, true, new HashSet<string>(["__icall", "cc_fp"], StringComparer.Ordinal)),
        StdLib.TargetModule("6809", "setjmp.s", "setjmp", "longjmp"),
    ];

    /// <inheritdoc/>
    public override string Name => "6809";

    /// <inheritdoc/>
    public override string Description => "Motorola 6809";

    /// <inheritdoc/>
    public override string AssemblerCpu => "6809";

    /// <inheritdoc/>
    public override bool SupportsAbiV2 => true;

    /// <inheritdoc/>
    protected override IReadOnlyList<StdModule> AssemblyRuntime => AssemblyModules;

    internal override ByteIsa CreateIsa() => new M6809Isa();
}
