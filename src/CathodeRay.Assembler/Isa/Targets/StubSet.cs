namespace CathodeRay.Assembler.Isa.Targets;

/// <summary>Moduł CPU zaślepki: tryb z <c>operands[0].type</c> (<c>immediate8</c>, <c>address16</c>, <c>address16_x</c>).
/// Wartość natychmiastowa bez prefiksu <c>#</c> (składnia stuba), więc mnemonik nie może mieć naraz trybu
/// natychmiastowego i adresu bez indeksu — operandu nie dałoby się rozróżnić.</summary>
public static class StubSet
{
    /// <summary>Buduje zestaw instrukcji z JSON.</summary>
    /// <param name="json">Strumień ISA JSON.</param>
    /// <returns>Zestaw instrukcji.</returns>
    /// <exception cref="InvalidDataException">Nieznany typ operandu lub niejednoznaczny mnemonik.</exception>
    public static InstructionSet Load(Stream json)
    {
        List<InstructionForm> forms = [.. IsaEntry.ReadAll(json).Select(Form)];
        IGrouping<string, InstructionForm>? ambiguous = forms
            .GroupBy(static f => f.Mnemonic, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static g => g.Any(static f => f.Pattern.Template == "{b}") && g.Any(static f => f.Pattern.Template == "{w}"));
        return ambiguous is null
            ? new InstructionSet("stub", Endianness.Little, forms)
            : throw new InvalidDataException(
                $"Mnemonic '{ambiguous.Key}' is ambiguous: immediate8 and address16 look the same without '#'.");
    }

    private static InstructionForm Form(IsaEntry entry) => new(
        entry.Mnemonic,
        OperandPattern.Parse(entry.OperandTypes.Count == 0 ? null : entry.OperandTypes[0] switch
        {
            "immediate8" => "{b}",
            "address16" => "{w}",
            "address16_x" => "{w},X",
            string other => throw new InvalidDataException($"{entry}: unknown operand type '{other}'."),
        }),
        entry.Opcode);
}
