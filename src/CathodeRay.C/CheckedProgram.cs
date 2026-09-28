namespace CathodeRay.C;

/// <summary>Program po kontroli typów.</summary>
/// <param name="Functions">Funkcje.</param>
/// <param name="Globals">Globale.</param>
/// <param name="Warnings">Ostrzeżenia (np. zawężenie int→uchar).</param>
public sealed record CheckedProgram(IReadOnlyList<CheckedFunction> Functions, IReadOnlyList<TypedSymbol> Globals, IReadOnlyList<string> Warnings);
