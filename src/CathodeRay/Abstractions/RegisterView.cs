using System.Collections;

namespace CathodeRay.Abstractions;

/// <summary>Widok rejestrów (read-model): współdzielony <see cref="RegisterLayout"/> + wartości z chwili snapshotu.
/// Snapshot kosztuje jedną tablicę wartości; opis rejestrów nie jest kopiowany.</summary>
public sealed class RegisterView : IReadOnlyCollection<RegisterEntry>
{
    private readonly RegisterLayout _layout;
    private readonly ulong[] _values;

    /// <summary>Tworzy widok; przejmuje tablicę wartości na własność (nie modyfikuj jej po przekazaniu).</summary>
    /// <param name="layout">Układ rejestrów.</param>
    /// <param name="values">Wartości w kolejności układu.</param>
    public RegisterView(RegisterLayout layout, ulong[] values)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length != layout.Count)
        {
            throw new ArgumentException($"Expected {layout.Count} values, got {values.Length}.", nameof(values));
        }

        _layout = layout;
        _values = values;
    }

    /// <summary>Pusty widok (np. gdy CPU nie wystawia rejestrów).</summary>
    public static RegisterView Empty { get; } = new(RegisterLayout.Empty, []);

    /// <inheritdoc/>
    public int Count => _values.Length;

    /// <summary>Nazwy rejestrów w kolejności deklaracji.</summary>
    public IReadOnlyList<string> Names => _layout.Names;

    /// <summary>Nazwy rejestrów o szerokości 8 bitów (do formatowania jak 2 cyfry hex).</summary>
    public IReadOnlyCollection<string> EightBitNames => _layout.EightBitNames;

    /// <summary>Wpis rejestru po nazwie.</summary>
    /// <param name="name">Nazwa rejestru.</param>
    public RegisterEntry this[string name] =>
        TryGet(name, out RegisterEntry entry) ? entry : throw new KeyNotFoundException($"Register '{name}' not found.");

    /// <summary>Próbuje pobrać wpis rejestru po nazwie.</summary>
    /// <param name="name">Nazwa rejestru.</param>
    /// <param name="entry">Znaleziony wpis.</param>
    /// <returns>Czy rejestr istnieje.</returns>
    public bool TryGet(string name, out RegisterEntry entry)
    {
        if (_layout.TryGetIndex(name, out int index))
        {
            entry = At(index);
            return true;
        }

        entry = default;
        return false;
    }

    /// <inheritdoc/>
    public IEnumerator<RegisterEntry> GetEnumerator()
    {
        for (int i = 0; i < _values.Length; i++)
        {
            yield return At(i);
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private RegisterEntry At(int index) => new(_layout.Definitions[index], _values[index]);
}
