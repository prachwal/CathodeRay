namespace CathodeRay.C;

/// <summary>Interpreter modułów VReg: wyrocznia semantyki ścieżki wirtualnych rejestrów. Wykonuje przez normalizację
/// do Cell IR (<see cref="VRegToCell"/>) w <see cref="IrInterpreter"/>, więc ten sam program liczony obiema ścieżkami
/// musi dać ten sam wynik; przebiegi VReg nie mogą zmienić obserwowanego zachowania.</summary>
public sealed class VRegInterpreter
{
    private readonly IrInterpreter _inner;

    /// <summary>Utworzony przez <see cref="Load"/>.</summary>
    private VRegInterpreter(IrInterpreter inner) => _inner = inner;

    /// <summary>Wypisane znaki konsoli.</summary>
    public string Console => _inner.Console;

    /// <summary>Liczba wykonanych instrukcji.</summary>
    public long Steps => _inner.Steps;

    /// <summary>Ładuje moduły VReg (przez Cell IR).</summary>
    /// <param name="modules">Moduły programu; funkcja <c>main</c> w którymś z nich.</param>
    /// <param name="stepLimit">Limit instrukcji.</param>
    /// <returns>Gotowy do uruchomienia interpreter.</returns>
    public static VRegInterpreter Load(IReadOnlyList<VReg.Module> modules, long stepLimit = 20_000_000)
    {
        ArgumentNullException.ThrowIfNull(modules);
        return new VRegInterpreter(IrInterpreter.Load([.. modules.Select(VRegToCell.Run)], stepLimit));
    }

    /// <summary>Czyta bajty pamięci pod adresem wyeksportowanego symbolu.</summary>
    /// <param name="symbol">Symbol wyeksportowany.</param>
    /// <param name="size">Liczba bajtów.</param>
    /// <returns>Bajty.</returns>
    public byte[] Peek(string symbol, int size) => _inner.Peek(symbol, size);

    /// <summary>Uruchamia inicjalizatory globali, potem <c>main</c>.</summary>
    /// <returns>Wynik <c>main</c> (młodsze 16 bitów) oraz szerokość wyniku (0 = void).</returns>
    public (int Value, int Width) RunMain() => _inner.RunMain();
}
