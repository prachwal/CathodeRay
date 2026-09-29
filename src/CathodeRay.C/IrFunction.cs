namespace CathodeRay.C;

/// <summary>Funkcja modułu: instrukcje od komentarza <c>;c:</c> i <c>.proc</c> po <c>.endproc</c>.</summary>
/// <param name="Name">Nazwa funkcji.</param>
/// <param name="IsStatic">Symbol lokalny modułu.</param>
/// <param name="Body">Instrukcje.</param>
public sealed record IrFunction(string Name, bool IsStatic, IReadOnlyList<Ins> Body) : IrItem;
