namespace CathodeRay.Abstractions;

/// <summary>Niezmienny układ rejestrów CPU (definicje w kolejności prezentacji + indeks po nazwie).
/// Tworzony raz na typ CPU i współdzielony przez wszystkie <see cref="RegisterView"/>.</summary>
public sealed class RegisterLayout
{
    private readonly RegisterDefinition[] _definitions;
    private readonly Dictionary<string, int> _indexByName;

    /// <summary>Tworzy układ (duplikat nazwy = wyjątek).</summary>
    /// <param name="definitions">Definicje w kolejności prezentacji.</param>
    public RegisterLayout(params RegisterDefinition[] definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        _definitions = (RegisterDefinition[])definitions.Clone();
        _indexByName = new Dictionary<string, int>(_definitions.Length, StringComparer.Ordinal);
        for (int i = 0; i < _definitions.Length; i++)
        {
            if (!_indexByName.TryAdd(_definitions[i].Name, i))
            {
                throw new ArgumentException($"Duplicate register '{_definitions[i].Name}'.", nameof(definitions));
            }
        }

        Names = Array.ConvertAll(_definitions, static d => d.Name);
        EightBitNames = Array.ConvertAll(Array.FindAll(_definitions, static d => d.IsEightBit), static d => d.Name);
    }

    /// <summary>Pusty układ.</summary>
    public static RegisterLayout Empty { get; } = new();

    /// <summary>Liczba rejestrów.</summary>
    public int Count => _definitions.Length;

    /// <summary>Definicje w kolejności prezentacji.</summary>
    public IReadOnlyList<RegisterDefinition> Definitions => _definitions;

    /// <summary>Nazwy rejestrów w kolejności deklaracji.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>Nazwy rejestrów 8-bit.</summary>
    public IReadOnlyCollection<string> EightBitNames { get; }

    /// <summary>Próbuje znaleźć indeks rejestru po nazwie.</summary>
    /// <param name="name">Nazwa rejestru.</param>
    /// <param name="index">Indeks w układzie.</param>
    /// <returns>Czy rejestr istnieje.</returns>
    public bool TryGetIndex(string name, out int index) => _indexByName.TryGetValue(name, out index);
}
