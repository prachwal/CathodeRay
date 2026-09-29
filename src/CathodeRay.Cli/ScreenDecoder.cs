namespace CathodeRay.Cli;

/// <summary>Dekoder ekranu tekstowego: obszar pamięci (wierszami) na tekst o danej szerokości i wysokości.</summary>
internal sealed class ScreenDecoder
{
    /// <summary>Tworzy dekoder.</summary>
    /// <param name="width">Szerokość w znakach (domyślnie 40).</param>
    /// <param name="height">Wysokość w wierszach (domyślnie 25).</param>
    public ScreenDecoder(int width = 40, int height = 25)
    {
        if (width < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        }

        Width = width;
        Height = height;
    }

    /// <summary>Szerokość ekranu w znakach.</summary>
    public int Width { get; }

    /// <summary>Wysokość ekranu w wierszach.</summary>
    public int Height { get; }

    /// <summary>Rozmiar bufora ekranu w bajtach.</summary>
    public int Size => Width * Height;

    /// <summary>Dekoduje bufor na wiersze tekstu (0/niedrukowalne to spacja, końcowe spacje cięte).</summary>
    /// <param name="memory">Odczyt bajtu spod adresu.</param>
    /// <param name="address">Adres bufora.</param>
    /// <returns>Wiersze tekstu (dokładnie <see cref="Height"/>).</returns>
    public IReadOnlyList<string> Render(Func<ushort, byte> memory, int address)
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
    public string ToMarkdown(IReadOnlyList<string> rows, int address) =>
        $"# Screen dump ({Width}x{Height} @ ${address:X4})\n\n```text\n{string.Join("\n", rows)}\n```\n";
}
