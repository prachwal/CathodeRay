using CathodeRay.Abstractions;

namespace CathodeRay.Stub;

/// <summary>Wpis tablicy opcode zaślepki: handler + metadane (mnemonic, cykle, słowa).</summary>
/// <param name="Handler">Handler wywoływany przez dispatch.</param>
/// <param name="Mnemonic">Mnemonik.</param>
/// <param name="Cycles">Bazowa liczba cykli.</param>
/// <param name="Words">Liczba słów (1–3).</param>
public sealed record StubOpcodeEntry(Action<OpcodeContext> Handler, string Mnemonic, int Cycles, int Words);
