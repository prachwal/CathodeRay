namespace CathodeRay.Abstractions;

/// <summary>Aktywność magistrali w trakcie instrukcji (flagi OR-owane raz na instrukcję, nie raz na cykl).</summary>
[Flags]
public enum BusActivity
{
    /// <summary>Brak aktywności.</summary>
    None = 0,

    /// <summary>Pobranie kodu operacji.</summary>
    Fetch = 1,

    /// <summary>Odczyt pamięci lub urządzenia.</summary>
    Read = 2,

    /// <summary>Zapis pamięci lub urządzenia.</summary>
    Write = 4,

    /// <summary>Dostęp do stosu (push/pop).</summary>
    Stack = 8,

    /// <summary>Wejście w stan zatrzymania (HLT).</summary>
    Halt = 16,

    /// <summary>Obsługa przerwania.</summary>
    Interrupt = 32,
}
