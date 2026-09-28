using System.Globalization;

namespace CathodeRay.Assembler;

/// <summary>Wynik asemblacji: obraz od najniższego do najwyższego zapisanego adresu, symbole i listing.</summary>
/// <param name="Origin">Adres pierwszego bajtu obrazu (adres ładowania).</param>
/// <param name="Image">Bajty od <paramref name="Origin"/>; luki wypełnione zerami.</param>
/// <param name="Symbols">Etykiety i stałe.</param>
/// <param name="Listing">Linie listingu w kolejności źródła.</param>
public sealed record AssemblyResult(
    int Origin,
    byte[] Image,
    IReadOnlyDictionary<string, int> Symbols,
    IReadOnlyList<ListingLine> Listing)
{
    private const int BytesPerRow = 4;

    /// <summary>Wypisuje listing jak oryginalne asemblery: adres, bajty, numer linii, źródło.</summary>
    /// <param name="output">Wyjście.</param>
    public void WriteListing(TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);
        foreach (ListingLine line in Listing)
        {
            for (int row = 0; row == 0 || row * BytesPerRow < line.Bytes.Length; row++)
            {
                byte[] chunk = [.. line.Bytes.Skip(row * BytesPerRow).Take(BytesPerRow)];
                string address = chunk.Length > 0 || row == 0 ? (line.Address + (row * BytesPerRow)).ToString("X4", CultureInfo.InvariantCulture) : "    ";
                string bytes = string.Join(' ', chunk.Select(static b => b.ToString("X2", CultureInfo.InvariantCulture)));
                string source = row == 0 ? string.Create(CultureInfo.InvariantCulture, $"{line.Line,5}  {line.Source}") : string.Empty;
                output.WriteLine($"{address}  {bytes,-11}  {source}".TrimEnd());
            }
        }
    }
}
