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

    /// <summary>Konsola w C (wspólna dla celów bajtowych).</summary>
    private static readonly StdModule IoModule =
        new("io.c", StdLib.Portable("io.c"), false, new HashSet<string>(["putchar", "puthex", "putdec"], StringComparer.Ordinal));

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

    /// <summary>Dwa bajty na każdą parę rejestrów komórek (<see cref="ByteIsa.CellPairs"/>), bo wokół wołania selektor odkłada
    /// najwyżej wszystkie pary (<see cref="CellMap.SavedAround"/>).</summary>
    public int CallSaveBytes => 2 * CreateIsa().CellPairs.Count;

    /// <summary>Cel obsługuje ABI v2 (domyślnie tylko v1).</summary>
    public virtual bool SupportsAbiV2 => false;

    /// <inheritdoc/>
    public virtual TargetLayout Layout => FlatLayout;

    /// <inheritdoc/>
    public string Crt0 => CreateIsa().Crt0();

    /// <inheritdoc/>
    public IReadOnlyList<StdModule> RuntimeModules => [.. AssemblyRuntime, IoModule, .. StdLib.RuntimeModules];

    /// <summary>Ręcznie pisane procedury wykonawcze celu; wygrywają z wersjami z C przy linkowaniu (stoją przed nimi).</summary>
    protected virtual IReadOnlyList<StdModule> AssemblyRuntime => [];

    /// <summary>Kod startowy z ABI wpisaną w świeżą instancję ISA (właściwość <c>Crt0</c> tworzy instancję dziewiczą
    /// z <c>AbiV2=false</c>, więc domyślne <c>Crt0For</c> z interfejsu gubiłoby flagę v2 i writeback wyniku).</summary>
    /// <param name="abiV2">Ścieżka v2.</param>
    /// <returns>Źródło dla asemblera.</returns>
    public string Crt0For(bool abiV2)
    {
        ByteIsa isa = CreateIsa();
        isa.AbiV2 = abiV2;
        return isa.Crt0();
    }

    /// <inheritdoc/>
    public string Emit(Ir.Module module, bool optimize, bool abiV2 = false)
    {
        ArgumentNullException.ThrowIfNull(module);
        Ir.Module wide = WideLegalizer.Run(Legalizer.Run(CaseFold.Apply(module), wide: true), ByteOrder, keepArithmetic: true);
        Ir.Module legal = Legalizer.Run(wide);
        ByteIsa isa = CreateIsa();
        isa.AbiV2 = abiV2;
        if (isa is IIndexed)
        {
            legal = IndexFusion.Run(legal);
        }

        if (optimize && ByteOrder == TargetByteOrder.Little)
        {
            // v2: parametry w rejestrach zostają w komórkach (alias tylko dla pamięciowych, np. arg2+);
            // inaczej prolog czytałby rejestry, a ciało komórki cc_argN.
            legal = abiV2
                ? ParamAlias.Run(legal, (function, i) => !IsRegParam(isa, function, i))
                : ParamAlias.Run(legal);
        }

        Ir.Module tuned = optimize ? Tune(legal, isa) : legal;
        return new ByteSelector(tuned, isa).Emit();
    }

    /// <summary>Dostosowanie modułu do CPU po legalizacji (np. przydział strony zerowej); domyślnie bez zmian.</summary>
    /// <param name="module">Moduł po legalizacji.</param>
    /// <param name="isa">Prymitywy, które mogą zapamiętać wybory.</param>
    /// <returns>Moduł przekazywany selektorowi.</returns>
    internal virtual Ir.Module Tune(Ir.Module module, ByteIsa isa) => module;

    /// <summary>Tworzy nowy zestaw prymitywów CPU (ma stan emisji).</summary>
    /// <returns>Prymitywy.</returns>
    internal abstract ByteIsa CreateIsa();

    /// <summary>Parametr funkcji jedzie rejestrem v2 (A/X na nes): nie wolno aliasować go na <c>cc_argN</c>.</summary>
    /// <param name="isa">Prymitywy celu (z wpisaną flagą ABI).</param>
    /// <param name="function">Funkcja.</param>
    /// <param name="index">Numer parametru.</param>
    /// <returns>Czy parametr idzie rejestrem.</returns>
    private static bool IsRegParam(ByteIsa isa, Ir.Function function, int index)
    {
        Ir.Cell param = function.Params[index];
        return param.W <= 2 && isa.Model.HasRegister(isa.ArgCell(index, 0, param.W));
    }
}
