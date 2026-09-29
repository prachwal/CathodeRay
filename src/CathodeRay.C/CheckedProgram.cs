namespace CathodeRay.C;

/// <summary>Program po kontroli typów.</summary>
/// <param name="Functions">Funkcje.</param>
/// <param name="Globals">Globale.</param>
/// <param name="Warnings">Ostrzeżenia (np. zawężenie int→uchar).</param>
/// <param name="Lines">Linie węzłów (funkcje, instrukcje) do mapy debug.</param>
/// <param name="GlobalTypes">Typy węzłów w inicjalizatorach globali (kod startowy).</param>
/// <param name="Constants">Wartości stałych wyrażeń (16-bit), policzone przez checker.</param>
public sealed record CheckedProgram(
    IReadOnlyList<CheckedFunction> Functions,
    IReadOnlyList<TypedSymbol> Globals,
    List<string> Warnings,
    IReadOnlyDictionary<Ast.Node, int> Lines,
    IReadOnlyDictionary<Ast.Expr, CType>? GlobalTypes = null,
    IReadOnlyDictionary<Ast.Expr, int>? Constants = null);
