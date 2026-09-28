using CathodeRay.Assembler.Syntax;

namespace CathodeRay.Assembler.Link;

/// <summary>Współdzielony stan linkowania obu przebiegów: zewnętrzne, globalne i miejsca ich deklaracji.</summary>
internal sealed class LinkScope(StringComparer comparer)
{
    /// <summary>Symbole zewnętrzne (<c>EXTERN</c>): odwołania nie są błędami, trafiają do relokacji.</summary>
    public HashSet<string> Externals { get; } = new(comparer);

    /// <summary>Symbole eksportowane (<c>GLOBAL</c>).</summary>
    public HashSet<string> Globals { get; } = new(comparer);

    /// <summary>Miejsca deklaracji <c>GLOBAL</c> (do błędu o niezdefiniowanym eksporcie).</summary>
    public Dictionary<string, SourceLine> GlobalSites { get; } = new(comparer);
}
