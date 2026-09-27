namespace CathodeRay.Abstractions;

/// <summary>Dekorator <see cref="IBus"/>: przy każdym dostępie pyta obserwatora o watchpoint i zapamiętuje trafienie.
/// Polityka zostaje w obserwatorze; CPU i "goła" szyna nic o niej nie wiedzą.</summary>
public sealed class WatchpointBus : IBus
{
    private readonly IBus _inner;
    private readonly ICpuExecutionObserver _observer;

    /// <summary>Tworzy dekorator nad szyną.</summary>
    /// <param name="inner">Szyna wewnętrzna.</param>
    /// <param name="observer">Obserwator dostarczający politykę watchpointów.</param>
    public WatchpointBus(IBus inner, ICpuExecutionObserver observer)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(observer);
        _inner = inner;
        _observer = observer;
    }

    /// <summary>Ostatni dostęp oznaczony przez obserwatora (<see langword="null"/> = brak trafienia).</summary>
    public BusAccess? LastHit { get; private set; }

    /// <inheritdoc/>
    public byte Read(ushort address)
    {
        byte value = _inner.Read(address);
        Inspect(new BusAccess(address, value, IsWrite: false));
        return value;
    }

    /// <inheritdoc/>
    public void Write(ushort address, byte value)
    {
        _inner.Write(address, value);
        Inspect(new BusAccess(address, value, IsWrite: true));
    }

    private void Inspect(BusAccess access)
    {
        if (_observer.ShouldBreakOnMemoryAccess(access))
        {
            LastHit = access;
        }
    }
}
