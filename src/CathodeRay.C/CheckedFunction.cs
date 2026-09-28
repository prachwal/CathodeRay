namespace CathodeRay.C;

/// <summary>Funkcja po kontroli typów.</summary>
/// <param name="Def">Definicja źródłowa.</param>
/// <param name="Params">Parametry z typami.</param>
/// <param name="Locals">Zmienne lokalne z typami (kolejność deklaracji).</param>
public sealed record CheckedFunction(Ast.Function Def, IReadOnlyList<TypedSymbol> Params, IReadOnlyList<TypedSymbol> Locals);
