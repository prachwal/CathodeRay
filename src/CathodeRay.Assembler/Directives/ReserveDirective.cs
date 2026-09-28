namespace CathodeRay.Assembler.Directives;

internal sealed class ReserveDirective : IDirective
{
    public void Execute(IAssemblyContext context, string? operand)
    {
        IReadOnlyList<string> items = OperandList.Split(operand ?? throw context.Error("count expected."));
        int count = context.Evaluate(items[0]);
        int fill = items.Count > 1 ? context.TryEvaluate(items[1]) ?? 0 : 0;
        if (count < 0 || items.Count > 2 || fill is < 0 or > 255)
        {
            throw context.Error("expected count[,fill] with count >= 0 and fill 0..255.");
        }

        for (int i = 0; i < count; i++)
        {
            context.Emit((byte)fill);
        }
    }
}
