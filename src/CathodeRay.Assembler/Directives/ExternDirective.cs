using CathodeRay.Assembler.Syntax;

namespace CathodeRay.Assembler.Directives;

/// <summary>Deklaruje symbole zewnętrzne (<c>EXTERN a, b</c>); odwołania trafiają do relokacji.</summary>
internal sealed class ExternDirective : IDirective
{
    public void Execute(IAssemblyContext context, string? operand)
    {
        foreach (string name in OperandList.Split(operand ?? throw context.Error("symbol expected.")))
        {
            if (!Expression.IsIdentifier(name) || name.StartsWith('@'))
            {
                throw context.Error($"invalid symbol '{name}' (only global names can be external).");
            }

            context.DeclareExternal(name);
        }
    }
}
