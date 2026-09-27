namespace CathodeRay.Abstractions;

/// <summary>Runner diagnostyczny: dekoruje <see cref="ICpuCore"/>, zdejmuje snapshoty przed/po kroku i woła <see cref="ICpuExecutionObserver"/>.
/// Introspekcja jest częścią <see cref="ICpuCore"/>, więc brak statusu jest błędem kompilacji, nie runtime.</summary>
public sealed class CpuDiagnostics
{
    private readonly ICpuCore _cpu;
    private readonly ICpuExecutionObserver _observer;

    /// <summary>Tworzy runner dla CPU i obserwatora.</summary>
    /// <param name="cpu">CPU do dekorowania.</param>
    /// <param name="observer">Obserwator (polityka breakpointów, log).</param>
    public CpuDiagnostics(ICpuCore cpu, ICpuExecutionObserver observer)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(observer);
        _cpu = cpu;
        _observer = observer;
    }

    /// <summary>Aktualny snapshot.</summary>
    public CpuDebugSnapshot Snapshot => CpuDebugSnapshot.From(_cpu);

    /// <summary>Czy obserwator żąda przerwania przed krokiem.</summary>
    /// <returns>Czy przerwać.</returns>
    public bool ShouldBreak() => _observer.ShouldBreak(Snapshot);

    /// <summary>Wykonuje jeden krok, buduje ślad i woła obserwatora (błąd kroku przekazuje do <see cref="ICpuExecutionObserver.OnStepFailed"/> i rzuca dalej).</summary>
    /// <returns>Ślad kroku.</returns>
    public CpuStepTrace Step() => StepFrom(Snapshot);

    /// <summary>Wykonuje kroki aż do breakpointu (<see cref="ICpuExecutionObserver.ShouldBreak"/>) lub limitu.
    /// Ślady trafiają do <see cref="ICpuExecutionObserver.OnStepCompleted"/> (nie są zbierane); snapshot "po" kroku służy jako "przed" następnego.</summary>
    /// <param name="maxSteps">Maksymalna liczba kroków.</param>
    /// <returns>Liczba wykonanych kroków.</returns>
    public int Run(int maxSteps = int.MaxValue)
    {
        int steps = 0;
        CpuDebugSnapshot current = Snapshot;
        while (steps < maxSteps && !_observer.ShouldBreak(current))
        {
            current = StepFrom(current).After;
            steps++;
        }

        return steps;
    }

    private CpuStepTrace StepFrom(CpuDebugSnapshot before)
    {
        int cycles;
        try
        {
            cycles = _cpu.Step();
        }
        catch (Exception exception)
        {
            _observer.OnStepFailed(before, exception);
            throw;
        }

        var trace = new CpuStepTrace(before, Snapshot, cycles);
        _observer.OnStepCompleted(trace);
        return trace;
    }
}
