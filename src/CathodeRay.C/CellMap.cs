namespace CathodeRay.C;

/// <summary>Rozmieszczenie komórek jednej emisji: przypisanie rejestrów (<see cref="RegisterAllocator"/>), pary rejestrów
/// żywe za wołaniem i zapytania o to, gdzie leży bajt/słowo komórki. Cała wiedza „rejestr czy pamięć" żyje tutaj;
/// <see cref="ByteIsa"/> tylko emituje składnię swojego CPU.</summary>
internal sealed class CellMap
{
    private readonly ByteIsa _isa;
    private readonly Dictionary<string, string> _registers = new(StringComparer.Ordinal);
    private readonly Dictionary<Ir.Call, IReadOnlyList<string>> _callSaves = new(ReferenceEqualityComparer.Instance);
    private HashSet<string>? _active;

    /// <summary>Tworzy mapę dla CPU.</summary>
    /// <param name="isa">ISA (źródło nazw symboli i list rejestrów komórek).</param>
    public CellMap(ByteIsa isa) => _isa = isa;

    /// <summary>Starszy bajt leży tuż za młodszym: <c>x</c>/<c>x+1</c>, <c>x+2</c>/<c>x+3</c> albo para komórek crt0 <c>cc_x</c>/<c>cc_x_h</c>.</summary>
    /// <param name="word">Słowo w pamięci.</param>
    /// <returns><see langword="true"/>, gdy bajty są sąsiednie.</returns>
    public static bool Adjacent(Word word)
    {
        if (word.Hi == word.Lo + "+1" || word.Hi == word.Lo + "_h")
        {
            return true;
        }

        int plus = word.Lo.LastIndexOf('+');
        return plus > 0 && int.TryParse(word.Lo.AsSpan(plus + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int offset)
            && word.Hi == $"{word.Lo[..plus]}+{offset + 1}";
    }

    /// <summary>Przypisuje komórkom rejestry; selektor dalej widzi nazwy symboliczne, a ISA tłumaczy operand przy emisji (jak strona
    /// zerowa w <see cref="Mos6502Isa"/>). Komórka 1-bajtowa dostaje rejestr z <see cref="ByteIsa.CellRegisters"/>, 2-bajtowa parę z
    /// <see cref="ByteIsa.CellPairs"/> (starszy rejestr pierwszy).</summary>
    /// <param name="cells">Symbol komórki z kodu pośredniego → rejestr albo para.</param>
    /// <param name="saves">Wołanie (instancja) → pary z <see cref="ByteIsa.CellPairs"/> do zapisania wokół niego (<see cref="SavedAround"/>).</param>
    public void AssignRegisters(IReadOnlyDictionary<string, string> cells, IEnumerable<(Ir.Call Call, IReadOnlyList<string> Pairs)>? saves = null)
    {
        ArgumentNullException.ThrowIfNull(cells);
        foreach ((Ir.Call call, IReadOnlyList<string> pairs) in saves ?? [])
        {
            _callSaves[call] = pairs.All(_isa.CellPairs.Contains) ? pairs : throw new ArgumentException($"niedozwolone pary '{string.Join(",", pairs)}'", nameof(saves));
        }

        foreach ((string sym, string registers) in cells)
        {
            if (!(registers.Length == 1 ? _isa.CellRegisters : _isa.CellPairs).Contains(registers))
            {
                throw new ArgumentException($"niedozwolone rejestry '{registers}' dla {sym}", nameof(cells));
            }

            _registers[_isa.Sym(sym)] = registers[^1..];
            if (registers.Length == 2)
            {
                _registers[_isa.At(sym, 1)] = registers[..1];
            }
        }
    }

    /// <summary>Pary rejestrów komórek żywych za wołaniem: selektor odkłada je na stos przed <c>call</c> (<see cref="IPairStack.PushPair"/>, po
    /// argumentach) i zdejmuje w odwrotnej kolejności po nim (<see cref="IPairStack.PopPair"/>, przed zapisem wyniku).</summary>
    /// <param name="call">Wołanie (ta sama instancja co w module).</param>
    /// <returns>Pary albo pusta lista.</returns>
    public IReadOnlyList<string> SavedAround(Ir.Call call) => _callSaves.GetValueOrDefault(call) ?? [];

    /// <summary>Bajt komórki leży w rejestrze CPU (mapa <see cref="AssignRegisters"/>), nie w pamięci: nie trafia do sekcji danych.</summary>
    /// <param name="address">Adres bajtu (tekst jak z <see cref="ByteIsa.Loc"/>).</param>
    /// <returns><see langword="true"/>, gdy bajt ma rejestr.</returns>
    public bool IsRegister(string address) => _registers.ContainsKey(address);

    /// <summary>Początek emisji funkcji: zbiera rejestry jej komórek. Parę pomocniczą wolną w tej funkcji prymitywy mogą niszczyć,
    /// bo rejestr komórki nie żyje przez wejście do funkcji ani przez wołanie bez zapisu.</summary>
    /// <param name="function">Funkcja.</param>
    public void BeginFunction(Ir.Function function)
    {
        ArgumentNullException.ThrowIfNull(function);
        _active = [.. function.Params.Concat(function.Body.SelectMany(IrFacts.Operands).OfType<Ir.Cell>())
            .SelectMany(c => Enumerable.Range(0, c.W).Select(i => Resolve(_isa.Loc(c.Sym, c.W, i))))
            .OfType<string>()];
    }

    /// <summary>Rejestr przypisany bajtowi komórki albo <see langword="null"/> (pamięć).</summary>
    /// <param name="address">Adres bajtu.</param>
    /// <returns>Rejestr albo null.</returns>
    public string? Resolve(string address) => _registers.GetValueOrDefault(address);

    /// <summary>Oba bajty słowa w rejestrach.</summary>
    /// <param name="word">Słowo.</param>
    /// <returns><see langword="true"/>, gdy oba bajty mają rejestry.</returns>
    public bool InRegisters(Word word) => !word.IsImmediate && IsRegister(word.Lo) && IsRegister(word.Hi);

    /// <summary>Słowo, które przeniesie para rejestrów: stała, oba bajty w rejestrach albo oba w pamięci obok siebie.</summary>
    /// <param name="word">Słowo.</param>
    /// <returns><see langword="true"/>, gdy słowo da się przenieść parą.</returns>
    public bool Usable(Word word) => word.IsImmediate || InRegisters(word) || (!IsRegister(word.Lo) && !IsRegister(word.Hi) && Adjacent(word));

    /// <summary>Oba bajty słowa w pamięci, obok siebie (bez rejestrów).</summary>
    /// <param name="word">Słowo.</param>
    /// <returns><see langword="true"/>, gdy słowo da się przenieść jednym <c>ld hl,(n)</c>/<c>lhld n</c>.</returns>
    public bool InMemoryWord(Word word) => !word.IsImmediate && !IsRegister(word.Lo) && !IsRegister(word.Hi) && Adjacent(word);

    /// <summary>Para z <see cref="ByteIsa.CellPairs"/>, gdy słowo leży w niej w całości (młodszy bajt w młodszym rejestrze).</summary>
    /// <param name="word">Słowo.</param>
    /// <returns>Para (np. <c>bc</c>) albo null.</returns>
    public string? PairOf(Word word) =>
        InRegisters(word) && _isa.CellPairs.Contains(_registers[word.Hi] + _registers[word.Lo]) ? _registers[word.Hi] + _registers[word.Lo] : null;

    /// <summary>Para pomocnicza bez rejestrów komórek bieżącej funkcji, od ostatniej z <see cref="ByteIsa.CellPairs"/> (DE, potem BC);
    /// <see langword="null"/>, gdy wszystkie zajęte.</summary>
    /// <returns>Para albo null.</returns>
    public string? Scratch() => _isa.CellPairs.Reverse().FirstOrDefault(p => !Taken(p[..1]) && !Taken(p[1..]));

    /// <summary>Rejestr należy do komórki bieżącej funkcji (bez <see cref="BeginFunction"/>: do którejkolwiek komórki).</summary>
    private bool Taken(string register) =>
        _active is { } active
            ? active.Contains(register) || _isa.Model.AliasesOf(register).Any(active.Contains)
            : _registers.ContainsValue(register) || _isa.Model.AliasesOf(register).Any(_registers.ContainsValue);
}
