namespace CathodeRay.Assembler.Syntax;

/// <summary>Użycie makra, z którego pochodzi rozwinięta linia (do błędów wskazujących wywołanie i definicję).</summary>
/// <param name="Name">Nazwa makra.</param>
/// <param name="DefFile">Plik definicji lub <see langword="null"/>.</param>
/// <param name="DefLine">Linia definicji (dyrektywy <c>.macro</c>/<c>MACRO</c>).</param>
internal sealed record MacroUse(string Name, string? DefFile, int DefLine);
