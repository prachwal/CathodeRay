namespace CathodeRay.C;

/// <summary>Wspólna część celów akumulatorowych generowanych przez <see cref="ByteSelector"/>: układ pamięci jak w
/// stubie (płaskie 64 KB), konsola z <c>stdlib/portable/io.c</c>, mnożenie i dzielenie z <c>rt.c</c>.</summary>
public abstract class ByteTarget : ICTarget
{
    /// <summary>Płaski układ 64 KB (kod, INIT, BSS, dane).</summary>
    protected static readonly TargetLayout FlatLayout = new(
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
    public abstract string Name { get; }

    /// <inheritdoc/>
    public abstract string Description { get; }

    /// <inheritdoc/>
    public abstract string AssemblerCpu { get; }

    /// <inheritdoc/>
    public virtual TargetByteOrder ByteOrder => TargetByteOrder.Little;

    /// <inheritdoc/>
    public virtual int? StackLimit => 256;

    /// <inheritdoc/>
    public virtual TargetLayout Layout => FlatLayout;

    /// <inheritdoc/>
    public string Crt0 => CreateIsa().Crt0();

    /// <inheritdoc/>
    public IReadOnlyList<StdModule> RuntimeModules { get; } =
    [
        new("io.c", StdLib.Portable("io.c"), false, new HashSet<string>(["putchar", "puthex", "putdec"], StringComparer.Ordinal)),
    ];

    /// <inheritdoc/>
    public string Emit(Ir.Module module, bool optimize)
    {
        ArgumentNullException.ThrowIfNull(module);
        Ir.Module wide = WideLegalizer.Run(Legalizer.Run(CaseFold.Apply(module), wide: true), ByteOrder);
        Ir.Module legal = Legalizer.Run(wide);
        ByteIsa isa = CreateIsa();
        return new ByteSelector(Tune(legal, isa), isa).Emit();
    }

    /// <summary>Dostosowanie modułu do CPU po legalizacji (np. przydział strony zerowej); domyślnie bez zmian.</summary>
    /// <param name="module">Moduł po legalizacji.</param>
    /// <param name="isa">Prymitywy, które mogą zapamiętać wybory.</param>
    /// <returns>Moduł przekazywany selektorowi.</returns>
    internal virtual Ir.Module Tune(Ir.Module module, ByteIsa isa) => module;

    /// <summary>Tworzy nowy zestaw prymitywów CPU (ma stan emisji).</summary>
    /// <returns>Prymitywy.</returns>
    internal abstract ByteIsa CreateIsa();
}
