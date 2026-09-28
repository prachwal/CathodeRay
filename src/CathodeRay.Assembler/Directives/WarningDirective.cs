namespace CathodeRay.Assembler.Directives;

/// <summary>Ostrzeżenie asemblacji (<c>.warning "tekst"</c>); nie przerywa.</summary>
internal sealed class WarningDirective : IDirective
{
    public void Execute(IAssemblyContext context, string? operand)
    {
        foreach (string item in OperandList.Split(operand ?? throw context.Error(".warning needs a message.")))
        {
            if (OperandList.TryUnquote(item, out string? text))
            {
                context.Notify(text, warning: true);
                return;
            }
        }

        throw context.Error(".warning needs a quoted message.");
    }
}
