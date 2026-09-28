namespace CathodeRay.Assembler.Directives;

using CathodeRay.Assembler.Syntax;

/// <summary>Eksportuje symbole (<c>GLOBAL a, b</c>); niezdefiniowany eksport to błąd po pass 1.</summary>
internal sealed class GlobalDirective : IDirective
{
    public void Execute(IAssemblyContext context, string? operand)
    {
        foreach (string name in OperandList.Split(operand ?? throw context.Error("symbol expected.")))
        {
            if (!Expression.IsIdentifier(name) || name.StartsWith('@'))
            {
                throw context.Error($"invalid symbol '{name}' (only global names can be exported).");
            }

            context.DeclareGlobal(name);
        }
    }
}
