namespace CathodeRay.Assembler;

/// <summary>Pojedynczy błąd asemblacji: lokalizacja i opis (bez formatowania).</summary>
/// <param name="File">Plik źródła lub <see langword="null"/> (asemblacja z samego tekstu).</param>
/// <param name="Line">Numer linii w pliku (od 1).</param>
/// <param name="Message">Opis błędu.</param>
public sealed record AssemblerError(string? File, int Line, string Message);
