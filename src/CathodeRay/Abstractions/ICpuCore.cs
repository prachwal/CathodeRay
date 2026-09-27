namespace CathodeRay.Abstractions;

/// <summary>Rdzeniowy kontrakt CPU: krok, reset i (wymuszona przez <see cref="ICpuStatus"/>) introspekcja. Bez typowanego stanu — stan przez <see cref="ICpu{TState}"/>.</summary>
public interface ICpuCore : ICpuStatus
{
    /// <summary>Wykonuje jedną instrukcję i zwraca jej koszt w cyklach (0, gdy CPU czeka lub jest zatrzymany).</summary>
    /// <returns>Liczba cykli wykonanej instrukcji.</returns>
    int Step();

    /// <summary>Zeruje stan i ustawia licznik programu na adres startowy.</summary>
    void Reset();
}
