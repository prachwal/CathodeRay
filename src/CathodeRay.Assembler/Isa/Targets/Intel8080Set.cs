using System.Text.RegularExpressions;

namespace CathodeRay.Assembler.Isa.Targets;

/// <summary>Moduł CPU 8080: pole <c>mnemonic</c> jest szablonem składni Intel (<c>MVI B,d8</c>, <c>LXI H,d16</c>, <c>STA a16</c>).
/// <c>d8</c> → bajt, <c>d16</c>/<c>a16</c> → słowo, reszta (rejestry, <c>M</c>, <c>PSW</c>, numer RST) to literały.
/// Nieudokumentowane aliasy (np. NOP 08, JMP CB) przegrywają z kanonicznym, niższym opcode'em.</summary>
public static partial class Intel8080Set
{
    /// <summary>Buduje zestaw instrukcji z JSON.</summary>
    /// <param name="json">Strumień ISA JSON.</param>
    /// <returns>Zestaw instrukcji.</returns>
    public static InstructionSet Load(Stream json) =>
        new("8080", Endianness.Little, IsaEntry.ReadAll(json)
            .OrderBy(static e => e.Opcode[0])
            .Select(static e =>
            {
                string[] parts = e.Mnemonic.Split(' ', 2, StringSplitOptions.TrimEntries);
                string? template = parts.Length > 1 ? Placeholder().Replace(parts[1], static m => Slot(m.Value)) : null;
                var form = new InstructionForm(parts[0], OperandPattern.Parse(template), e.Opcode);
                return form.Size == e.Words ? form : throw new InvalidDataException($"{e}: template size {form.Size} != words {e.Words}.");
            }));

    private static string Slot(string placeholder) => placeholder == "d8" ? "{b}" : "{w}";

    [GeneratedRegex(@"\b(d8|d16|a16)\b")]
    private static partial Regex Placeholder();
}
