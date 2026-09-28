namespace CathodeRay.Assembler.Directives;

/// <summary>Definicja makra liniowego (<c>.define</c>): zbierana w pre-passie (<c>MacroExpander</c>).
/// Wykonanie = linia poza pre-passem (błąd wewnętrzny).</summary>
internal sealed class DefineDirective : IDirective
{
    public void Execute(IAssemblyContext context, string? operand) =>
        throw context.Error("define directive outside pre-pass handling (internal error).");
}
