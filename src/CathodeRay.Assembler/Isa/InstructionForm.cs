namespace CathodeRay.Assembler.Isa;

/// <summary>Jedna forma instrukcji: mnemonik + kształt operandu → bajty opcode'u i pola.
/// Domyślny układ: bajty opcode'u, potem pola w kolejności wzorca. Własny <paramref name="Layout"/> pozwala wstawić pole
/// w środek opcode'u (Z80 <c>DD CB d 06</c>) albo dodać stały bajt (<c>(IX)</c> = przesunięcie 0), bez zmian w rdzeniu.</summary>
/// <param name="Mnemonic">Mnemonik (wielkie litery).</param>
/// <param name="Pattern">Kształt operandu.</param>
/// <param name="Opcode">Bajty opcode'u.</param>
/// <param name="Layout">Układ bajtów instrukcji lub <see langword="null"/> dla domyślnego.</param>
public sealed record InstructionForm(string Mnemonic, OperandPattern Pattern, byte[] Opcode, IReadOnlyList<EncodingPart>? Layout = null)
{
    /// <summary>Układ bajtów instrukcji (własny albo domyślny). Pola stałych <see cref="FieldKind.Constant"/> w nim nie występują.</summary>
    public IReadOnlyList<EncodingPart> Encoding => Layout ??
    [
        .. Opcode.Select(EncodingPart.Byte),
        .. Pattern.Fields.Select(static (kind, i) => (kind, i)).Where(static f => f.kind != FieldKind.Constant).Select(static f => EncodingPart.Slot(f.i)),
    ];

    /// <summary>Rozmiar instrukcji w bajtach.</summary>
    public int Size => Encoding.Sum(p => p.IsField && Pattern.Fields[p.Field] == FieldKind.Word ? 2 : 1);

    /// <summary>Sprawdza, że układ odwołuje się do każdego pola (poza stałymi) dokładnie raz.</summary>
    /// <exception cref="InvalidDataException">Brakujące, powtórzone lub niepoprawne pole.</exception>
    public void Validate()
    {
        int[] referenced = [.. Encoding.Where(static p => p.IsField).Select(static p => p.Field).Order()];
        int[] expected = [.. Pattern.Fields.Select(static (kind, i) => (kind, i)).Where(static f => f.kind != FieldKind.Constant).Select(static f => f.i)];
        if (!referenced.SequenceEqual(expected))
        {
            throw new InvalidDataException(
                $"{Mnemonic} {Pattern}: layout references fields [{string.Join(",", referenced)}], expected [{string.Join(",", expected)}].");
        }
    }
}
