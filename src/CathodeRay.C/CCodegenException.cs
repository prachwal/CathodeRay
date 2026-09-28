namespace CathodeRay.C;

using System.Diagnostics.CodeAnalysis;

/// <summary>Błąd generatora: konstrukcja poza podzbiorem v1.</summary>
[SuppressMessage("Roslynator", "RCS1194", Justification = "Prosty błąd z samym komunikatem; standardowe konstruktory byłyby martwe.")]
public sealed class CCodegenException : Exception
{
    /// <summary>Tworzy błąd.</summary>
    /// <param name="message">Opis.</param>
    public CCodegenException(string message)
        : base(message)
    {
    }
}
