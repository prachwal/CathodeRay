namespace CathodeRay.C;

/// <summary>Generator kodu mini-C: program po kontroli typów obniżany jest do kodu pośredniego (<see cref="Ir"/>),
/// a wybrany cel (<see cref="ICTarget"/>) drukuje go jako asembler swojego procesora. Konwencje stuba:
/// <c>docs/stub-calling-conv.md</c>; specyfikacja języka: <c>docs/minic.md</c>.</summary>
public static class Codegen
{
    /// <summary>Generuje tekst asemblera dla celu domyślnego (stub).</summary>
    /// <param name="program">Program po kontroli typów.</param>
    /// <param name="fileName">Nazwa pliku C do adnotacji <c>;c:</c> (null = sama linia).</param>
    /// <param name="objectMode">Tryb obiektowy (linker): emituje <c>.extern</c> dla prototypów.</param>
    /// <param name="optimize">Optymalizacje celu (dla stuba: <see cref="Peephole"/>).</param>
    /// <returns>Źródło dla asemblera (start zapewnia crt0 celu, linkowany zawsze pierwszy).</returns>
    public static string Emit(CheckedProgram program, string? fileName = null, bool objectMode = false, bool optimize = true) =>
        Emit(program, CTargets.Default, fileName, objectMode, optimize);

    /// <summary>Generuje tekst asemblera dla wybranego celu.</summary>
    /// <param name="program">Program po kontroli typów.</param>
    /// <param name="target">Cel (drukuje kod pośredni jako asembler).</param>
    /// <param name="fileName">Nazwa pliku C do adnotacji <c>;c:</c> (null = sama linia).</param>
    /// <param name="objectMode">Tryb obiektowy (linker): emituje <c>.extern</c> dla prototypów.</param>
    /// <param name="optimize">Optymalizacje celu.</param>
    /// <returns>Źródło dla asemblera celu.</returns>
    public static string Emit(CheckedProgram program, ICTarget target, string? fileName = null, bool objectMode = false, bool optimize = true)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.Emit(Lower(program, fileName, objectMode, target.StackLimit), optimize);
    }

    /// <summary>Obniża program do kodu pośredniego.</summary>
    /// <param name="program">Program po kontroli typów.</param>
    /// <param name="fileName">Nazwa pliku C do adnotacji <c>;c:</c> (null = sama linia).</param>
    /// <param name="objectMode">Tryb obiektowy (linker): moduł deklaruje symbole zewnętrzne.</param>
    /// <param name="stackLimit">Rozmiar stosu sprzętowego do kontroli głębokości wołań (null = bez kontroli).</param>
    /// <returns>Moduł IR.</returns>
    public static Ir.Module Lower(CheckedProgram program, string? fileName = null, bool objectMode = false, int? stackLimit = 256)
    {
        ArgumentNullException.ThrowIfNull(program);
        return new Lowering(program, fileName, objectMode, stackLimit).Run();
    }
}
