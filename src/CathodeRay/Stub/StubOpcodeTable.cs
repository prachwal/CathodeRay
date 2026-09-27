namespace CathodeRay.Stub;

/// <summary>Rejestr opcode zaślepki (kształt jak <c>PetEmulator.Core.OpcodeTable</c>): Add (duplikat = wyjątek), Replace, Seal, Get/TryGet.</summary>
public sealed class StubOpcodeTable
{
    private readonly Dictionary<byte, StubOpcodeEntry> _entries = new();
    private bool _sealed;

    /// <summary>Zarejestrowane wpisy.</summary>
    public IReadOnlyCollection<StubOpcodeEntry> Entries => _entries.Values;

    /// <summary>Czy rejestr zamknięty (<see cref="Seal"/>).</summary>
    public bool IsSealed => _sealed;

    /// <summary>Dodaje wpis; duplikat opcode = wyjątek.</summary>
    /// <param name="opcode">Klucz opcode.</param>
    /// <param name="entry">Wpis.</param>
    public void Add(byte opcode, StubOpcodeEntry entry)
    {
        EnsureMutable();
        if (!_entries.TryAdd(opcode, entry))
        {
            throw new InvalidOperationException($"Opcode 0x{opcode:X2} already registered.");
        }
    }

    /// <summary>Dodaje lub nadpisuje wpis.</summary>
    /// <param name="opcode">Klucz opcode.</param>
    /// <param name="entry">Wpis.</param>
    public void Replace(byte opcode, StubOpcodeEntry entry)
    {
        EnsureMutable();
        _entries[opcode] = entry;
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
        _entries.TryGetValue(opcode, out StubOpcodeEntry? entry)
            ? entry
            : throw new KeyNotFoundException($"Opcode 0x{opcode:X2} is not registered.");

    /// <summary>Próbuje pobrać wpis.</summary>
    /// <param name="opcode">Klucz opcode.</param>
    /// <param name="entry">Wpis lub <see langword="null"/>.</param>
    /// <returns>Czy wpis istnieje.</returns>
    public bool TryGet(byte opcode, out StubOpcodeEntry? entry) => _entries.TryGetValue(opcode, out entry);

    private void EnsureMutable()
    {
        if (_sealed)
        {
            throw new InvalidOperationException("Opcode table is sealed.");
        }
    }
}
