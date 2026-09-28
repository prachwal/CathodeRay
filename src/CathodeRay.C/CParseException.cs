namespace CathodeRay.C;

using System.Diagnostics.CodeAnalysis;

/// <summary>Błąd składni mini-C z pozycją (linie i kolumny od 1).</summary>
[SuppressMessage("Roslynator", "RCS1194", Justification = "Bez pozycji wyjątek nie ma sensu; standardowe konstruktory byłyby martwe.")]
public sealed class CParseException : Exception
{
    /// <summary>Tworzy błąd.</summary>
    /// <param name="line">Linia (od 1).</param>
    /// <param name="column">Kolumna (od 1).</param>
    /// <param name="message">Opis.</param>
    public CParseException(int line, int column, string message)
        : base($"line {line}, col {column}: {message}")
    {
        Line = line;
        Column = column;
    }

    /// <summary>Linia (od 1).</summary>
    public int Line { get; }

    /// <summary>Kolumna (od 1).</summary>
    public int Column { get; }
}
