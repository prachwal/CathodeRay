using CathodeRay.Abstractions;

namespace CathodeRay.Stub;

/// <summary>Wpis tablicy opcode zaślepki: metadane + operacja wbudowana (szybka ścieżka) albo handler (instrukcje własne).</summary>
/// <param name="Handler">Handler dla <see cref="StubOperation.Custom"/>; dla operacji wbudowanych <see langword="null"/>.</param>
/// <param name="Mnemonic">Mnemonik.</param>
/// <param name="Cycles">Bazowa liczba cykli.</param>
/// <param name="Words">Liczba słów (1–3).</param>
/// <param name="Operation">Operacja wbudowana lub <see cref="StubOperation.Custom"/>.</param>
/// <param name="Mode">Tryb adresowania (wyznacza wartość/adres efektywny dla operacji wbudowanych).</param>
public sealed record StubOpcodeEntry(
    Action<OpcodeContext>? Handler,
    string Mnemonic,
    int Cycles,
    int Words,
    StubOperation Operation = StubOperation.Custom,
    OperandMode Mode = OperandMode.None);
