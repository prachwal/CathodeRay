using CathodeRay.Abstractions;

namespace CathodeRay.Cli;

/// <summary>Zakres pamięci do zrzutu w formacie <c>start:długość</c> (np. <c>0x2000:16</c>).</summary>
/// <param name="Start">Adres początkowy.</param>
/// <param name="Length">Liczba bajtów.</param>
internal readonly record struct MemoryRange(int Start, int Length)
{
    private const int BytesPerLine = 16;

    /// <summary>Parsuje <c>start:długość</c>; zakres musi mieścić się w 64 KB.</summary>
    /// <param name="text">Tekst zakresu.</param>
    /// <param name="range">Zakres.</param>
    /// <param name="error">Opis błędu lub <see langword="null"/>.</param>
    /// <returns>Czy się udało.</returns>
    public static bool TryParse(string text, out MemoryRange range, out string? error)
    {
        range = default;
        string[] parts = text.Split(':');
        if (parts.Length != 2
            || !NumberLiteral.TryParse(parts[0], out int start)
            || !NumberLiteral.TryParse(parts[1], out int length))
        {
            error = $"Invalid dump range '{text}' (expected start:length, e.g. 0x2000:16).";
            return false;
        }

        if (length == 0 || start + (long)length > 0x10000)
        {
            error = $"Dump range '{text}' must be non-empty and within 64 KB.";
            return false;
        }

        range = new MemoryRange(start, length);
        error = null;
        return true;
    }

    /// <summary>Wypisuje zakres jako hexdump po 16 bajtów na linię.</summary>
    /// <param name="output">Wyjście.</param>
    /// <param name="bus">Szyna, z której czytane są bajty.</param>
    public void WriteHexDump(TextWriter output, IBus bus)
    {
        for (int line = Start; line < Start + Length; line += BytesPerLine)
        {
            int count = Math.Min(BytesPerLine, Start + Length - line);
            IEnumerable<string> bytes = Enumerable.Range(line, count).Select(a => bus.Read((ushort)a).ToString("X2", null));
            output.WriteLine($"{line:X4}: {string.Join(' ', bytes)}");
        }
    }
}
