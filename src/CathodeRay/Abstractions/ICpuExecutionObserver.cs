namespace CathodeRay.Abstractions;

/// <summary>Obserwator wykonania CPU. Polityka (breakpoint, watchpoint, log) należy do obserwatora, nie do CPU — metody mają domyślne no-opy.</summary>
public interface ICpuExecutionObserver
{
    /// <summary>Zwraca <see langword="true"/>, gdy krok ma się nie wykonać (breakpoint przed wykonaniem).</summary>
    /// <param name="snapshot">Stan przed krokiem.</param>
    /// <returns>Czy przerwać.</returns>
    bool ShouldBreak(CpuDebugSnapshot snapshot) => false;

    /// <summary>Zwraca <see langword="true"/>, gdy ten dostęp do magistrali trafia watchpoint.</summary>
    /// <param name="access">Dostęp (adres, wartość, odczyt/zapis).</param>
    /// <returns>Czy to trafienie watchpointu.</returns>
    bool ShouldBreakOnMemoryAccess(BusAccess access) => false;

    /// <summary>Odbiera zakończony krok.</summary>
    /// <param name="trace">Ślad kroku.</param>
    void OnStepCompleted(CpuStepTrace trace)
    {
    }

    /// <summary>Odbiera wyjątek rzucony w trakcie kroku.</summary>
    /// <param name="snapshot">Stan przed krokiem.</param>
    /// <param name="exception">Rzucony wyjątek.</param>
    void OnStepFailed(CpuDebugSnapshot snapshot, Exception exception)
    {
    }
}
