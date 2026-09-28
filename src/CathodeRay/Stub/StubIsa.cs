using System.Globalization;
using System.Text.Json;

namespace CathodeRay.Stub;

/// <summary>Tabela opcode zaślepki wczytana z pliku JSON (schemat <c>isa.schema.json</c>).</summary>
public sealed class StubIsa
{
    private StubIsa(IReadOnlyDictionary<byte, StubOpcode> opcodes) => Opcodes = opcodes;

    /// <summary>Mapa opcode → definicja.</summary>
    public IReadOnlyDictionary<byte, StubOpcode> Opcodes { get; }

    /// <summary>Wczytuje tabelę z pliku JSON.</summary>
    /// <param name="path">Ścieżka do pliku ISA.</param>
    /// <returns>Tabela opcode.</returns>
    public static StubIsa FromJsonFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return FromJson(stream);
    }

    /// <summary>Parsuje tabelę ze strumienia JSON (klucz <c>instructions</c>).</summary>
    /// <param name="stream">Strumień z JSON-em ISA.</param>
    /// <returns>Tabela opcode.</returns>
    public static StubIsa FromJson(Stream stream)
    {
        using var document = JsonDocument.Parse(stream);
        var opcodes = new Dictionary<byte, StubOpcode>();
        foreach (JsonElement entry in document.RootElement.GetProperty("instructions").EnumerateArray())
        {
            string hex = entry.GetProperty("opcode").GetString()!;
            byte opcode = byte.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            string mnemonic = entry.GetProperty("mnemonic").GetString()!;
            int cycles = entry.GetProperty("cycles").GetInt32();
            int words = entry.GetProperty("words").GetInt32();
            OperandMode mode = ParseMode(entry);
            int expectedWords = mode switch
            {
                OperandMode.None => 1,
                OperandMode.Immediate8 => 2,
                _ => 3,
            };
            if (words != expectedWords)
            {
                throw new InvalidDataException($"Opcode 0x{opcode:X2} ({mnemonic}): {mode} needs {expectedWords} words, got {words}.");
            }

            opcodes[opcode] = new StubOpcode(mnemonic, cycles, words, mode);
        }

        return new StubIsa(opcodes);
    }

    private static OperandMode ParseMode(JsonElement entry)
    {
        if (!entry.TryGetProperty("operands", out JsonElement operands)
            || operands.ValueKind != JsonValueKind.Array
            || operands.GetArrayLength() == 0)
        {
            return OperandMode.None;
        }

        string? type = operands[0].GetProperty("type").GetString();
        return type switch
        {
            "immediate8" => OperandMode.Immediate8,
            "address16" => OperandMode.Address16,
            "address16_x" => OperandMode.Address16X,
            _ => throw new InvalidDataException($"Unknown operand type '{type}'."),
        };
    }
}
