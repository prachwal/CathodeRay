using CathodeRay.Assembler.Isa;
using CathodeRay.Assembler.Syntax;

namespace CathodeRay.Assembler;

/// <summary>Cel asemblera (wartość <c>--cpu</c>): skąd wziąć dane ISA, jak z nich zbudować zestaw instrukcji
/// i jakie dialekty składni są dozwolone (pierwszy = domyślny).</summary>
/// <param name="Name">Nazwa celu.</param>
/// <param name="Description">Opis do pomocy CLI.</param>
/// <param name="IsaFile">Nazwa pliku ISA JSON.</param>
/// <param name="Load">Adapter danych ISA → zestaw instrukcji.</param>
/// <param name="Syntaxes">Dozwolone dialekty; pierwszy jest domyślny.</param>
public sealed record AssemblerTarget(
    string Name,
    string Description,
    string IsaFile,
    Func<Stream, InstructionSet> Load,
    IReadOnlyList<SyntaxDialect> Syntaxes)
{
    /// <summary>Kolejność bajtów słów w kodzie maszynowym (linker zapisuje wg niej relokacje <c>Abs16</c>).</summary>
    public Endianness Endianness { get; init; } = Endianness.Little;

    /// <summary>Dialekt domyślny.</summary>
    public SyntaxDialect DefaultSyntax => Syntaxes[0];

    /// <summary>Szuka dozwolonego dialektu po nazwie.</summary>
    /// <param name="name">Nazwa dialektu.</param>
    /// <returns>Dialekt lub <see langword="null"/>.</returns>
    public SyntaxDialect? FindSyntax(string name) =>
        Syntaxes.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
}
