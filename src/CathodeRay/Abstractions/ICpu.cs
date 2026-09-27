namespace CathodeRay.Abstractions;

/// <summary>Kontrakt CPU: krok, reset i dostęp do stanu. Zero logiki — tylko kształt.</summary>
/// <typeparam name="TState">Typ stanu tego CPU.</typeparam>
public interface ICpu<TState>
    where TState : ICpuState<TState>
{
    /// <summary>Aktualny stan (rejestry, PC, stos); snapshot przez <see cref="ICpuState{T}.Clone"/>.</summary>
    TState State { get; }

    /// <summary>Wykonuje jedną instrukcję i zwraca jej koszt w cyklach (0, gdy CPU czeka lub jest zatrzymany).</summary>
    /// <returns>Liczba cykli wykonanej instrukcji.</returns>
    int Step();

    /// <summary>Zeruje stan i ustawia licznik programu na adres startowy.</summary>
    void Reset();
}
