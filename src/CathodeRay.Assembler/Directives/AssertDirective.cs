namespace CathodeRay.Assembler.Directives;

/// <summary>Asercja asemblacji (<c>.assert wyr, warning|error [, "tekst"]</c>, jak ca65).</summary>
internal sealed class AssertDirective : IDirective
{
    public void Execute(IAssemblyContext context, string? operand)
    {
        IReadOnlyList<string> items = OperandList.Split(operand ?? throw context.Error(".assert needs a condition."));
        if (items.Count is < 2 or > 3)
        {
            throw context.Error(".assert needs condition, warning|error [, message].");
        }

        bool warning = items[1].Trim().ToUpperInvariant() switch
        {
            "WARNING" or "LDWARNING" => true,
            "ERROR" or "LDERROR" => false,
            _ => throw context.Error($".assert action must be warning or error, got '{items[1].Trim()}'."),
        };
        int? value = context.TryEvaluate(items[0]);
        if (value is null)
        {
            throw context.Error($"'{items[0]}' must be known at this point (no forward references).");
        }

        if (value == 0)
        {
            string message = items.Count > 2 && OperandList.TryUnquote(items[2].Trim(), out string? text)
                ? text
                : $"assertion failed: {items[0].Trim()}";
            if (warning)
            {
                context.Notify(message, warning: true);
            }
            else
            {
                throw context.Error(message);
            }
        }
    }
}
