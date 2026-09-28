namespace CathodeRay.Assembler.Syntax;

/// <summary>Linia źródła po rozbiorze składniowym.</summary>
/// <param name="Number">Numer linii w pliku (od 1).</param>
/// <param name="Text">Oryginalny tekst (do listingu).</param>
/// <param name="Label">Etykieta (adres) lub nazwa symbolu przy przypisaniu.</param>
/// <param name="Keyword">Mnemonik, dyrektywa albo <see cref="Assignment"/>.</param>
/// <param name="Operand">Operand bez komentarza lub <see langword="null"/>.</param>
/// <param name="File">Plik źródła (<see langword="null"/> = tekst bez kontekstu pliku).</param>
/// <param name="Macro">Użycie makra, z którego pochodzi linia (<see langword="null"/> = zwykła linia).</param>
internal sealed record SourceLine(int Number, string Text, string? Label, string? Keyword, string? Operand, string? File = null, MacroUse? Macro = null)
{
    /// <summary>Znormalizowane słowo przypisania symbolu (<c>=</c>, <c>EQU</c>, <c>SET</c>).</summary>
    public const string Assignment = "=";

    public bool IsAssignment => Keyword == Assignment;
}
