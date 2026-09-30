using System.Text.RegularExpressions;

namespace CathodeRay.Assembler.Isa.Targets;

/// <summary>Moduł CPU 6800: pole <c>mnemonic</c> jest szablonem składni Motoroli
/// (<c>LDAA #d8</c>, <c>LDX #d16</c>, <c>LDAA d8</c>, <c>STAA a16</c>, <c>LDAA d8,X</c>, <c>BRA rel</c>).
/// <c>#d8</c> → bajt natychmiastowy, <c>#d16</c> → słowo natychmiastowe (LDX/LDS/CPX),
/// <c>d8</c> → strona zerowa, <c>a16</c> → rozszerzony, <c>d8,X</c> → indeksowany, <c>rel</c> → względny.
/// Tryb bezpośredni ma też formę <c>z:{b}</c> (jak strona zerowa 6502): wymusza 2 bajty także dla symbolu relokowalnego.
/// Cel big-endian (6800).</summary>
public static partial class M6800Set
{
    /// <summary>Buduje zestaw instrukcji z JSON.</summary>
    /// <param name="json">Strumień ISA JSON.</param>
    /// <returns>Zestaw instrukcji.</returns>
    public static InstructionSet Load(Stream json) =>
        new("6800", Endianness.Big, IsaEntry.ReadAll(json)
            .OrderBy(static e => e.Opcode[0])
            .SelectMany(static e => Forms(e)));

    private static IEnumerable<InstructionForm> Forms(IsaEntry entry)
    {
        string[] parts = entry.Mnemonic.Split(' ', 2, StringSplitOptions.TrimEntries);
        string? template = parts.Length > 1 ? Placeholder().Replace(parts[1], static m => Slot(m.Value)) : null;
        string?[] templates = template == "{b}" ? [template, "z:{b}"] : [template];
        foreach (string? text in templates)
        {
            var form = new InstructionForm(parts[0], OperandPattern.Parse(text), entry.Opcode);
            yield return form.Size == entry.Words ? form : throw new InvalidDataException($"{entry}: template size {form.Size} != words {entry.Words}.");
        }
    }

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
