namespace CathodeRay.Assembler.Isa.Targets;

/// <summary>Moduł ISA Motorola 6809: słowa big-endian, prefiksy opcode'u oraz adresowanie indexed z postbyte.</summary>
public static class M6809Set
{
    private static readonly (string Register, byte Bits, FieldKind FiveBit)[] IndexRegisters =
    [
        ("X", 0x00, FieldKind.Indexed5X),
        ("Y", 0x20, FieldKind.Indexed5Y),
        ("U", 0x40, FieldKind.Indexed5U),
        ("S", 0x60, FieldKind.Indexed5S),
    ];

    private static readonly Dictionary<string, int> RegisterCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["D"] = 0x0,
        ["X"] = 0x1,
        ["Y"] = 0x2,
        ["U"] = 0x3,
        ["S"] = 0x4,
        ["PC"] = 0x5,
        ["A"] = 0x8,
        ["B"] = 0x9,
        ["CC"] = 0xA,
        ["DP"] = 0xB,
    };

    /// <summary>Buduje zestaw instrukcji z tabeli ISA JSON.</summary>
    /// <param name="json">Plik ISA M6809.</param>
    /// <returns>Zestaw instrukcji.</returns>
    public static InstructionSet Load(Stream json) =>
        new("6809", Endianness.Big, IsaEntry.ReadAll(json).SelectMany(Forms));

    private static IEnumerable<InstructionForm> Forms(IsaEntry entry)
    {
        string[] parts = entry.Mnemonic.Split(' ', 2, StringSplitOptions.TrimEntries);
        string mnemonic = parts[0];
        string mode = entry.Encoding ?? throw new InvalidDataException($"{entry}: missing encoding mode.");
        if (mode == "indexed")
        {
            InstructionForm[] forms = IndexedForms(mnemonic, entry.Opcode).ToArray();
            if (!forms.Any(f => f.Size == entry.Words))
            {
                throw new InvalidDataException($"{entry}: no indexed form has the declared minimum size.");
            }

            foreach (InstructionForm indexed in forms)
            {
                yield return indexed;
            }

            yield break;
        }

        OperandPattern pattern = mode switch
        {
            "inherent" => OperandPattern.Parse(null),
            "direct" => OperandPattern.Parse("{b}"),
            "extended" => OperandPattern.Parse("{w}"),
            "immediate8" => OperandPattern.Parse("#{b}"),
            "immediate16" => OperandPattern.Parse("#{w}"),
            "relative8" => OperandPattern.Parse("{r}"),
            "relative16" => OperandPattern.Parse("{r16}"),
            "registerpair" => OperandPattern.EncodedByte("register-pair", EncodeRegisterPair),
            "registermask" => OperandPattern.EncodedByte("register-mask", operand => EncodeRegisterMask(mnemonic, operand)),
            _ => throw new InvalidDataException($"{entry}: unknown encoding mode '{mode}'."),
        };

        var form = new InstructionForm(mnemonic, pattern, entry.Opcode);
        if (form.Size != entry.Words)
        {
            throw new InvalidDataException($"{entry}: template size {form.Size} != words {entry.Words}.");
        }

        yield return form;
        if (mode == "direct")
        {
            var forced = new InstructionForm(mnemonic, OperandPattern.Parse("z:{b}"), entry.Opcode);
            yield return forced;
        }
    }

    private static IEnumerable<InstructionForm> IndexedForms(string mnemonic, byte[] opcode)
    {
        foreach ((string register, byte bits, FieldKind fiveBit) in IndexRegisters)
        {
            yield return FixedPostbyte(mnemonic, $",{register}", opcode, (byte)(0x84 | bits));
            yield return FixedPostbyte(mnemonic, $"[,{register}]", opcode, (byte)(0x94 | bits));

            yield return FieldPostbyte(mnemonic, $"{{i5{register.ToLowerInvariant()}}},{register}", opcode, fiveBit, null);
            yield return FieldPostbyte(mnemonic, $"{{d}},{register}", opcode, FieldKind.Displacement8, (byte)(0x88 | bits));
            yield return FieldPostbyte(mnemonic, $"[{{d}},{register}]", opcode, FieldKind.Displacement8, (byte)(0x98 | bits));
            yield return FieldPostbyte(mnemonic, $"{{d16}},{register}", opcode, FieldKind.Displacement16, (byte)(0x89 | bits));
            yield return FieldPostbyte(mnemonic, $"[{{d16}},{register}]", opcode, FieldKind.Displacement16, (byte)(0x99 | bits));

            foreach ((string accumulator, int mode) in new[] { ("B", 0x05), ("A", 0x06), ("D", 0x0B) })
            {
                yield return FixedPostbyte(mnemonic, $"{accumulator},{register}", opcode, (byte)(0x80 | bits | mode));
                yield return FixedPostbyte(mnemonic, $"[{accumulator},{register}]", opcode, (byte)(0x90 | bits | mode));
            }

            yield return FixedPostbyte(mnemonic, $",{register}+", opcode, (byte)(0x80 | bits));
            yield return FixedPostbyte(mnemonic, $",{register}++", opcode, (byte)(0x81 | bits));
            yield return FixedPostbyte(mnemonic, $"[,{register}++]", opcode, (byte)(0x91 | bits));
            yield return FixedPostbyte(mnemonic, $",-{register}", opcode, (byte)(0x82 | bits));
            yield return FixedPostbyte(mnemonic, $",--{register}", opcode, (byte)(0x83 | bits));
            yield return FixedPostbyte(mnemonic, $"[,--{register}]", opcode, (byte)(0x93 | bits));
        }

        yield return FieldPostbyte(mnemonic, "{r},PCR", opcode, FieldKind.Relative8, 0x8C);
        yield return FieldPostbyte(mnemonic, "[{r},PCR]", opcode, FieldKind.Relative8, 0x9C);
        yield return FieldPostbyte(mnemonic, "{r16},PCR", opcode, FieldKind.Relative16, 0x8D);
        yield return FieldPostbyte(mnemonic, "[{r16},PCR]", opcode, FieldKind.Relative16, 0x9D);
        yield return WordPostbyte(mnemonic, "[{w}]", opcode, 0x9F);
    }

    private static InstructionForm FixedPostbyte(string mnemonic, string operand, byte[] opcode, byte postbyte) =>
        new(
            mnemonic,
            OperandPattern.Parse(operand),
            opcode,
            [.. opcode.Select(EncodingPart.Byte), EncodingPart.Byte(postbyte)]);

    private static InstructionForm FieldPostbyte(string mnemonic, string operand, byte[] opcode, FieldKind field, byte? postbyte)
    {
        OperandPattern pattern = OperandPattern.Parse(operand);
        if (pattern.Fields.Count != 1 || pattern.Fields[0] != field)
        {
            throw new InvalidDataException($"Invalid M6809 indexed pattern '{operand}' (expected {field}).");
        }

        var layout = new List<EncodingPart>(opcode.Select(EncodingPart.Byte));
        if (postbyte is { } b)
        {
            layout.Add(EncodingPart.Byte(b));
        }

        layout.Add(EncodingPart.Slot(0));
        return new InstructionForm(mnemonic, pattern, opcode, layout);
    }

    private static InstructionForm WordPostbyte(string mnemonic, string operand, byte[] opcode, byte postbyte) =>
        new(
            mnemonic,
            OperandPattern.Parse(operand),
            opcode,
            [.. opcode.Select(EncodingPart.Byte), EncodingPart.Byte(postbyte), EncodingPart.Slot(0)]);

    private static int? EncodeRegisterPair(string operand)
    {
        string[] registers = operand.Split(',', StringSplitOptions.TrimEntries);
        return registers.Length == 2
            && RegisterCodes.TryGetValue(registers[0], out int source)
            && RegisterCodes.TryGetValue(registers[1], out int destination)
                ? (source << 4) | destination
                : null;
    }

    private static int? EncodeRegisterMask(string mnemonic, string operand)
    {
        bool stackS = mnemonic.EndsWith('S');
        string ownStack = stackS ? "S" : "U";
        string otherStack = stackS ? "U" : "S";
        var codes = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            ["PC"] = 0x80,
            [otherStack] = 0x40,
            ["Y"] = 0x20,
            ["X"] = 0x10,
            ["DP"] = 0x08,
            ["B"] = 0x04,
            ["A"] = 0x02,
            ["CC"] = 0x01,
        };
        byte mask = 0;
        foreach (string register in operand.Split(',', StringSplitOptions.TrimEntries))
        {
            byte bits = register.Equals("D", StringComparison.OrdinalIgnoreCase)
                ? (byte)0x06
                : codes.GetValueOrDefault(register);
            if (bits == 0 || register.Equals(ownStack, StringComparison.OrdinalIgnoreCase) || (mask & bits) != 0)
            {
                return null;
            }

            mask |= bits;
        }

        return mask;
    }
}
