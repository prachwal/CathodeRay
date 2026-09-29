namespace CathodeRay.C;

/// <summary>Moduł biblioteki standardowej (osadzony w pliku wykonywalnym).</summary>
/// <param name="Name">Nazwa pliku (<c>strlen.c</c>, <c>io.s</c>).</param>
/// <param name="Source">Tekst źródłowy.</param>
/// <param name="IsAssembly">Moduł w asemblerze (<c>.s</c>), nie w C.</param>
/// <param name="Defines">Funkcje eksportowane przez moduł.</param>
public sealed record StdModule(string Name, string Source, bool IsAssembly, IReadOnlySet<string> Defines);
