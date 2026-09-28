namespace CathodeRay.Assembler.Directives;

internal sealed class EndDirective : IDirective
{
    public void Execute(IAssemblyContext context, string? operand) => context.Stop();
}
