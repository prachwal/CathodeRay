namespace CathodeRay.Assembler.Directives;

/// <summary>Wyrównuje adres do wielokrotności N, dopełniając wypełnieniem:
/// <c>.align N[,wypełnienie]</c>, <c>ALIGN N[,wypełnienie]</c>. Już wyrównany = bez zmian.</summary>
internal sealed class AlignDirective : IDirective
{
    public void Execute(IAssemblyContext context, string? operand)
    {
        IReadOnlyList<string> items = OperandList.Split(operand ?? throw context.Error("alignment expected."));
        int alignment = context.Evaluate(items[0]);
        int fill = items.Count > 1 ? context.TryEvaluate(items[1]) ?? 0 : 0;
        if (alignment < 1 || items.Count > 2 || fill is < 0 or > 255)
        {
            throw context.Error("expected alignment[,fill] with alignment >= 1 and fill 0..255.");
        }

        while (context.ProgramCounter % alignment != 0)
        {
            context.Emit((byte)fill);
        }
    }
}
