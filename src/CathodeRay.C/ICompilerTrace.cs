namespace CathodeRay.C;

/// <summary>Odbiornik diagnostyki kompilatora: kod pośredni po kolejnych etapach i decyzje alokatora rejestrów.
/// Włączany tylko na czas wywołania (<see cref="CompilerTrace.Begin"/>); bez odbiornika kompilator niczego nie liczy ani nie wypisuje.</summary>
public interface ICompilerTrace
{
    /// <summary>Kod pośredni po etapie.</summary>
    /// <param name="stage">Nazwa etapu: <c>lowered</c> (po obniżeniu i inlinowaniu) albo <c>vreg</c> (po przebiegach VReg).</param>
    /// <param name="module">Moduł IR.</param>
    void Ir(string stage, Ir.Module module);

    /// <summary>Przydział rejestrów celu (tylko cele z alokatorem: z80, 8080).</summary>
    /// <param name="module">Moduł, dla którego policzono przydział.</param>
    /// <param name="assignment">Symbol komórki → rejestr albo para.</param>
    /// <param name="causes">Funkcja → (symbol, przyczyna): <c>register</c> albo powód, dla którego komórka zostaje w pamięci.</param>
    void Allocation(Ir.Module module, IReadOnlyDictionary<string, string> assignment, IReadOnlyDictionary<string, List<(string Sym, string Cause)>> causes);
}
