namespace CathodeRay.C;

/// <summary>Sygnatura funkcji (typ wyniku i parametrów) dla wskaźników do funkcji.</summary>
/// <param name="Return">Typ wyniku.</param>
/// <param name="Params">Typy parametrów.</param>
public sealed record FuncSig(CType Return, IReadOnlyList<CType> Params);
