using System.Text.RegularExpressions;

namespace CathodeRay.Assembler.Isa.Targets;

/// <summary>Moduł CPU 6800: pole <c>mnemonic</c> jest szablonem składni Motoroli
/// (<c>LDAA #d8</c>, <c>LDX #d16</c>, <c>LDAA d8</c>, <c>STAA a16</c>, <c>LDAA d8,X</c>, <c>BRA rel</c>).
/// <c>#d8</c> → bajt natychmiastowy, <c>#d16</c> → słowo natychmiastowe (LDX/LDS/CPX),
/// <c>d8</c> → strona zerowa, <c>a16</c> → rozszerzony, <c>d8,X</c> → indeksowany, <c>rel</c> → względny.
/// Cel big-endian (6800).</summary>
public static partial class M6800Set
{
    /// <summary>Buduje zestaw instrukcji z JSON.</summary>
    /// <param name="json">Strumień ISA JSON.</param>
    /// <returns>Zestaw instrukcji.</returns>
    public static InstructionSet Load(Stream json) =>
        new("6800", Endianness.Big, IsaEntry.ReadAll(json)
            .OrderBy(static e => e.Opcode[0])
            .Select(static e =>
            {
                string[] parts = e.Mnemonic.Split(' ', 2, StringSplitOptions.TrimEntries);
                string? template = parts.Length > 1 ? Placeholder().Replace(parts[1], static m => Slot(m.Value)) : null;
                var form = new InstructionForm(parts[0], OperandPattern.Parse(template), e.Opcode);
                return form.Size == e.Words ? form : throw new InvalidDataException($"{e}: template size {form.Size} != words {e.Words}.");
            }));

    private static string Slot(string placeholder) => placeholder switch
    {
        "#d8" => "#{b}",
        "#d16" => "#{w}",
        "d8" => "{b}",
        "a16" => "{w}",
        "rel" => "{r}",
        _ => throw new InvalidDataException($"Unknown placeholder '{placeholder}'."),
    };

    [GeneratedRegex(@"#d16|#d8|\bd8\b|\ba16\b|\brel\b")]
    private static partial Regex Placeholder();
}
