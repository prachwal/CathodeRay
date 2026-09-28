namespace CathodeRay.Stub;

/// <summary>Dwuprzebiegowy asembler ISA zaślepki sterowany tabelą z JSON.
/// Składnia: <c>[etykieta:] [MNEMONIK [operand[,X]] | .org adres | .byte v, v, ...] [; komentarz]</c>;
/// tryb adresowania wynika z operandu: brak, <c>wartość</c> (d8 lub a16, zależnie od instrukcji) albo <c>a16,X</c>;
/// wartość to liczba (<c>42</c>, <c>$2A</c>, <c>0x2A</c>), etykieta lub ich suma/różnica (<c>op+1</c>, <c>end-start</c>).
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
        Dictionary<string, Dictionary<OperandMode, (byte Opcode, int Words)>> mnemonics = BuildMnemonics(isa);
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
            else if (mnemonics.TryGetValue(keyword, out Dictionary<OperandMode, (byte Opcode, int Words)>? modes))
            {
                (byte opcode, int words, string? value) = SelectMode(keyword, modes, operand, line);
                chunk = new Chunk(line, address, opcode, words - 1, value is null ? [] : [value]);
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
                if (value < 0 || value > max)
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

    private static Dictionary<string, Dictionary<OperandMode, (byte Opcode, int Words)>> BuildMnemonics(StubIsa isa)
    {
        var mnemonics = new Dictionary<string, Dictionary<OperandMode, (byte Opcode, int Words)>>(StringComparer.OrdinalIgnoreCase);
        foreach ((byte opcode, StubOpcode definition) in isa.Opcodes)
        {
            if (!mnemonics.TryGetValue(definition.Mnemonic, out Dictionary<OperandMode, (byte Opcode, int Words)>? modes))
            {
                modes = [];
                mnemonics.Add(definition.Mnemonic, modes);
            }

            if (!modes.TryAdd(definition.Mode, (opcode, definition.Words))
                || (modes.ContainsKey(OperandMode.Immediate8) && modes.ContainsKey(OperandMode.Address16)))
            {
                throw new InvalidOperationException(
                    $"Mnemonic '{definition.Mnemonic}' is ambiguous in ISA (duplicate mode or both immediate8 and address16).");
            }
        }

        return mnemonics;
    }

    private static (byte Opcode, int Words, string? Operand) SelectMode(
        string keyword, Dictionary<OperandMode, (byte Opcode, int Words)> modes, string? operand, int line)
    {
        OperandMode wanted = OperandMode.None;
        if (operand is not null)
        {
            string[] pieces = operand.Split(',', StringSplitOptions.TrimEntries);
            if (pieces.Length == 2 && pieces[1].Equals("X", StringComparison.OrdinalIgnoreCase))
            {
                operand = pieces[0];
                wanted = OperandMode.Address16X;
            }
            else if (pieces.Length > 1)
            {
                throw new AssemblerException(line, $"invalid operand '{operand}' (only a16,X is supported).");
            }
            else
            {
                wanted = modes.ContainsKey(OperandMode.Address16) ? OperandMode.Address16 : OperandMode.Immediate8;
            }
        }

        if (modes.TryGetValue(wanted, out (byte Opcode, int Words) found))
        {
            return (found.Opcode, found.Words, operand);
        }

        string message = wanted switch
        {
            OperandMode.None => $"{keyword} requires an operand.",
            _ when modes.Count == 1 && modes.ContainsKey(OperandMode.None) => $"{keyword} takes no operand.",
            OperandMode.Address16X => $"{keyword} has no indexed mode (a16,X).",
            _ => $"{keyword} requires an indexed operand (a16,X).",
        };
        throw new AssemblerException(line, message);
    }

    private static string RequireOperand(string keyword, string? operand, int line) =>
        operand ?? throw new AssemblerException(line, $"{keyword} requires an operand.");

    private static int Resolve(string operand, Dictionary<string, int> labels, int line)
    {
        int split = operand.LastIndexOfAny(['+', '-']);
        if (split > 0)
        {
            int left = Resolve(operand[..split].Trim(), labels, line);
            int right = Resolve(operand[(split + 1)..].Trim(), labels, line);
            return operand[split] == '+' ? left + right : left - right;
        }

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
