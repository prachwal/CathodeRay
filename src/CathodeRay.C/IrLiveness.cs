namespace CathodeRay.C;

/// <summary>Żywość komórek w ciele funkcji: graf przepływu po instrukcjach (<see cref="Ir.Jmp"/> do etykiety,
/// <see cref="Ir.BrCmp"/> do etykiety i dalej, <see cref="Ir.Ret"/> kończy) i iteracja wstecz do punktu stałego
/// <c>live_out = ∪ live_in(następnik)</c>, więc skoki w przód i w tył, pętle i przepływ nieredukowalny obsługuje to samo równanie.
/// Jednostką jest symbol obiektu bez przesunięcia połówki (<c>x+2</c> to część <c>x</c>); użyciem jest każdy odczyt komórki i
/// wzięcie adresu, a zapis zabija tylko wtedy, gdy obejmuje cały obiekt (zapis częściowy nie zabija).</summary>
internal sealed class IrLiveness
{
    private readonly HashSet<string>[] _in;
    private readonly HashSet<string>[] _out;

    private IrLiveness(HashSet<string>[] liveIn, HashSet<string>[] liveOut)
    {
        _in = liveIn;
        _out = liveOut;
    }

    /// <summary>Liczy żywość dla ciała funkcji.</summary>
    /// <param name="body">Instrukcje funkcji.</param>
    /// <param name="cellSize">Rozmiar obiektu po symbolu bazowym (0, gdy nieznany: zapis nigdy go wtedy nie zabija).</param>
    /// <returns>Wynik analizy.</returns>
    public static IrLiveness Of(IReadOnlyList<Ir.Ins> body, Func<string, int> cellSize)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(cellSize);
        var labels = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < body.Count; i++)
        {
            if (body[i] is Ir.Label label)
            {
                labels[label.Name] = i;
            }
        }

        int Target(string name) => labels.TryGetValue(name, out int index) ? index : throw new InvalidOperationException($"unknown label '{name}'.");
        var successors = new int[body.Count][];
        var uses = new string[body.Count][];
        var kills = new string?[body.Count];
        for (int i = 0; i < body.Count; i++)
        {
            Ir.Ins ins = body[i];
            int[] next = (i + 1 < body.Count) ? [i + 1] : [];
            successors[i] = ins switch
            {
                Ir.Jmp jump => [Target(jump.Target)],
                Ir.BrCmp branch => [Target(branch.Target), .. next],
                Ir.Ret => [],
                _ => next,
            };
            uses[i] = [.. IrFacts.Uses(ins).Select(static op => op switch
            {
                Ir.Cell cell => BaseSymbol(cell.Sym),
                Ir.AddrOf address => BaseSymbol(address.Sym),
                _ => null,
            }).OfType<string>()];
            kills[i] = Killed(ins, cellSize);
        }

        HashSet<string>[] liveIn = [.. body.Select(static _ => new HashSet<string>(StringComparer.Ordinal))];
        HashSet<string>[] liveOut = [.. body.Select(static _ => new HashSet<string>(StringComparer.Ordinal))];
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = body.Count - 1; i >= 0; i--)
            {
                foreach (int successor in successors[i])
                {
                    liveOut[i].UnionWith(liveIn[successor]);
                }

                var fresh = new HashSet<string>(liveOut[i], StringComparer.Ordinal);
                if (kills[i] is { } killed)
                {
                    fresh.Remove(killed);
                }

                fresh.UnionWith(uses[i]);
                if (!fresh.SetEquals(liveIn[i]))
                {
                    liveIn[i] = fresh;
                    changed = true;
                }
            }
        }

        return new IrLiveness(liveIn, liveOut);
    }

    /// <summary>Symbol obiektu bez przesunięcia połówki (<c>x+4</c> to część obiektu <c>x</c>).</summary>
    /// <param name="symbol">Symbol komórki.</param>
    /// <returns>Symbol bazowy.</returns>
    public static string BaseSymbol(string symbol)
    {
        int plus = symbol.IndexOf('+', StringComparison.Ordinal);
        return (plus < 0) ? symbol : symbol[..plus];
    }

    /// <summary>Obiekt, którego całą wartość instrukcja nadpisuje (zabija).</summary>
    /// <param name="ins">Instrukcja.</param>
    /// <param name="cellSize">Rozmiar obiektu po symbolu bazowym.</param>
    /// <returns>Symbol bazowy albo <see langword="null"/>, gdy instrukcja nic nie zapisuje albo zapisuje tylko część obiektu.</returns>
    public static string? Killed(Ir.Ins ins, Func<string, int> cellSize)
    {
        ArgumentNullException.ThrowIfNull(cellSize);
        return (IrFacts.Def(ins) is { } def && !def.Sym.Contains('+', StringComparison.Ordinal) && def.W == cellSize(def.Sym)) ? def.Sym : null;
    }

    /// <summary>Obiekty żywe tuż przed instrukcją.</summary>
    /// <param name="index">Indeks instrukcji.</param>
    /// <returns>Symbole bazowe.</returns>
    public IReadOnlySet<string> LiveIn(int index) => _in[index];

    /// <summary>Obiekty żywe tuż po instrukcji.</summary>
    /// <param name="index">Indeks instrukcji.</param>
    /// <returns>Symbole bazowe.</returns>
    public IReadOnlySet<string> LiveOut(int index) => _out[index];
}
