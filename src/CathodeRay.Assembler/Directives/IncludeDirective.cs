namespace CathodeRay.Assembler.Directives;

/// <summary>Znacznik <c>.include</c>/<c>INCLUDE</c>: ekspansja w <see cref="SourceLoader"/> przed pierwszym przebiegiem.
/// Nigdy nie wykonuje się w przebiegu (linie znikają przy ekspansji); wykonanie = asemblacja z samego tekstu.</summary>
internal sealed class IncludeDirective : IDirective
{
    public void Execute(IAssemblyContext context, string? operand) =>
        throw context.Error(".include needs file context (assemble a file via CLI, not bare text).");
}
