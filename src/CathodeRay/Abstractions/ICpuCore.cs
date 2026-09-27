namespace CathodeRay.Abstractions;

/// <summary>Rdzeniowy kontrakt CPU bez typowanego stanu: krok i reset (stan przez <see cref="ICpu{TState}"/>, introspekcja przez <see cref="ICpuStatus"/>).</summary>
public interface ICpuCore
{
    /// <summary>Wykonuje jedną instrukcję i zwraca jej koszt w cyklach (0, gdy CPU czeka lub jest zatrzymany).</summary>
    /// <returns>Liczba cykli wykonanej instrukcji.</returns>
    int Step();

    /// <summary>Zeruje stan i ustawia licznik programu na adres startowy.</summary>
    void Reset();
}
