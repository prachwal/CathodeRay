namespace CathodeRay.Abstractions;

/// <summary>Runner diagnostyczny: dekoruje <see cref="ICpuCore"/>, zdejmuje snapshoty przed/po kroku i woła <see cref="ICpuExecutionObserver"/>.
/// Wymaga <see cref="ICpuStatus"/> (introspekcja obowiązkowa) — brak zdolności to jawny błąd, nie ciche zerowe dane.</summary>
public sealed class CpuDiagnostics
{
    private readonly ICpuCore _cpu;
    private readonly ICpuStatus _status;
    private readonly ICpuExecutionObserver _observer;

    /// <summary>Tworzy runner dla CPU i obserwatora.</summary>
    /// <param name="cpu">CPU do dekorowania (musi implementować <see cref="ICpuStatus"/>).</param>
    /// <param name="observer">Obserwator (polityka breakpointów, log).</param>
    /// <exception cref="ArgumentException">CPU nie implementuje <see cref="ICpuStatus"/>.</exception>
    public CpuDiagnostics(ICpuCore cpu, ICpuExecutionObserver observer)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(observer);
        _cpu = cpu;
        _status = cpu as ICpuStatus
            ?? throw new ArgumentException(
                $"CPU {cpu.GetType().Name} must implement {nameof(ICpuStatus)}.", nameof(cpu));
        _observer = observer;
    }

    /// <summary>Aktualny snapshot.</summary>
    public CpuDebugSnapshot Snapshot => CpuDebugSnapshot.From(_status);

    /// <summary>Czy obserwator żąda przerwania przed krokiem.</summary>
    /// <returns>Czy przerwać.</returns>
    public bool ShouldBreak() => _observer.ShouldBreak(Snapshot);

    /// <summary>Wykonuje jeden krok, buduje ślad i woła obserwatora (błąd kroku przekazuje do <see cref="ICpuExecutionObserver.OnStepFailed"/> i rzuca dalej).</summary>
    /// <returns>Ślad kroku.</returns>
    public CpuStepTrace Step()
    {
        CpuDebugSnapshot before = Snapshot;
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

    /// <summary>Wykonuje kroki aż do breakpointu (<see cref="ICpuExecutionObserver.ShouldBreak"/>) lub limitu.</summary>
    /// <param name="maxSteps">Maksymalna liczba kroków.</param>
    /// <returns>Zebrane ślady.</returns>
    public IReadOnlyList<CpuStepTrace> Run(int maxSteps = int.MaxValue)
    {
        var traces = new List<CpuStepTrace>();
        while (traces.Count < maxSteps && !ShouldBreak())
        {
            traces.Add(Step());
        }

        return traces;
    }
}
