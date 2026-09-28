namespace CathodeRay.Stub;

/// <summary>Dwuprzebiegowy asembler ISA zaślepki sterowany tabelą z JSON.
/// Składnia: <c>[etykieta:] [MNEMONIK [operand] | .org adres | .byte v, v, ...] [; komentarz]</c>;
/// wartość to liczba (<c>42</c>, <c>$2A</c>, <c>0x2A</c>) lub etykieta.
/// Operand 1 bajt dla instrukcji 2-słowowych, 2 bajty little-endian dla 3-słowowych.
/// Obraz zaczyna się od adresu 0; luki po <c>.org</c> są wypełniane zerami.</summary>
public static class StubAssembler
{
    /// <summary>Asembluje źródło do obrazu binarnego.</summary>
    /// <param name="source">Tekst źródła.</param>
    /// <param name="isa">Tabela opcode z JSON.</param>
    /// <returns>Obraz binarny od adresu 0.</returns>
    /// <exception cref="AssemblerException">Błąd składni, nieznany mnemonik, etykieta lub wartość poza zakresem.</exception>
    public static byte[] Assemble(string source, StubIsa isa)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(isa);
        Dictionary<string, (byte Opcode, int Words)> mnemonics = BuildMnemonics(isa);
        var labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var chunks = new List<Chunk>();
        int address = 0;
        int end = 0;

        string[] rows = source.Split('\n');
        for (int i = 0; i < rows.Length; i++)
        {
            int line = i + 1;
            string text = StripComment(rows[i]);
            int colon = text.IndexOf(':', StringComparison.Ordinal);
            if (colon >= 0)
            {
                string label = text[..colon].Trim();
                if (!IsIdentifier(label))
                {
                    throw new AssemblerException(line, $"invalid label '{label}'.");
                }

                if (!labels.TryAdd(label, address))
                {
                    throw new AssemblerException(line, $"duplicate label '{label}'.");
                }

                text = text[(colon + 1)..].Trim();
            }

            if (text.Length == 0)
            {
                continue;
            }

            string[] parts = text.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string keyword = parts[0];
            string? operand = parts.Length > 1 ? parts[1] : null;
            Chunk chunk;
            if (keyword.Equals(".org", StringComparison.OrdinalIgnoreCase))
            {
                int origin = Resolve(RequireOperand(keyword, operand, line), labels, line);
                if (origin < address || origin > ushort.MaxValue)
                {
                    throw new AssemblerException(line, $".org {origin} must be in {address}..{ushort.MaxValue} (no overlap).");
                }

                address = origin;
                continue;
            }

            if (keyword.Equals(".byte", StringComparison.OrdinalIgnoreCase))
            {
                string[] values = RequireOperand(keyword, operand, line).Split(',', StringSplitOptions.TrimEntries);
                if (Array.Exists(values, static v => v.Length == 0))
                {
                    throw new AssemblerException(line, ".byte has an empty value.");
                }

                chunk = new Chunk(line, address, null, 1, values);
            }
            else if (mnemonics.TryGetValue(keyword, out (byte Opcode, int Words) definition))
            {
                if ((definition.Words > 1) != (operand is not null))
                {
                    throw new AssemblerException(
                        line, definition.Words > 1 ? $"{keyword} requires an operand." : $"{keyword} takes no operand.");
                }

                chunk = new Chunk(line, address, definition.Opcode, definition.Words - 1, operand is null ? [] : [operand]);
            }
            else
            {
                throw new AssemblerException(line, $"unknown mnemonic '{keyword}'.");
            }

            chunks.Add(chunk);
            address += chunk.Size;
            if (address > 0x10000)
            {
                throw new AssemblerException(line, "program exceeds 64 KB.");
            }

            end = address;
        }

        var image = new byte[end];
        foreach (Chunk chunk in chunks)
        {
            int position = chunk.Address;
            if (chunk.Opcode is byte opcode)
            {
                image[position++] = opcode;
            }

            int max = chunk.OperandBytes == 1 ? byte.MaxValue : ushort.MaxValue;
            foreach (string operand in chunk.Operands)
            {
                int value = Resolve(operand, labels, chunk.Line);
                if (value > max)
                {
                    throw new AssemblerException(chunk.Line, $"value {value} out of range 0..{max}.");
                }

                image[position++] = (byte)value;
                if (chunk.OperandBytes == 2)
                {
                    image[position++] = (byte)(value >> 8);
                }
            }
        }

        return image;
    }

    private static Dictionary<string, (byte Opcode, int Words)> BuildMnemonics(StubIsa isa)
    {
        var mnemonics = new Dictionary<string, (byte Opcode, int Words)>(StringComparer.OrdinalIgnoreCase);
        foreach ((byte opcode, StubOpcode definition) in isa.Opcodes)
        {
            if (definition.Words is < 1 or > 3)
            {
                throw new InvalidOperationException($"{definition.Mnemonic}: unsupported word count {definition.Words}.");
            }

            if (!mnemonics.TryAdd(definition.Mnemonic, (opcode, definition.Words)))
            {
                throw new InvalidOperationException($"Mnemonic '{definition.Mnemonic}' is ambiguous in ISA.");
            }
        }

        return mnemonics;
    }

    private static string RequireOperand(string keyword, string? operand, int line) =>
        operand ?? throw new AssemblerException(line, $"{keyword} requires an operand.");

    private static int Resolve(string operand, Dictionary<string, int> labels, int line)
    {
        if (NumberLiteral.TryParse(operand, out int value))
        {
            return value;
        }

        if (IsIdentifier(operand))
        {
            return labels.TryGetValue(operand, out value)
                ? value
                : throw new AssemblerException(line, $"undefined label '{operand}'.");
        }

        throw new AssemblerException(line, $"invalid operand '{operand}'.");
    }

    private static bool IsIdentifier(string text) =>
        text.Length > 0
        && (char.IsAsciiLetter(text[0]) || text[0] == '_')
        && text.All(static c => char.IsAsciiLetterOrDigit(c) || c == '_');

    private static string StripComment(string row)
    {
        int semicolon = row.IndexOf(';', StringComparison.Ordinal);
        return (semicolon >= 0 ? row[..semicolon] : row).Trim();
    }

    /// <summary>Fragment obrazu: opcjonalny opcode i operandy o stałej szerokości (instrukcja lub <c>.byte</c>).</summary>
    private readonly record struct Chunk(int Line, int Address, byte? Opcode, int OperandBytes, string[] Operands)
    {
        public int Size => (Opcode is null ? 0 : 1) + (OperandBytes * Operands.Length);
    }
}
