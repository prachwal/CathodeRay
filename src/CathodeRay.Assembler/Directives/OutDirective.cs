namespace CathodeRay.Assembler.Directives;

/// <summary>Wypisuje komunikat (<c>.out "tekst"</c>).</summary>
internal sealed class OutDirective : IDirective
{
    public void Execute(IAssemblyContext context, string? operand) =>
        context.Notify(Message(context, operand), warning: false);

    private static string Message(IAssemblyContext context, string? operand)
    {
        foreach (string item in OperandList.Split(operand ?? throw context.Error(".out needs a message.")))
        {
            if (OperandList.TryUnquote(item, out string? text))
            {
                return text;
            }
        }

        throw context.Error(".out needs a quoted message.");
    }
}
