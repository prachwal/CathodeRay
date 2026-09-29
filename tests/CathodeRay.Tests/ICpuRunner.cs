namespace CathodeRay.Tests;

/// <summary>Uruchamia obraz programu na interpreterze procesora celu: ładowanie, krokowanie, odczyt pamięci.
/// Wynik <c>main</c> czytamy z pamięci (<c>cc_ret</c>/<c>cc_ret_h</c>), więc test nie zależy od rejestrów CPU.</summary>
public interface ICpuRunner
{
    /// <summary>Procesor się zatrzymał (HLT / pętla w miejscu / rozkaz zatrzymania).</summary>
    bool Halted { get; }

    /// <summary>Ładuje bajty od adresu.</summary>
    /// <param name="address">Adres początku.</param>
    /// <param name="bytes">Zawartość.</param>
    void Load(int address, byte[] bytes);

    /// <summary>Ustawia licznik programu.</summary>
    /// <param name="address">Adres startu.</param>
    void Start(int address);

    /// <summary>Wykonuje jedną instrukcję.</summary>
    void Step();

    /// <summary>Czyta bajt pamięci.</summary>
    /// <param name="address">Adres.</param>
    /// <returns>Wartość.</returns>
    int Read(int address);
}
