namespace CathodeRay.C;

/// <summary>Symbol po kontroli typów (nazwa i typ do codegen).</summary>
/// <param name="Name">Nazwa.</param>
/// <param name="Type">Typ.</param>
/// <param name="Init">Inicjalizator globala (null = zero).</param>
public sealed record TypedSymbol(string Name, CType Type, Ast.Expr? Init = null);
