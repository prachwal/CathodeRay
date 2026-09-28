using System.Globalization;
using System.Text.Json;

namespace CathodeRay.Assembler.Isa;

/// <summary>Surowy wpis pliku ISA JSON (pola wspólne dla wszystkich CPU); interpretację robi moduł CPU.</summary>
/// <param name="Opcode">Bajty opcode'u.</param>
/// <param name="Mnemonic">Pole <c>mnemonic</c> (dla 8080 to szablon składni, np. <c>MVI B,d8</c>).</param>
/// <param name="Words">Liczba bajtów instrukcji.</param>
/// <param name="Encoding">Pole <c>encoding</c> (dla 6502 nazwa trybu adresowania) lub <see langword="null"/>.</param>
/// <param name="OperandTypes">Typy z <c>operands[].type</c>.</param>
public sealed record IsaEntry(byte[] Opcode, string Mnemonic, int Words, string? Encoding, IReadOnlyList<string> OperandTypes)
{
    /// <summary>Czyta wszystkie wpisy z klucza <c>instructions</c>.</summary>
    /// <param name="json">Strumień JSON.</param>
    /// <returns>Wpisy w kolejności pliku.</returns>
    public static IReadOnlyList<IsaEntry> ReadAll(Stream json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return [.. document.RootElement.GetProperty("instructions").EnumerateArray().Select(Read)];
    }

    /// <inheritdoc/>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Convert.ToHexString(Opcode)} {Mnemonic}");

    private static IsaEntry Read(JsonElement entry)
    {
        string hex = entry.GetProperty("opcode").GetString()!;
        byte[] opcode = Convert.FromHexString(hex);
        string? encoding = entry.TryGetProperty("encoding", out JsonElement e) && e.ValueKind == JsonValueKind.String
            ? e.GetString()
            : null;
        IReadOnlyList<string> types = entry.TryGetProperty("operands", out JsonElement ops) && ops.ValueKind == JsonValueKind.Array
            ? [.. ops.EnumerateArray().Select(static o => o.GetProperty("type").GetString() ?? string.Empty)]
            : [];
        return new IsaEntry(
            opcode,
            entry.GetProperty("mnemonic").GetString()!,
            entry.GetProperty("words").GetInt32(),
            encoding,
            types);
    }
}
