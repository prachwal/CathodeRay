using CathodeRay.Assembler.Isa;

namespace CathodeRay.Assembler.Directives;

/// <summary>Stan asemblacji widoczny dla dyrektyw.</summary>
public interface IAssemblyContext
{
    /// <summary>Bieżący adres.</summary>
    int ProgramCounter { get; }

    /// <summary>Kolejność bajtów celu.</summary>
    Endianness Endianness { get; }

    /// <summary>Ustawia bieżący adres.</summary>
    /// <param name="address">Adres 0..65535.</param>
    void SetProgramCounter(int address);

    /// <summary>Emituje bajt pod bieżący adres i przesuwa go.</summary>
    /// <param name="value">Bajt.</param>
    void Emit(byte value);

    /// <summary>Ewaluuje wyrażenie; <see langword="null"/>, gdy w tym przebiegu symbol nie jest jeszcze znany.</summary>
    /// <param name="expression">Wyrażenie.</param>
    /// <returns>Wartość lub <see langword="null"/>.</returns>
    int? TryEvaluate(string expression);

    /// <summary>Ewaluuje wyrażenie, które musi być znane już w pierwszym przebiegu (np. adres <c>.org</c>).</summary>
    /// <param name="expression">Wyrażenie.</param>
    /// <returns>Wartość.</returns>
    int Evaluate(string expression);

    /// <summary>Kończy asemblację (dyrektywa END).</summary>
    void Stop();

    /// <summary>Przełącza bieżący segment (licznik per segment, adresy absolutne).</summary>
    /// <param name="name">Nazwa segmentu (bez rozróżniania wielkości liter).</param>
    /// <param name="emit">Czy segment emituje bajty; <see langword="null"/> = bez zmian (nowy segment emituje).</param>
    void SwitchSegment(string name, bool? emit);

    /// <summary>Czyta plik binarny dla <c>.incbin</c>: cudzysłowy, ścieżki jak w <c>.include</c>.</summary>
    /// <param name="operand">Tekst operandu z nazwą pliku lub <see langword="null"/>.</param>
    /// <returns>Bajty pliku.</returns>
    /// <exception cref="AssemblerException">Brak kontekstu pliku, brak pliku lub zła nazwa.</exception>
    byte[] ReadBinaryFile(string? operand);

    /// <summary>Tworzy błąd przypisany do bieżącej linii.</summary>
    /// <param name="message">Opis.</param>
    /// <returns>Wyjątek do rzucenia.</returns>
    AssemblerException Error(string message);
}
