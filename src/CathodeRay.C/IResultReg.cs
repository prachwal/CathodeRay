namespace CathodeRay.C;

/// <summary>CPU zwraca wynik funkcji o szerokości 1-2 bajtów w rejestrze (Z80/8080: HL; 6502: A/X na ścieżce v2), nie w
/// <c>cc_ret</c>, i umie przenosić pojedyncze bajty do/z tego rejestru. Pary rejestru wyniku obsługuje <c>IResultPairs</c>.</summary>
internal interface IResultReg
{
    /// <summary>Wynik funkcji o szerokości 1 lub 2 wraca w rejestrze CPU (Z80/8080: HL, dla 1 bajtu L), nie w <c>cc_ret</c>;
    /// crt0 po <c>call main</c> zapisuje go do <c>cc_ret</c>.</summary>
    bool ReturnsInResultReg { get; }

    /// <summary>Bajt rejestru wyniku ← A.</summary>
    /// <param name="index">0 = młodszy, 1 = starszy.</param>
    void ResultByteFromA(int index);

    /// <summary>A ← bajt rejestru wyniku.</summary>
    /// <param name="index">0 = młodszy, 1 = starszy.</param>
    void ResultByteToA(int index);
}
