namespace CathodeRay.Abstractions;

/// <summary>Kontrakt CPU z typowanym stanem (krok i reset w <see cref="ICpuCore"/>).</summary>
/// <typeparam name="TState">Typ stanu tego CPU.</typeparam>
public interface ICpu<TState> : ICpuCore
    where TState : ICpuState<TState>
{
    /// <summary>Aktualny stan (rejestry, PC, stos); snapshot przez <see cref="ICpuState{T}.Clone"/>.</summary>
    TState State { get; }
}
