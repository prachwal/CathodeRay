using System.Diagnostics.CodeAnalysis;

namespace CathodeRay.Assembler;

/// <summary>Błąd asemblacji wskazujący linię źródła (i plik, gdy asemblacja z kontekstem pliku).</summary>
[SuppressMessage("Roslynator", "RCS1194", Justification = "Bez numeru linii wyjątek nie ma sensu; standardowe konstruktory byłyby martwe.")]
public sealed class AssemblerException : Exception
{
    /// <summary>Tworzy błąd dla linii źródła.</summary>
    /// <param name="line">Numer linii (od 1).</param>
    /// <param name="message">Opis błędu.</param>
    /// <param name="file">Plik źródła lub <see langword="null"/> (format komunikatu bez zmian).</param>
    public AssemblerException(int line, string message, string? file = null)
        : base($"line {line}: {message}")
    {
        Line = line;
        File = file;
    }

    /// <summary>Numer linii w pliku (od 1).</summary>
    public int Line { get; }

    /// <summary>Plik źródła lub <see langword="null"/> (asemblacja z samego tekstu).</summary>
    public string? File { get; }
}
