namespace CathodeRay.Assembler.Directives;

/// <summary>Dyrektywa asemblera. Ten sam kod działa w obu przebiegach: w pierwszym <see cref="IAssemblyContext.Emit"/>
/// tylko przesuwa adres, w drugim zapisuje bajty. Dialekt mapuje swoje nazwy (<c>.byte</c>, <c>DB</c>) na implementacje.</summary>
public interface IDirective
{
    /// <summary>Wykonuje dyrektywę.</summary>
    /// <param name="context">Stan asemblacji.</param>
    /// <param name="operand">Tekst operandu lub <see langword="null"/>.</param>
    void Execute(IAssemblyContext context, string? operand);
}
