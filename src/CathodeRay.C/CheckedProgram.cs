namespace CathodeRay.C;

/// <summary>Program po kontroli typów.</summary>
/// <param name="Functions">Funkcje.</param>
/// <param name="Globals">Globale.</param>
/// <param name="Warnings">Ostrzeżenia (np. zawężenie int→uchar).</param>
/// <param name="Lines">Linie węzłów (funkcje, instrukcje) do mapy debug.</param>
public sealed record CheckedProgram(
    IReadOnlyList<CheckedFunction> Functions,
    IReadOnlyList<TypedSymbol> Globals,
    List<string> Warnings,
    IReadOnlyDictionary<Ast.Node, int> Lines);
