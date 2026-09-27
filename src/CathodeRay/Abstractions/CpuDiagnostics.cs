namespace CathodeRay.Abstractions;

/// <summary>Runner diagnostyczny: dekoruje <see cref="ICpu{TState}"/>, zdejmuje snapshoty przed/po kroku i woła <see cref="ICpuExecutionObserver"/>.
/// Działa z każdym CPU; gdy CPU implementuje opcjonalny <see cref="ICpuStatus"/>, snapshoty niosą realny stan, inaczej są domyślne (degradacja).</summary>
/// <typeparam name="TState">Typ stanu CPU.</typeparam>
public sealed class CpuDiagnostics<TState>
    where TState : ICpuState<TState>
{
    private readonly ICpu<TState> _cpu;
    private readonly ICpuExecutionObserver _observer;
    private readonly ICpuStatus? _status;

    /// <summary>Tworzy runner dla CPU i obserwatora.</summary>
    /// <param name="cpu">CPU do dekorowania.</param>
    /// <param name="observer">Obserwator (polityka breakpointów, log).</param>
    public CpuDiagnostics(ICpu<TState> cpu, ICpuExecutionObserver observer)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(observer);
        _cpu = cpu;
        _observer = observer;
        _status = cpu as ICpuStatus;
    }

    /// <summary>Czy CPU daje introspekcję (implementuje <see cref="ICpuStatus"/>).</summary>
    public bool HasStatus => _status is not null;

    /// <summary>Aktualny snapshot (domyślny, gdy CPU nie ma <see cref="ICpuStatus"/>).</summary>
    public CpuDebugSnapshot Snapshot => _status is null ? default : CpuDebugSnapshot.From(_status);

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
