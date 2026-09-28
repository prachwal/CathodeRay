using System.Globalization;
using System.Text.RegularExpressions;

namespace CathodeRay.Assembler.Isa.Targets;

/// <summary>Moduł CPU Z80: pole <c>mnemonic</c> to szablon składni Zilog (<c>LD A,(IX+d)</c>, <c>JR NZ,e</c>, <c>BIT 3,B</c>).
/// <c>nn</c> → słowo, <c>n</c> → bajt, <c>e</c> → skok względny, <c>(IX+d)</c> → <c>(IX{d})</c> (przesunięcie ze znakiem),
/// liczby w szablonie (numer bitu, tryb IM, adres RST <c>38H</c>) → stała <c>{c=N}</c>, reszta to literały.
/// DDCB/FDCB: przesunięcie przed ostatnim bajtem opcode'u (<c>DD CB d op</c>). Do każdej formy z <c>(IX+d)</c> dochodzi
/// forma <c>(IX)</c> z przesunięciem 0, jak w oryginalnych asemblerach.
/// Przy duplikatach (nieudokumentowane aliasy, np. <c>NEG</c> ED4C) wygrywa forma udokumentowana, potem niższy opcode.</summary>
public static partial class Z80Set
{
    /// <summary>Buduje zestaw instrukcji z JSON.</summary>
    /// <param name="json">Strumień ISA JSON Z80.</param>
    /// <param name="variant">Wariant zestawu.</param>
    /// <returns>Zestaw instrukcji.</returns>
    public static InstructionSet Load(Stream json, Z80Variant variant) =>
        new(variant == Z80Variant.Documented ? "z80" : "z80u", Endianness.Little, IsaEntry.ReadAll(json)
            .Where(e => variant == Z80Variant.Undocumented || !IsUndocumented(e))
            .OrderBy(static e => IsUndocumented(e))
            .ThenBy(static e => Convert.ToHexString(e.Opcode), StringComparer.Ordinal)
            .SelectMany(static e => Forms(e)));

    private static bool IsUndocumented(IsaEntry entry) => entry.Variants.Contains("undocumented", StringComparer.Ordinal);

    private static IEnumerable<InstructionForm> Forms(IsaEntry entry)
    {
        InstructionForm form = Form(entry);
        yield return form;
        int displacement = form.Pattern.Fields.ToList().IndexOf(FieldKind.Displacement8);
        if (displacement >= 0)
        {
            yield return WithoutDisplacement(form, displacement);
        }
    }

    /// <summary><c>(IX)</c> = <c>(IX+0)</c>: ten sam opcode, przesunięcie jako stały bajt 0 w układzie.</summary>
    private static InstructionForm WithoutDisplacement(InstructionForm form, int displacement)
    {
        EncodingPart[] layout = [.. form.Encoding.Select(p => !p.IsField ? p
            : p.Field == displacement ? EncodingPart.Byte(0)
            : EncodingPart.Slot(p.Field > displacement ? p.Field - 1 : p.Field))];
        string template = form.Pattern.Template.Replace("{d})", ")", StringComparison.Ordinal);
        return new InstructionForm(form.Mnemonic, OperandPattern.Parse(template), form.Opcode, layout);
    }

    private static InstructionForm Form(IsaEntry entry)
    {
        string[] parts = entry.Mnemonic.Split(' ', 2, StringSplitOptions.TrimEntries);
        string? template = parts.Length > 1 ? Placeholders(Constants(parts[1])) : null;
        OperandPattern pattern = OperandPattern.Parse(template);
        byte[] op = entry.Opcode;
        IReadOnlyList<EncodingPart>? layout = op.Length == 3 && op[1] == 0xCB
            ? [EncodingPart.Byte(op[0]), EncodingPart.Byte(op[1]), EncodingPart.Slot(IndexOf(pattern, FieldKind.Displacement8)), EncodingPart.Byte(op[2])]
            : null;
        var form = new InstructionForm(parts[0], pattern, op, layout);
        return form.Size == entry.Words
            ? form
            : throw new InvalidDataException($"{entry}: template size {form.Size} != words {entry.Words}.");
    }

    private static int IndexOf(OperandPattern pattern, FieldKind kind) =>
        pattern.Fields.Select((f, i) => (f, i)).First(x => x.f == kind).i;

    private static string Constants(string template) => Number().Replace(template, static m =>
    {
        string text = m.Value;
        int value = text.EndsWith('H')
            ? int.Parse(text.AsSpan(0, text.Length - 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : int.Parse(text, CultureInfo.InvariantCulture);
        return string.Create(CultureInfo.InvariantCulture, $"{{c={value}}}");
    });

    private static string Placeholders(string template) =>
        Word().Replace(Byte().Replace(Relative().Replace(Displacement().Replace(template, "{d})"), "{r}"), "{b}"), "{w}");

    [GeneratedRegex(@"\b(?:[0-9][0-9A-F]*H|[0-9]+)\b")]
    private static partial Regex Number();

    [GeneratedRegex(@"\+d\)")]
    private static partial Regex Displacement();

    [GeneratedRegex(@"\be\b")]
    private static partial Regex Relative();

    [GeneratedRegex(@"\bn\b")]
    private static partial Regex Byte();

    [GeneratedRegex(@"\bnn\b")]
    private static partial Regex Word();
}
