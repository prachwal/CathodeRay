using System.Globalization;

namespace CathodeRay.Assembler;

/// <summary>Wynik asemblacji: obraz od najniższego do najwyższego zapisanego adresu, symbole i listing.</summary>
/// <param name="Origin">Adres pierwszego bajtu obrazu (adres ładowania).</param>
/// <param name="Image">Bajty od <paramref name="Origin"/>; luki wypełnione zerami.</param>
/// <param name="Symbols">Etykiety i stałe.</param>
/// <param name="Listing">Linie listingu w kolejności źródła.</param>
/// <param name="Segments">Zakresy segmentów w kolejności pierwszego użycia (jeden wpis dla programów bez segmentów).</param>
/// <param name="Messages">Komunikaty <c>.out</c>/<c>.warning</c> w kolejności źródła.</param>
public sealed record AssemblyResult(
    int Origin,
    byte[] Image,
    IReadOnlyDictionary<string, int> Symbols,
    IReadOnlyList<ListingLine> Listing,
    IReadOnlyList<SegmentSpan>? Segments = null,
    IReadOnlyList<AsmMessage>? Messages = null)
{
    private const int BytesPerRow = 4;

    private const int HexPerRow = 16;

    /// <summary>Zakresy segmentów (puste = sprzed segmentów).</summary>
    public IReadOnlyList<SegmentSpan> Segments { get; } = Segments ?? [];

    /// <summary>Komunikaty <c>.out</c>/<c>.warning</c> w kolejności źródła.</summary>
    public IReadOnlyList<AsmMessage> Messages { get; } = Messages ?? [];

    /// <summary>Wypisuje listing jak oryginalne asemblery: adres, bajty, numer linii, źródło.</summary>
    /// <param name="output">Wyjście.</param>
    public void WriteListing(TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);
        bool grouped = Listing.Select(static line => line.Segment).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1;
        string? current = null;
        foreach (ListingLine line in Listing)
        {
            if (grouped && !StringComparer.OrdinalIgnoreCase.Equals(line.Segment, current))
            {
                current = line.Segment;
                output.WriteLine($"***** {current} *****");
            }

            for (int row = 0; row == 0 || row * BytesPerRow < line.Bytes.Length; row++)
            {
                byte[] chunk = [.. line.Bytes.Skip(row * BytesPerRow).Take(BytesPerRow)];
                string address = chunk.Length > 0 || row == 0 ? (line.Address + (row * BytesPerRow)).ToString("X4", CultureInfo.InvariantCulture) : "    ";
                string bytes = string.Join(' ', chunk.Select(static b => b.ToString("X2", CultureInfo.InvariantCulture)));
                string location = line.File is null ? $"{line.Line,5}" : $"{line.File}:{line.Line}";
                string source = row == 0 ? string.Create(CultureInfo.InvariantCulture, $"{location}  {line.Source}") : string.Empty;
                output.WriteLine($"{address}  {bytes,-11}  {source}".TrimEnd());
            }
        }
    }

    /// <summary>Wypisuje obraz w formacie Intel HEX (rekordy danych 00 po 16 bajtów + EOF 01, suma kontrolna).</summary>
    /// <param name="output">Wyjście.</param>
    public void WriteIntelHex(TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);
        for (int offset = 0; offset < Image.Length; offset += HexPerRow)
        {
            byte[] chunk = Image[offset..Math.Min(offset + HexPerRow, Image.Length)];
            int address = Origin + offset;
            int sum = chunk.Length + (address >> 8) + (address & 0xFF);
            foreach (byte b in chunk)
            {
                sum += b;
            }

            string data = string.Concat(chunk.Select(static b => b.ToString("X2", CultureInfo.InvariantCulture)));
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $":{chunk.Length:X2}{address:X4}00{data}{(byte)-sum:X2}"));
        }

        output.WriteLine(":00000001FF");
    }
}
