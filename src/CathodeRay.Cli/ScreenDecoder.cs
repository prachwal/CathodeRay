namespace CathodeRay.Cli;

/// <summary>Dekoder ekranu tekstowego: 1000 B pamięci (wierszami) na tekst 40x25.</summary>
internal static class ScreenDecoder
{
    /// <summary>Szerokość ekranu w znakach.</summary>
    public const int Width = 40;

    /// <summary>Wysokość ekranu w wierszach.</summary>
    public const int Height = 25;

    /// <summary>Rozmiar bufora ekranu w bajtach.</summary>
    public const int Size = Width * Height;

    /// <summary>Dekoduje bufor na wiersze tekstu (0/niedrukowalne to spacja, końcowe spacje cięte).</summary>
    /// <param name="memory">Odczyt bajtu spod adresu.</param>
    /// <param name="address">Adres bufora.</param>
    /// <returns>Wiersze tekstu (dokładnie <see cref="Height"/>).</returns>
    public static IReadOnlyList<string> Render(Func<ushort, byte> memory, int address)
    {
        var rows = new List<string>(Height);
        for (int row = 0; row < Height; row++)
        {
            char[] line = new char[Width];
            for (int col = 0; col < Width; col++)
            {
                byte value = memory((ushort)(address + (row * Width) + col));
                line[col] = value is >= 32 and <= 126 ? (char)value : ' ';
            }

            rows.Add(new string(line).TrimEnd());
        }

        return rows;
    }

    /// <summary>Zapisuje wiersze jako dokument Markdown (blok text).</summary>
    /// <param name="rows">Wiersze z <see cref="Render"/>.</param>
    /// <param name="address">Adres bufora (do nagłówka).</param>
    /// <returns>Tekst Markdown.</returns>
    public static string ToMarkdown(IReadOnlyList<string> rows, int address) =>
        $"# Screen dump (40x25 @ ${address:X4})\n\n```text\n{string.Join("\n", rows)}\n```\n";
}
