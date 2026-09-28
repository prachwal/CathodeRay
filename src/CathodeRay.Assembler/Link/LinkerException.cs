namespace CathodeRay.Assembler.Link;

using System.Diagnostics.CodeAnalysis;

/// <summary>Błąd linkera (konfiguracja, duplikat/brak symbolu, overflow relokacji).</summary>
[SuppressMessage("Roslynator", "RCS1194", Justification = "Prosty błąd z samym komunikatem; standardowe konstruktory byłyby martwe.")]
public sealed class LinkerException : Exception
{
    /// <summary>Tworzy błąd linkera.</summary>
    /// <param name="message">Opis błędu.</param>
    public LinkerException(string message)
        : base(message)
    {
    }
}
