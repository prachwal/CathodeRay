namespace CathodeRay.Abstractions;

/// <summary>Kontekst wywołania handlera opcode: słowo operacji, operand i adres.</summary>
/// <param name="Opcode">Pierwsze słowo (tu bajt).</param>
/// <param name="Operand">Drugie słowo (0, gdy instrukcja ma jedno słowo).</param>
/// <param name="Address">Adres pierwszego słowa.</param>
public readonly record struct OpcodeContext(int Opcode, int Operand, ushort Address);
