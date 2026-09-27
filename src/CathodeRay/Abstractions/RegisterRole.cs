namespace CathodeRay.Abstractions;

/// <summary>Rola rejestru — do prezentacji, serializacji i walidacji bez zgadywania z nazwy.</summary>
public enum RegisterRole
{
    /// <summary>Rejestr ogólnego przeznaczenia.</summary>
    General,

    /// <summary>Akumulator (główny rejestr arytmetyczny).</summary>
    Accumulator,

    /// <summary>Rejestr indeksowy.</summary>
    Index,

    /// <summary>Wskaźnik stosu.</summary>
    StackPointer,

    /// <summary>Licznik programu.</summary>
    ProgramCounter,

    /// <summary>Rejestr statusu/flag.</summary>
    Status,

    /// <summary>Licznik (np. cykli/instrukcji).</summary>
    Counter,

    /// <summary>Inna rola.</summary>
    Other,
}
