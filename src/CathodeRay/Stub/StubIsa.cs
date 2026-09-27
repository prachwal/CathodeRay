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
            opcodes[opcode] = new StubOpcode(mnemonic, cycles, words);
        }

        return new StubIsa(opcodes);
    }
}
