namespace CathodeRay.Assembler.Isa;

/// <summary>Jedna forma instrukcji: mnemonik + kształt operandu → bajty opcode'u i pola.
/// Opcode jako tablica, żeby prefiksy (np. Z80 CB/DD/ED/FD) nie wymagały zmian w rdzeniu.</summary>
/// <param name="Mnemonic">Mnemonik (wielkie litery).</param>
/// <param name="Pattern">Kształt operandu.</param>
/// <param name="Opcode">Bajty opcode'u.</param>
public sealed record InstructionForm(string Mnemonic, OperandPattern Pattern, byte[] Opcode)
{
    /// <summary>Rozmiar instrukcji w bajtach.</summary>
    public int Size => Opcode.Length + Pattern.Fields.Sum(static f => f == FieldKind.Word ? 2 : 1);
}
