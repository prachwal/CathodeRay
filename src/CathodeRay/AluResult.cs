namespace CathodeRay;

/// <summary>Wynik operacji ALU 8-bit: wartość i flagi. Zapis do rejestru flag należy do rdzenia CPU.</summary>
/// <param name="Value">Wartość 8-bit.</param>
/// <param name="Carry">Przeniesienie (dla odejmowania: brak pożyczenia).</param>
/// <param name="Overflow">Nadmiar ze znaku.</param>
public readonly record struct AluResult(byte Value, bool Carry, bool Overflow)
{
    /// <summary>Czy wynik jest zerem.</summary>
    public bool Zero => Value == 0;

    /// <summary>Czy ustawiony bit 7 (znak).</summary>
    public bool Negative => (Value & 0x80) != 0;
}
