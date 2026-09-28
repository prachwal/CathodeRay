namespace CathodeRay.Assembler.Isa;

/// <summary>Zestaw instrukcji celu: formy pogrupowane po mnemoniku + kolejność bajtów.
/// Budowany przez moduł CPU (adapter danych ISA); rdzeń asemblera zna tylko ten typ.</summary>
public sealed class InstructionSet
{
    private readonly Dictionary<string, List<InstructionForm>> _forms = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Tworzy zestaw z form; przy duplikacie (mnemonik + wzorzec) wygrywa pierwsza forma,
    /// więc moduł CPU ustala preferowany opcode kolejnością.</summary>
    /// <param name="name">Nazwa celu (np. "6502").</param>
    /// <param name="endianness">Kolejność bajtów słów.</param>
    /// <param name="forms">Formy w kolejności preferencji.</param>
    public InstructionSet(string name, Endianness endianness, IEnumerable<InstructionForm> forms)
    {
        ArgumentNullException.ThrowIfNull(forms);
        Name = name;
        Endianness = endianness;
        foreach (InstructionForm form in forms)
        {
            form.Validate();
            if (!_forms.TryGetValue(form.Mnemonic, out List<InstructionForm>? list))
            {
                list = [];
                _forms.Add(form.Mnemonic, list);
            }

            if (!list.Exists(f => string.Equals(f.Pattern.Template, form.Pattern.Template, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(form);
            }
        }

        Patterns = [.. _forms.Values.SelectMany(static l => l).Select(static f => f.Pattern)
            .DistinctBy(static p => p.Template, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Nazwa celu.</summary>
    public string Name { get; }

    /// <summary>Kolejność bajtów słów.</summary>
    public Endianness Endianness { get; }

    /// <summary>Mnemoniki zestawu.</summary>
    public IEnumerable<string> Mnemonics => _forms.Keys;

    /// <summary>Wszystkie kształty operandów w zestawie (tryby adresowania CPU).</summary>
    public IReadOnlyList<OperandPattern> Patterns { get; }

    /// <summary>Formy mnemonika (pusta lista = nieznany mnemonik).</summary>
    /// <param name="mnemonic">Mnemonik (bez rozróżniania wielkości liter).</param>
    /// <returns>Formy.</returns>
    public IReadOnlyList<InstructionForm> FormsFor(string mnemonic) =>
        _forms.TryGetValue(mnemonic, out List<InstructionForm>? list) ? list : [];

    /// <summary>Czy mnemonik należy do zestawu.</summary>
    /// <param name="mnemonic">Mnemonik.</param>
    /// <returns>Czy istnieje.</returns>
    public bool Contains(string mnemonic) => _forms.ContainsKey(mnemonic);
}
