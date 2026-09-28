namespace CathodeRay.Assembler.Directives;

internal sealed class OrgDirective : IDirective
{
    public void Execute(IAssemblyContext context, string? operand) =>
        context.SetProgramCounter(context.Evaluate(operand ?? throw context.Error("address expected.")));
}
