namespace CathodeRay.Assembler.Isa;

/// <summary>Wybór formy instrukcji dla operandu, jak w oryginalnych asemblerach:
/// 1) najbardziej szczegółowy wzorzec (najwięcej literałów), więc <c>($10),Y</c> wygrywa z <c>{w}</c>; dopiero wśród nich
///    stałe <c>{c=N}</c> wybierają opcode (<c>OUT (C),B</c> to literał, a nie <c>OUT (C),{c=0}</c> z symbolem B);
/// 2) najkrótsza forma, w której wartości mieszczą się w polach (zero page przed absolute);
/// 3) wartość jeszcze nieznana (odwołanie w przód) → najdłuższa forma; rozmiar nie zmienia się w drugim przebiegu.
/// Operand w kształcie trybu, który CPU ma, a ta instrukcja nie (np. <c>LDA ($12)</c> na NMOS 6502), to błąd,
/// a nie wyrażenie w nawiasach — jak w oryginalnych asemblerach.</summary>
internal static class FormSelector
{
    public static FormChoice Select(InstructionSet isa, string mnemonic, string? operand, Func<string, int?> evaluate)
    {
        IReadOnlyList<InstructionForm> forms = isa.FormsFor(mnemonic);
        var matches = new List<FormChoice>();
        foreach (InstructionForm form in forms)
        {
            if (form.Pattern.TryMatch(operand, out string[] captures))
            {
                matches.Add(new FormChoice(form, captures));
            }
        }

        int weight = matches.Count > 0 ? matches.Max(static m => m.Form.Pattern.LiteralWeight) : -1;
        OperandPattern? shape = isa.Patterns
            .Where(p => p.LiteralWeight > weight && p.TryMatch(operand, out _))
            .MaxBy(static p => p.LiteralWeight);
        if (shape is not null && operand is not null && forms.Any(static f => f.Pattern.Template.Length > 0))
        {
            throw new FormatException($"addressing mode {shape.Template} is not available for {mnemonic}.");
        }

        if (matches.Count == 0)
        {
            throw new FormatException(NoMatch(mnemonic, forms, operand));
        }

        List<FormChoice> candidates = FilterByConstants(
            mnemonic, [.. matches.Where(m => m.Form.Pattern.LiteralWeight == weight).OrderBy(static m => m.Form.Size)], evaluate);
        foreach (FormChoice candidate in candidates)
        {
            int?[] values = [.. candidate.Captures.Select(evaluate)];
            if (Array.Exists(values, static v => v is null))
            {
                return candidates[^1];
            }

            if (Fits(candidate.Form.Pattern.Fields, values))
            {
                return candidate;
            }
        }

        return candidates[^1];
    }

    private static List<FormChoice> FilterByConstants(string mnemonic, List<FormChoice> matches, Func<string, int?> evaluate)
    {
        List<FormChoice> kept = [.. matches.Where(m => ConstantsMatch(mnemonic, m, evaluate))];
        if (kept.Count > 0 || matches.Count == 0)
        {
            return kept;
        }

        string allowed = string.Join(", ", matches.SelectMany(static m => m.Form.Pattern.Constants).OfType<int>().Distinct());
        throw new FormatException($"invalid operand '{string.Join(",", matches[0].Captures)}' for {mnemonic} (allowed constants: {allowed}).");
    }

    private static bool ConstantsMatch(string mnemonic, FormChoice match, Func<string, int?> evaluate) =>
        match.Form.Pattern.Constants.Select((required, i) => required is null
            || (evaluate(match.Captures[i]) ?? throw new FormatException(
                $"'{match.Captures[i]}' must be known at this point (it selects the {mnemonic} opcode).")) == required)
            .All(static ok => ok);

    private static bool Fits(IReadOnlyList<FieldKind> fields, int?[] values) =>
        fields.Select((field, i) => field switch
        {
            FieldKind.Byte => values[i] is >= 0 and <= byte.MaxValue,
            FieldKind.Word => values[i] is >= 0 and <= ushort.MaxValue,
            FieldKind.Displacement8 => values[i] is >= sbyte.MinValue and <= sbyte.MaxValue,
            _ => true,
        }).All(static fits => fits);

    private static string NoMatch(string mnemonic, IReadOnlyList<InstructionForm> forms, string? operand)
    {
        if (operand is null)
        {
            return $"{mnemonic} requires an operand.";
        }

        if (forms.All(static f => f.Pattern.Template.Length == 0))
        {
            return $"{mnemonic} takes no operand.";
        }

        string expected = string.Join(", ", forms.Select(static f => f.Pattern.Template.Length == 0 ? "(none)" : f.Pattern.Template));
        return $"invalid operand '{operand}' for {mnemonic} (expected: {expected}).";
    }
}
