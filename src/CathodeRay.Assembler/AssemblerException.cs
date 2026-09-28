using System.Diagnostics.CodeAnalysis;

namespace CathodeRay.Assembler;

/// <summary>Błąd asemblacji wskazujący linię źródła.</summary>
[SuppressMessage("Roslynator", "RCS1194", Justification = "Bez numeru linii wyjątek nie ma sensu; standardowe konstruktory byłyby martwe.")]
public sealed class AssemblerException : Exception
{
    /// <summary>Tworzy błąd dla linii źródła.</summary>
    /// <param name="line">Numer linii (od 1).</param>
    /// <param name="message">Opis błędu.</param>
    public AssemblerException(int line, string message)
        : base($"line {line}: {message}")
    {
        Line = line;
    }

    /// <summary>Numer linii (od 1).</summary>
    public int Line { get; }
}
