namespace CathodeRay.Stub;

/// <summary>Opcode zaślepki wczytany z JSON: mnemonic + koszt cykli + liczba słów.</summary>
/// <param name="Mnemonic">Mnemonic — klucz handlera w <see cref="StubCpu"/>.</param>
/// <param name="Cycles">Bazowa liczba cykli zwracana przez <see cref="StubCpu.Step"/>.</param>
/// <param name="Words">Liczba słów (bajtów) instrukcji: 1–3.</param>
public sealed record StubOpcode(string Mnemonic, int Cycles, int Words);
