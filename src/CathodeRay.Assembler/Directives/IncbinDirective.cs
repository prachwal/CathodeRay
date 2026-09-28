namespace CathodeRay.Assembler.Directives;

/// <summary>Wstawia plik binarny do obrazu (<c>.incbin</c>, <c>INCBIN</c>).</summary>
internal sealed class IncbinDirective : IDirective
{
    public void Execute(IAssemblyContext context, string? operand)
    {
        foreach (byte b in context.ReadBinaryFile(operand))
        {
            context.Emit(b);
        }
    }
}
