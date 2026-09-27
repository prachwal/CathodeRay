namespace CathodeRay.Abstractions;

/// <summary>Kontrakt introspekcji CPU: licznik programu, zatrzymanie, liczniki, ostatnia instrukcja i widok rejestrów.
/// Część <see cref="ICpuCore"/> — każdy rdzeń jest introspekowalny (panel/debugger/log konsumują go wprost).</summary>
public interface ICpuStatus
{
    /// <summary>Licznik programu.</summary>
    ushort ProgramCounter { get; }

    /// <summary>Czy CPU jest zatrzymany.</summary>
    bool Halted { get; }

    /// <summary>Suma cykli od resetu.</summary>
    ulong CycleCount { get; }

    /// <summary>Liczba wykonanych instrukcji od resetu.</summary>
    ulong InstructionCount { get; }

    /// <summary>Aktywność magistrali ostatniej instrukcji.</summary>
    BusActivity LastBusActivity { get; }

    /// <summary>Ostatni opcode (-1, gdy jeszcze nic nie wykonano).</summary>
    int LastOpcode { get; }

    /// <summary>Mnemonik ostatniej instrukcji (<see langword="null"/>, gdy brak).</summary>
    string? LastMnemonic { get; }

    /// <summary>Widok rejestrów (nazwa, wartość, szerokość) — snapshot bez żywych referencji do stanu.</summary>
    /// <returns>Widok rejestrów.</returns>
    RegisterView CaptureRegisters();
}
