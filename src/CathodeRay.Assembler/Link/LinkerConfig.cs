using System.Text.RegularExpressions;

using CathodeRay.Assembler.Syntax;

namespace CathodeRay.Assembler.Link;

/// <summary>Konfiguracja pamięci linkera (podzbiór ld65: MEMORY/SEGMENTS).</summary>
/// <param name="Areas">Obszary.</param>
/// <param name="Segments">Mapowania segmentów w kolejności układania.</param>
public sealed partial record LinkerConfig(IReadOnlyList<MemoryArea> Areas, IReadOnlyList<SegmentMapping> Segments)
{
    /// <summary>Parsuje konfigurację.</summary>
    /// <param name="text">Tekst <c>.cfg</c>.</param>
    /// <returns>Konfiguracja.</returns>
    /// <exception cref="LinkerException">Błąd składni lub wartości.</exception>
    public static LinkerConfig Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var areas = new List<MemoryArea>();
        var segments = new List<SegmentMapping>();
        foreach (Match match in Block().Matches(text))
        {
            string kind = match.Groups[1].Value.ToUpperInvariant();
            foreach (string entry in match.Groups[2].Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (entry.Length == 0)
                {
                    continue;
                }

                int colon = entry.IndexOf(':');
                if (colon < 0)
                {
                    throw new LinkerException($"Invalid config entry '{entry}' (expected NAME: attrs).");
                }

                string name = entry[..colon].Trim();
                var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string attr in entry[(colon + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    int equals = attr.IndexOf('=');
                    if (equals < 0)
                    {
                        throw new LinkerException($"Invalid attribute '{attr}' (expected key=value).");
                    }

                    attrs[attr[..equals].Trim()] = attr[(equals + 1)..].Trim();
                }

                if (kind == "MEMORY")
                {
                    areas.Add(ParseArea(name, attrs));
                }
                else if (kind == "SEGMENTS")
                {
                    segments.Add(ParseSegment(name, attrs));
                }
                else
                {
                    throw new LinkerException($"Unknown config block '{match.Groups[1].Value}' (expected MEMORY or SEGMENTS).");
                }
            }
        }

        if (areas.Count == 0 || segments.Count == 0)
        {
            throw new LinkerException("Config needs MEMORY and SEGMENTS blocks.");
        }

        return new LinkerConfig(areas, segments);
    }

    private static MemoryArea ParseArea(string name, Dictionary<string, string> attrs)
    {
        if (!attrs.TryGetValue("start", out string? startText) || !TryNumber(startText, out int start))
        {
            throw new LinkerException($"Memory area '{name}' needs start=$hex/dec.");
        }

        if (!attrs.TryGetValue("size", out string? sizeText) || !TryNumber(sizeText, out int size) || size < 0)
        {
            throw new LinkerException($"Memory area '{name}' needs size=bytes.");
        }

        return new MemoryArea(name, start, start + size);
    }

    private static bool TryNumber(string text, out int value)
    {
        value = 0;
        string digits = text.StartsWith('$') ? text[1..] : text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : string.Empty;
        if (digits.Length > 0)
        {
            return int.TryParse(digits, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out value) && value >= 0;
        }

        return int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value) && value >= 0;
    }

    private static SegmentMapping ParseSegment(string name, Dictionary<string, string> attrs)
    {
        if (!attrs.TryGetValue("load", out string? load) || load.Length == 0)
        {
            throw new LinkerException($"Segment '{name}' needs load=area.");
        }

        return new SegmentMapping(name, load);
    }

    [GeneratedRegex(@"(MEMORY|SEGMENTS)\s*\{(.*?)\}", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Block();
}
