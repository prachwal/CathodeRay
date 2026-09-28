using System.Diagnostics.CodeAnalysis;

namespace CathodeRay.Assembler;

/// <summary>Błąd asemblacji: lista wszystkich błędów (kolejność źródła), nie tylko pierwszy.
/// Kontrakt: przebieg zbiera błędy per linia i kontynuuje (synchronizacja PC najlepszym
/// wysiłkiem: instrukcja, której formy nie dało się wybrać, przesuwa PC o rozmiar najdłuższej
/// formy mnemonika); pass 2 startuje tylko gdy pass 1 czysty (brak kaskad); po 20 błędach
/// przebieg staje z adnotacją; <see cref="Line"/>, <see cref="File"/> i komunikat opisują
/// pierwszy błąd (kompatybilność wsteczna).</summary>
[SuppressMessage("Roslynator", "RCS1194", Justification = "Bez numeru linii wyjątek nie ma sensu; standardowe konstruktory byłyby martwe.")]
public sealed class AssemblerException : Exception
{
    /// <summary>Tworzy błąd dla linii źródła.</summary>
    /// <param name="line">Numer linii (od 1).</param>
    /// <param name="message">Opis błędu.</param>
    /// <param name="file">Plik źródła lub <see langword="null"/> (format komunikatu bez zmian).</param>
    public AssemblerException(int line, string message, string? file = null)
        : this([new AssemblerError(file, line, message)])
    {
    }

    /// <summary>Tworzy agregat błędów.</summary>
    /// <param name="errors">Niepusta lista błędów w kolejności źródła.</param>
    public AssemblerException(IReadOnlyList<AssemblerError> errors)
        : base(errors.Count == 0
            ? throw new ArgumentException("Error list must not be empty.", nameof(errors))
            : $"line {errors[0].Line}: {errors[0].Message}" + (errors.Count > 1 ? $" (+{errors.Count - 1} more)" : string.Empty))
    {
        Errors = errors;
    }

    /// <summary>Wszystkie błędy w kolejności źródła.</summary>
    public IReadOnlyList<AssemblerError> Errors { get; }

    /// <summary>Numer linii pierwszego błędu (od 1).</summary>
    public int Line => Errors[0].Line;

    /// <summary>Plik pierwszego błędu lub <see langword="null"/>.</summary>
    public string? File => Errors[0].File;
}
