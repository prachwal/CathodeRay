namespace CathodeRay.C;

using System.Diagnostics.CodeAnalysis;

/// <summary>Błąd typów mini-C (bez pozycji — pozycje ma tylko składnia).</summary>
[SuppressMessage("Roslynator", "RCS1194", Justification = "Prosty błąd z samym komunikatem; standardowe konstruktory byłyby martwe.")]
public sealed class CTypeException : Exception
{
    /// <summary>Tworzy błąd.</summary>
    /// <param name="message">Opis.</param>
    public CTypeException(string message)
        : base(message)
    {
    }

    /// <summary>Linia źródła C (0 = nieznana); uzupełniana przy przejściu przez instrukcję.</summary>
    public int Line { get; set; }

    /// <summary>Plik C (uzupełnia sterownik <c>cc</c>).</summary>
    public string? File { get; set; }
}
