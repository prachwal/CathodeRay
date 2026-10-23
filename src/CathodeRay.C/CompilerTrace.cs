namespace CathodeRay.C;

/// <summary>Bieżący odbiornik diagnostyki (per wątek/przepływ asynchroniczny).</summary>
public static class CompilerTrace
{
    private static readonly AsyncLocal<ICompilerTrace?> Slot = new();

    /// <summary>Aktywny odbiornik albo null.</summary>
    internal static ICompilerTrace? Current => Slot.Value;

    /// <summary>Włącza odbiornik do zwolnienia zwróconego obiektu.</summary>
    /// <param name="trace">Odbiornik.</param>
    /// <returns>Zakres, którego <see cref="IDisposable.Dispose"/> przywraca poprzedni odbiornik.</returns>
    public static IDisposable Begin(ICompilerTrace trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        ICompilerTrace? previous = Slot.Value;
        Slot.Value = trace;
        return new Scope(previous);
    }

    private sealed class Scope(ICompilerTrace? previous) : IDisposable
    {
        public void Dispose() => Slot.Value = previous;
    }
}
