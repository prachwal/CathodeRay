namespace CathodeRay.C;

using System.Diagnostics.CodeAnalysis;

/// <summary>Błąd preprocesora mini-C (zła dyrektywa, brak pliku, cykl).</summary>
[SuppressMessage("Roslynator", "RCS1194", Justification = "Bez linii wyjątek nie ma sensu; standardowe konstruktory byłyby martwe.")]
public sealed class CPreprocessException : Exception
{
    /// <summary>Tworzy błąd.</summary>
    /// <param name="line">Linia (od 1, w strumieniu po splicie).</param>
    /// <param name="message">Opis.</param>
    public CPreprocessException(int line, string message)
        : base($"line {line}: {message}")
    {
        Line = line;
    }

    /// <summary>Linia (od 1).</summary>
    public int Line { get; }
}
