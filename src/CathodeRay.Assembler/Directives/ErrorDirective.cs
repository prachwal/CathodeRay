namespace CathodeRay.Assembler.Directives;

/// <summary>Błąd użytkownika (<c>.error "tekst"</c>); przerywa jak inne błędy (agregowany).</summary>
internal sealed class ErrorDirective : IDirective
{
    public void Execute(IAssemblyContext context, string? operand)
    {
        foreach (string item in OperandList.Split(operand ?? throw context.Error(".error needs a message.")))
        {
            if (OperandList.TryUnquote(item, out string? text))
            {
                throw context.Error($"user error: {text}");
            }
        }

        throw context.Error(".error needs a quoted message.");
    }
}
