namespace CathodeRay.Abstractions;

/// <summary>Neutralny snapshot diagnostyczny CPU (bez typowanego stanu) — wspólny dla obserwatorów, logu i narzędzi.</summary>
/// <param name="ProgramCounter">Licznik programu.</param>
/// <param name="Halted">Czy CPU zatrzymany.</param>
/// <param name="CycleCount">Suma cykli od resetu.</param>
/// <param name="InstructionCount">Liczba wykonanych instrukcji od resetu.</param>
/// <param name="LastBusActivity">Aktywność magistrali ostatniej instrukcji.</param>
/// <param name="LastOpcode">Ostatni opcode (-1, gdy brak).</param>
/// <param name="LastMnemonic">Mnemonik ostatniej instrukcji (null, gdy brak).</param>
/// <param name="Registers">Widok rejestrów.</param>
public readonly record struct CpuDebugSnapshot(
    ushort ProgramCounter,
    bool Halted,
    ulong CycleCount,
    ulong InstructionCount,
    BusActivity LastBusActivity,
    int LastOpcode,
    string? LastMnemonic,
    RegisterView Registers)
{
    /// <summary>Pusty snapshot (CPU bez introspekcji).</summary>
    public static CpuDebugSnapshot Empty { get; } = new(
        0, false, 0, 0, BusActivity.None, -1, null, RegisterView.Empty);

    /// <summary>Buduje snapshot z opcjonalnej zdolności <see cref="ICpuStatus"/>.</summary>
    /// <param name="status">Status CPU.</param>
    /// <returns>Snapshot.</returns>
    public static CpuDebugSnapshot From(ICpuStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return new CpuDebugSnapshot(
            status.ProgramCounter,
            status.Halted,
            status.CycleCount,
            status.InstructionCount,
            status.LastBusActivity,
            status.LastOpcode,
            status.LastMnemonic,
            status.CaptureRegisters());
    }
}
