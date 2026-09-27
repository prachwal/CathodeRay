using System.Diagnostics.CodeAnalysis;

namespace CathodeRay.Stub;

/// <summary>Rejestr opcode zaślepki (kształt jak <c>PetEmulator.Core.OpcodeTable</c>): Add (duplikat = wyjątek), Replace, Seal, Get/TryGet.
/// Tablica 256 wpisów indeksowana bajtem opcode.</summary>
public sealed class StubOpcodeTable
{
    private readonly StubOpcodeEntry?[] _entries = new StubOpcodeEntry?[256];
    private bool _sealed;

    /// <summary>Czy rejestr zamknięty (<see cref="Seal"/>).</summary>
    public bool IsSealed => _sealed;

    /// <summary>Dodaje wpis; duplikat opcode = wyjątek.</summary>
    /// <param name="opcode">Klucz opcode.</param>
    /// <param name="entry">Wpis.</param>
    public void Add(byte opcode, StubOpcodeEntry entry)
    {
        EnsureMutable();
        if (_entries[opcode] is not null)
        {
            throw new InvalidOperationException($"Opcode 0x{opcode:X2} already registered.");
        }

        _entries[opcode] = entry;
    }

    /// <summary>Dodaje lub nadpisuje wpis.</summary>
    /// <param name="opcode">Klucz opcode.</param>
    /// <param name="entry">Wpis.</param>
    public void Replace(byte opcode, StubOpcodeEntry entry)
    {
        EnsureMutable();
        _entries[opcode] = entry;
    }

    /// <summary>Usuwa wpis (brak wpisu = wyjątek).</summary>
    /// <param name="opcode">Klucz opcode.</param>
    public void Remove(byte opcode)
    {
        EnsureMutable();
        if (_entries[opcode] is null)
        {
            throw new InvalidOperationException($"Opcode 0x{opcode:X2} is not registered.");
        }

        _entries[opcode] = null;
    }

    /// <summary>Zamraża rejestr — dalsze mutacje rzucają wyjątek.</summary>
    /// <returns>Ten sam rejestr.</returns>
    public StubOpcodeTable Seal()
    {
        _sealed = true;
        return this;
    }

    /// <summary>Pobiera wpis.</summary>
    /// <param name="opcode">Klucz opcode.</param>
    /// <returns>Wpis.</returns>
    /// <exception cref="KeyNotFoundException">Brak wpisu dla opcode.</exception>
    public StubOpcodeEntry Get(byte opcode) =>
        _entries[opcode] ?? throw new KeyNotFoundException($"Opcode 0x{opcode:X2} is not registered.");

    /// <summary>Próbuje pobrać wpis.</summary>
    /// <param name="opcode">Klucz opcode.</param>
    /// <param name="entry">Wpis lub <see langword="null"/>.</param>
    /// <returns>Czy wpis istnieje.</returns>
    public bool TryGet(byte opcode, [NotNullWhen(true)] out StubOpcodeEntry? entry)
    {
        entry = _entries[opcode];
        return entry is not null;
    }

    private void EnsureMutable()
    {
        if (_sealed)
        {
            throw new InvalidOperationException("Opcode table is sealed.");
        }
    }
}
