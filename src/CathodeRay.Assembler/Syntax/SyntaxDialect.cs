using CathodeRay.Assembler.Directives;

namespace CathodeRay.Assembler.Syntax;

/// <summary>Dialekt składni źródła jako dane: zapis liczb, symbol PC, reguły etykiet, operatory i nazwy dyrektyw.
/// Nowy dialekt = nowa instancja (zob. <see cref="SyntaxDialects"/>); rdzeń asemblera się nie zmienia.</summary>
public sealed record SyntaxDialect
{
    /// <summary>Nazwa (wartość <c>--syntax</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Akceptowane zapisy liczb.</summary>
    public required NumberFormats Numbers { get; init; }

    /// <summary>Symbol bieżącego adresu w wyrażeniach (<c>*</c> MOS/ca65, <c>$</c> Intel) lub <see langword="null"/>.</summary>
    public char? ProgramCounter { get; init; }

    /// <summary>Czy <c>*= wyrażenie</c> ustawia adres (MOS).</summary>
    public bool OrgByAssignment { get; init; }

    /// <summary>Czy etykieta może stać bez dwukropka w kolumnie 1 (MOS).</summary>
    public bool LabelsWithoutColon { get; init; }

    /// <summary>Czy nazwy symboli rozróżniają wielkość liter.</summary>
    public bool CaseSensitiveSymbols { get; init; }

    /// <summary>Czy unarne <c>&lt;</c>/<c>&gt;</c> dają młodszy/starszy bajt (ca65, MOS).</summary>
    public bool LowHighPrefixes { get; init; }

    /// <summary>Czy działają operatory słowne Intel: <c>AND OR XOR NOT MOD SHL SHR LOW HIGH</c>.</summary>
    public bool WordOperators { get; init; }

    /// <summary>Słowa przypisania symbolu (<c>nazwa = wyr</c>, <c>nazwa EQU wyr</c>).</summary>
    public IReadOnlySet<string> AssignmentKeywords { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "=" };

    /// <summary>Dyrektywy dialektu (nazwa bez rozróżniania wielkości liter → implementacja).</summary>
    public required IReadOnlyDictionary<string, IDirective> Directives { get; init; }

    /// <summary>Porównanie nazw symboli zgodne z dialektem.</summary>
    public StringComparer SymbolComparer => CaseSensitiveSymbols ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    /// <summary>Buduje słownik dyrektyw bez rozróżniania wielkości liter.</summary>
    /// <param name="directives">Pary nazwa → dyrektywa.</param>
    /// <returns>Słownik.</returns>
    public static IReadOnlyDictionary<string, IDirective> DirectiveTable(params (string Name, IDirective Directive)[] directives) =>
        directives.ToDictionary(static d => d.Name, static d => d.Directive, StringComparer.OrdinalIgnoreCase);
}
