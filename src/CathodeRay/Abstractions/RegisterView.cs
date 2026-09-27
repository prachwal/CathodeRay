using System.Collections;

namespace CathodeRay.Abstractions;

/// <summary>Dedykowany widok rejestrów (read-model): uporządkowane wpisy z nazwą i szerokością, wyszukiwanie po nazwie i lista rejestrów 8-bit.
/// Snapshot bez żywych referencji do stanu CPU.</summary>
public sealed class RegisterView : IReadOnlyCollection<RegisterEntry>
{
    private readonly RegisterEntry[] _entries;
    private readonly Dictionary<string, RegisterEntry> _byName;

    /// <summary>Tworzy widok z wpisów (duplikat nazwy = wyjątek).</summary>
    /// <param name="entries">Wpisy rejestrów w kolejności prezentacji.</param>
    public RegisterView(IEnumerable<RegisterEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries.ToArray();
        _byName = new Dictionary<string, RegisterEntry>(_entries.Length, StringComparer.Ordinal);
        foreach (RegisterEntry entry in _entries)
        {
            if (!_byName.TryAdd(entry.Name, entry))
            {
                throw new ArgumentException($"Duplicate register '{entry.Name}'.", nameof(entries));
            }
        }

        Names = Array.ConvertAll(_entries, static e => e.Name);
        EightBitNames = Array.ConvertAll(Array.FindAll(_entries, static e => e.WidthBits == 8), static e => e.Name);
    }

    /// <summary>Pusty widok (np. gdy CPU nie wystawia rejestrów).</summary>
    public static RegisterView Empty { get; } = new(Array.Empty<RegisterEntry>());

    /// <inheritdoc/>
    public int Count => _entries.Length;

    /// <summary>Nazwy rejestrów w kolejności deklaracji.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>Nazwy rejestrów o szerokości 8 bitów (do formatowania jak 2 cyfry hex).</summary>
    public IReadOnlyCollection<string> EightBitNames { get; }

    /// <summary>Wpis rejestru po nazwie.</summary>
    /// <param name="name">Nazwa rejestru.</param>
    public RegisterEntry this[string name] => _byName[name];

    /// <summary>Próbuje pobrać wpis rejestru po nazwie.</summary>
    /// <param name="name">Nazwa rejestru.</param>
    /// <param name="entry">Znaleziony wpis.</param>
    /// <returns>Czy rejestr istnieje.</returns>
    public bool TryGet(string name, out RegisterEntry entry) => _byName.TryGetValue(name, out entry);

    /// <inheritdoc/>
    public IEnumerator<RegisterEntry> GetEnumerator() => ((IEnumerable<RegisterEntry>)_entries).GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
