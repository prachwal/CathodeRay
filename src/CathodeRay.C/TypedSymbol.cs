namespace CathodeRay.C;

/// <summary>Symbol po kontroli typów (nazwa i typ do codegen).</summary>
/// <param name="Name">Nazwa.</param>
/// <param name="Type">Typ.</param>
public sealed record TypedSymbol(string Name, CType Type);
