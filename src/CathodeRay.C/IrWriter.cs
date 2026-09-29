namespace CathodeRay.C;

/// <summary>Zbiera instrukcje generatora w kolejności emisji (zamiast <c>StringBuilder</c>); funkcje mogą być
/// grupowane przez <see cref="BeginFunction"/> / <see cref="EndFunction"/>.</summary>
internal sealed class IrWriter
{
    private readonly List<IrItem> _items = [];
    private List<Ins>? _function;
    private string _functionName = string.Empty;
    private bool _functionStatic;

    /// <summary>Elementy w kolejności emisji.</summary>
    public IReadOnlyList<IrItem> Items => _items;

    /// <summary>Dopisuje linię (z końcem linii).</summary>
    /// <param name="text">Tekst linii.</param>
    public void AppendLine(string text) => Add(new Raw(text + Environment.NewLine));

    /// <summary>Dopisuje fragment bez końca linii.</summary>
    /// <param name="text">Tekst.</param>
    /// <returns>Ten sam obiekt (łańcuchowanie jak w <c>StringBuilder</c>).</returns>
    public IrWriter Append(string text)
    {
        Add(new Raw(text));
        return this;
    }

    /// <summary>Dopisuje wszystkie instrukcje innego zapisu (np. ciało zbudowane osobno).</summary>
    /// <param name="other">Zapis źródłowy.</param>
    public void AppendAll(IrWriter other)
    {
        foreach (IrItem item in other._items)
        {
            if (item is Ins ins)
            {
                Add(ins);
            }
            else
            {
                _items.Add(item);
            }
        }
    }

    /// <summary>Zaczyna grupę funkcji.</summary>
    /// <param name="name">Nazwa.</param>
    /// <param name="isStatic">Funkcja <c>static</c>.</param>
    public void BeginFunction(string name, bool isStatic)
    {
        _function = [];
        _functionName = name;
        _functionStatic = isStatic;
    }

    /// <summary>Kończy grupę funkcji.</summary>
    public void EndFunction()
    {
        if (_function is not null)
        {
            _items.Add(new IrFunction(_functionName, _functionStatic, _function));
            _function = null;
        }
    }

    /// <summary>Instrukcje bez grupowania (dla segmentów danych).</summary>
    /// <returns>Lista instrukcji.</returns>
    public IReadOnlyList<Ins> ToInstructions() => [.. _items.OfType<Ins>()];

    private void Add(Ins ins)
    {
        if (_function is not null)
        {
            _function.Add(ins);
        }
        else
        {
            _items.Add(ins);
        }
    }
}
