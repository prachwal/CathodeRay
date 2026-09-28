namespace CathodeRay.Assembler.Isa.Targets;

/// <summary>Moduł CPU rodziny 6502: tryb adresowania z pola <c>encoding</c> → wzorzec operandu w składni MOS.
/// Nazwy nieudokumentowanych instrukcji i wybór opcode'u przy duplikatach jak w ca65.</summary>
public static class Mos6502Set
{
    private static readonly Dictionary<string, string[]> Patterns = new(StringComparer.Ordinal)
    {
        ["Implied"] = [string.Empty],
        ["Accumulator"] = [string.Empty, "A"],
        ["Immediate"] = ["#{b}"],
        ["ZeroPage"] = ["{b}"],
        ["ZeroPageX"] = ["{b},X"],
        ["ZeroPageY"] = ["{b},Y"],
        ["Absolute"] = ["{w}"],
        ["AbsoluteX"] = ["{w},X"],
        ["AbsoluteY"] = ["{w},Y"],
        ["Indirect"] = ["({w})"],
        ["IndirectX"] = ["({b},X)"],
        ["IndirectY"] = ["({b}),Y"],
        ["ZeroPageIndirect"] = ["({b})"],
        ["AbsoluteIndexedIndirect"] = ["({w},X)"],
        ["Relative"] = ["{r}"],
        ["ZeroPageRelative"] = ["{b},{r}"],
    };

    private static readonly HashSet<string> Documented = new(
        "ADC AND ASL BCC BCS BEQ BIT BMI BNE BPL BRK BVC BVS CLC CLD CLI CLV CMP CPX CPY DEC DEX DEY EOR INC INX INY JMP JSR LDA LDX LDY LSR NOP ORA PHA PHP PLA PLP ROL ROR RTI RTS SBC SEC SED SEI STA STX STY TAX TAY TSX TXA TXS TYA"
            .Split(' '),
        StringComparer.Ordinal);

    private static readonly Dictionary<string, string> Ca65Names = new(StringComparer.Ordinal)
    {
        ["KIL"] = "JAM",
        ["XAA"] = "ANE",
    };

    private static readonly HashSet<string> UnknownToCa65 = new(["LXA", "USBC"], StringComparer.Ordinal);

    /// <summary>Buduje zestaw instrukcji z JSON.</summary>
    /// <param name="json">Strumień ISA JSON (6502 lub 65C02).</param>
    /// <param name="variant">Wariant CPU.</param>
    /// <returns>Zestaw instrukcji.</returns>
    public static InstructionSet Load(Stream json, Mos6502Variant variant) =>
        new(Name(variant), Endianness.Little, IsaEntry.ReadAll(json)
            .Where(e => IsAvailable(e, variant))
            .OrderBy(static e => e.Mnemonic == "NOP" && e.Opcode[0] == 0xEA ? -1 : e.Opcode[0])
            .SelectMany(static e => Forms(e)));

    private static string Name(Mos6502Variant variant) => variant switch
    {
        Mos6502Variant.Nmos => "6502",
        Mos6502Variant.NmosIllegal => "6502x",
        _ => "65c02",
    };

    private static bool IsAvailable(IsaEntry entry, Mos6502Variant variant) => variant switch
    {
        Mos6502Variant.Nmos => Documented.Contains(entry.Mnemonic) && !IsUndocumentedDuplicate(entry),
        Mos6502Variant.Cmos => entry.Mnemonic != "NOP" || entry.Opcode[0] == 0xEA,
        _ => !UnknownToCa65.Contains(entry.Mnemonic),
    };

    private static bool IsUndocumentedDuplicate(IsaEntry entry) =>
        (entry.Mnemonic == "NOP" && entry.Opcode[0] != 0xEA) || entry.Opcode[0] == 0xEB;

    private static IEnumerable<InstructionForm> Forms(IsaEntry entry)
    {
        string mode = entry.Opcode[0] == 0x7C && entry.Mnemonic == "JMP" ? "AbsoluteIndexedIndirect" : entry.Encoding ?? "Implied";
        if (!Patterns.TryGetValue(mode, out string[]? templates))
        {
            throw new InvalidDataException($"{entry}: unknown addressing mode '{mode}'.");
        }

        string mnemonic = Ca65Names.GetValueOrDefault(entry.Mnemonic, entry.Mnemonic);
        foreach (string template in templates)
        {
            var form = new InstructionForm(mnemonic, OperandPattern.Parse(template), entry.Opcode);
            if (form.Size != entry.Words && mode != "Implied")
            {
                throw new InvalidDataException($"{entry}: {mode} is {form.Size} bytes, data says {entry.Words}.");
            }

            yield return form;
        }
    }
}
