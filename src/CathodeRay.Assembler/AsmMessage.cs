namespace CathodeRay.Assembler;

/// <summary>Komunikat asemblacji (<c>.out</c> / <c>.warning</c>).</summary>
/// <param name="File">Plik dyrektywy lub <see langword="null"/>.</param>
/// <param name="Line">Linia dyrektywy.</param>
/// <param name="Text">Treść.</param>
/// <param name="Warning">Czy ostrzeżenie (<c>false</c> = informacyjny <c>.out</c>).</param>
public sealed record AsmMessage(string? File, int Line, string Text, bool Warning);
