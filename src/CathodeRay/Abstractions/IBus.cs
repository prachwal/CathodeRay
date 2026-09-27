namespace CathodeRay.Abstractions;

/// <summary>Szyna pamięci: jedyna droga CPU do pamięci i urządzeń memory-mapped (CPU szynę dostaje, nigdy jej nie tworzy).</summary>
public interface IBus
{
    /// <summary>Odczyt bajtu spod adresu; zachowanie dla adresów niezamapowanych definiuje implementacja (maszyna).</summary>
    /// <param name="address">Adres (istotne bity zależą od maszyny).</param>
    /// <returns>Bajt spod adresu.</returns>
    byte Read(ushort address);

    /// <summary>Zapis bajtu pod adres; zachowanie dla adresów niezamapowanych definiuje implementacja (maszyna).</summary>
    /// <param name="address">Adres.</param>
    /// <param name="value">Zapisywany bajt.</param>
    void Write(ushort address, byte value);
}
