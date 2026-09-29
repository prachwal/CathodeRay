using System.Text.Json;

namespace CathodeRay.Assembler.Link;

/// <summary>Moduł obiektu linkera: segmenty z bajtami, symbole globalne i relokacje.
/// Format JSON <c>cathode-obj/1</c> (czytelny, wersjonowany).</summary>
/// <param name="Cpu">Nazwa celu (<c>--cpu</c>).</param>
/// <param name="Segments">Segmenty w kolejności pierwszego użycia.</param>
/// <param name="Symbols">Symbole globalne i lokalne modułu (bez tanich <c>@</c>).</param>
/// <param name="Relocations">Relokacje pól.</param>
public sealed record ObjectModule(string Cpu, IReadOnlyList<ObjectSegment> Segments, IReadOnlyList<ObjectSymbol> Symbols, IReadOnlyList<Relocation> Relocations)
{
    /// <summary>Sygnatura formatu.</summary>
    public const string Format = "cathode-obj/1";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    /// <summary>Serializuje moduł do JSON.</summary>
    /// <returns>Tekst JSON.</returns>
    public string ToJson() => JsonSerializer.Serialize(
        new Dto(
            Format,
            Cpu,
            [.. Segments.Select(static s => new ObjectSegmentDto(
                s.Name,
                s.Bss,
                Convert.ToBase64String(s.Data),
                s.Length,
                [.. (s.Lines ?? []).Select(static l => new ObjectLineDto(
                    l.Offset,
                    Convert.ToBase64String(l.Bytes),
                    l.Text,
                    l.File,
                    l.Line))]))],
            [.. Symbols],
            [.. Relocations]),
        Json);

    /// <summary>Wczytuje moduł z JSON.</summary>
    /// <param name="json">Tekst JSON.</param>
    /// <returns>Moduł.</returns>
    /// <exception cref="InvalidDataException">Zły format.</exception>
    public static ObjectModule FromJson(string json)
    {
        Dto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<Dto>(json, Json);
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"Invalid object module: {e.Message}", e);
        }

        if (dto is null || dto.Format != Format)
        {
            throw new InvalidDataException($"Invalid object module (expected format '{Format}').");
        }

        return new ObjectModule(
            dto.Cpu,
            [.. dto.Segments.Select(static s => new ObjectSegment(
                s.Name,
                s.Bss,
                Convert.FromBase64String(s.Data),
                s.Length,
                [.. (s.Lines ?? []).Select(static l => new ObjectLine(
                    l.Offset,
                    Convert.FromBase64String(l.Bytes),
                    l.Text,
                    l.File,
                    l.Line))]))],
            dto.Symbols,
            dto.Relocations);
    }

    private sealed record Dto(string Format, string Cpu, List<ObjectSegmentDto> Segments, List<ObjectSymbol> Symbols, List<Relocation> Relocations);

    private sealed record ObjectSegmentDto(string Name, bool Bss, string Data, int Length, List<ObjectLineDto>? Lines = null);

    private sealed record ObjectLineDto(int Offset, string Bytes, string Text, string? File, int Line);
}
