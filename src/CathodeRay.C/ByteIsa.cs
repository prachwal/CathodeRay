using System.Text;

namespace CathodeRay.C;

/// <summary>Zestaw prymitywów procesora akumulatorowego, z których <see cref="ByteSelector"/> składa całą generację
/// kodu: wszystkie komórki są w pamięci, a każda operacja IR to ciąg „A ← bajt; A ← A op bajt; bajt ← A”. Klasa
/// pochodna zna tylko składnię i instrukcje swojego CPU (rejestr A, jeden rejestr adresowy dla wskaźników).</summary>
internal abstract class ByteIsa
{
    private readonly StringBuilder _out = new();

    /// <summary>Adres bajtu komórki (tekst jak z <see cref="Loc"/>) → rejestr z <see cref="CellRegisters"/>.</summary>
    private readonly Dictionary<string, string> _registers = new(StringComparer.Ordinal);

    /// <summary>Wołanie → pary rejestrów komórek żywych za nim, zapisywane na stosie wokół niego (klucz: instancja wołania).</summary>
    private readonly Dictionary<Ir.Call, IReadOnlyList<string>> _callSaves = new(ReferenceEqualityComparer.Instance);

    /// <summary>Rejestry komórek bieżącej funkcji (<see cref="BeginFunction"/>); <see langword="null"/>: wszystkie z mapy.</summary>
    private HashSet<string>? _active;

    private int _localLabels;

    /// <summary>Bajty słowa w pamięci od najstarszego (6800).</summary>
    public virtual bool BigEndian => false;

    /// <summary>Symbole wspólne wołania pośredniego (definiuje crt0), do zadeklarowania w module.</summary>
    public virtual IEnumerable<string> IndirectSymbols => ["__icall", "cc_fp"];

    /// <summary>Xor (i or/and) nie rusza przeniesienia (6502/6800): bias najstarszego bajtu może iść wprost między
    /// odejmowanie młodszych bajtów a sbc (na Z80/8080 xor gasi C, więc bias lewej strony idzie do komórki scratch).
    /// Domyślnie <see langword="false"/>.</summary>
    public virtual bool XorPreservesCarry => false;

    /// <summary>Ścieżka ABI v2 (argumenty i wyniki w rejestrach); ustawia cel przed emisją.</summary>
    public bool AbiV2 { get; set; }

    /// <summary>Rejestry 8-bitowe, które <see cref="RegisterAllocator"/> może dać komórkom 1-bajtowym (w kolejności preferencji);
    /// domyślnie brak (komórki tylko w pamięci).</summary>
    public virtual IReadOnlyList<string> CellRegisters => [];

    /// <summary>Pary rejestrów dla komórek 2-bajtowych, starszy rejestr pierwszy (np. <c>bc</c>: młodszy bajt w C); domyślnie brak.</summary>
    public virtual IReadOnlyList<string> CellPairs => [];

    /// <summary>Długość dotychczasowego tekstu (znacznik początku funkcji).</summary>
    public int Mark => _out.Length;

    /// <summary>Tekst dotychczas wyemitowanych instrukcji.</summary>
    public string Text => _out.ToString();

    /// <summary>Jawny kontrakt rejestrowy tego CPU.</summary>
    internal CpuModel Model => CpuModels.For(CpuName);

    /// <summary>Nazwy, których asembler nie przyjmie jako symbole użytkownika (bez rozróżniania wielkości liter).</summary>
    protected virtual IReadOnlySet<string> Reserved { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Nazwa CPU w <see cref="CpuModels"/> (jednoznaczna z klasą ISA).</summary>
    protected abstract string CpuName { get; }

    /// <summary>Skok, gdy słowo spełnia warunek ze znakiem względem zera — bez odejmowania (test bitu znaku
    /// i zera). Wołane tylko dla <c>Lt/Ge/Le/Gt</c> o szerokości 2 z jedną stroną zerową.</summary>
    /// <param name="value">Słowo 2-bajtowe (nie natychmiastowe).</param>
    /// <param name="cond">Warunek w postaci <c>wartość cond 0</c>.</param>
    /// <param name="target">Etykieta docelowa.</param>
    /// <returns><see langword="true"/>, gdy sekwencja została wyemitowana.</returns>
    public abstract bool TryBranchZeroSigned(Word value, Ir.Cond cond, string target);

    /// <summary>Lokalizacja argumentu: rejestr z <see cref="CpuModel.ArgRegs"/> (ABI v2)
    /// albo komórka <c>cc_argN</c> (ABI v1 — gdy model nie zna rejestrów).</summary>
    /// <param name="index">Numer argumentu od zera.</param>
    /// <param name="part">0 = młodszy bajt, 1 = starszy.</param>
    /// <param name="width">Szerokość argumentu (pary rejestrowe tylko dla W≤2).</param>
    /// <returns>Symbol komórki albo nazwa rejestru.</returns>
    public virtual string ArgCell(int index, int part, int width) =>
        Model.ArgRegs.Count > index
            ? Model.ArgRegs[index]
            : part == 0 ? $"cc_arg{index + 1}" : $"cc_arg{index + 1}_h";

    /// <summary>Komórka argumentu w pamięci, pozycyjnie (semantyka v1, bez względu na <see cref="CpuModel.ArgRegs"/>);
    /// selektor używa jej dla wołań helperów w ręcznym asemblerze na ścieżce v2 (tam rejestry nie obowiązują).</summary>
    /// <param name="index">Numer argumentu od zera.</param>
    /// <param name="part">0 = młodszy bajt, 1 = starszy.</param>
    /// <returns>Symbol komórki.</returns>
    public string MemArgCell(int index, int part) =>
        part == 0 ? $"cc_arg{index + 1}" : $"cc_arg{index + 1}_h";

    /// <summary>Adres bajtu komórki (indeks 0 = młodszy). Komórka 32-bitowa <c>cc_argN</c> (parametr <c>long</c> liścia po
    /// <see cref="ParamAlias"/>) ma starszą połowę w <c>cc_argN+1</c>.</summary>
    /// <param name="sym">Symbol komórki.</param>
    /// <param name="width">Szerokość komórki.</param>
    /// <param name="index">Numer bajtu od najmłodszego.</param>
    /// <returns>Wyrażenie adresu.</returns>
    public string Loc(string sym, int width, int index) =>
        width == 1 ? Sym(sym) : BigEndian ? At(sym, width - 1 - index)
        : (width == 4 && index >= 2 && ParamAlias.NextArg(sym) is { } high) ? At(high, index - 2) : At(sym, index);

    /// <summary>Nazwa symbolu w asemblerze: nazwy zastrzeżone CPU (rejestry, mnemoniki, operatory) dostają przedrostek.</summary>
    /// <param name="name">Nazwa z kodu pośredniego.</param>
    /// <returns>Nazwa bezpieczna dla asemblera.</returns>
    public string Sym(string name) => Reserved.Contains(name) ? "cc_r_" + name : name;

    /// <summary>Adres symbolu z przesunięciem.</summary>
    /// <param name="sym">Symbol.</param>
    /// <param name="offset">Przesunięcie w bajtach.</param>
    /// <returns>Wyrażenie adresu.</returns>
    public string At(string sym, int offset) =>
        offset == 0 ? Sym(sym) : $"{Sym(sym)}{(offset > 0 ? "+" : "-")}{Math.Abs(offset)}";

    /// <summary>Dyrektywa wyboru segmentu.</summary>
    /// <param name="name">Nazwa segmentu.</param>
    /// <returns>Linia asemblera.</returns>
    public abstract string Segment(string name);

    /// <summary>Dyrektywa symbolu globalnego.</summary>
    /// <param name="sym">Symbol.</param>
    /// <returns>Linia asemblera.</returns>
    public abstract string Global(string sym);

    /// <summary>Dyrektywa symbolu zewnętrznego.</summary>
    /// <param name="sym">Symbol.</param>
    /// <returns>Linia asemblera.</returns>
    public abstract string Extern(string sym);

    /// <summary>Dane bajtowe.</summary>
    /// <param name="values">Wartości.</param>
    /// <returns>Dyrektywa.</returns>
    public abstract string Bytes(IEnumerable<int> values);

    /// <summary>Słowo 16-bitowe (kolejność bajtów wg asemblera CPU).</summary>
    /// <param name="expression">Wyrażenie.</param>
    /// <returns>Dyrektywa.</returns>
    public abstract string Word(string expression);

    /// <summary>Zarezerwowane miejsce.</summary>
    /// <param name="size">Rozmiar.</param>
    /// <returns>Dyrektywa.</returns>
    public abstract string Reserve(int size);

    /// <summary>Linie na początku modułu (definicje pomocnicze CPU), np. adresy strony zerowej.</summary>
    /// <returns>Tekst albo pusty.</returns>
    public virtual string Preamble() => string.Empty;

    /// <summary>Składa kod startowy (definiuje komórki umówione, <c>__icall</c>, wywołuje <c>main</c>).</summary>
    /// <returns>Źródło crt0.</returns>
    public abstract string Crt0();

    /// <summary>A ← bajt.</summary>
    /// <param name="value">Stała albo adres.</param>
    public abstract void LoadA(Octet value);

    /// <summary>Bajt ← A (bez wpływu na przeniesienie).</summary>
    /// <param name="address">Adres.</param>
    public abstract void StoreA(string address);

    /// <summary>A ← A op bajt.</summary>
    /// <param name="op">Działanie.</param>
    /// <param name="value">Operand.</param>
    /// <param name="first">Pierwszy bajt ciągu (nie używa przeniesienia z poprzedniego).</param>
    public abstract void Alu(ByteAlu op, Octet value, bool first);

    /// <summary>Porównanie A z bajtem (ustawia Z i pożyczkę jak SUB, A bez zmian nie jest gwarantowane).</summary>
    /// <param name="value">Operand.</param>
    public abstract void Cmp(Octet value);

    /// <summary>Przesunięcie A w lewo; <paramref name="first"/> = wsuwa zero, inaczej przeniesienie.</summary>
    /// <param name="first">Pierwszy bajt.</param>
    public abstract void ShlA(bool first);

    /// <summary>Przesunięcie A w prawo; <paramref name="first"/> = wsuwa zero, inaczej przeniesienie.</summary>
    /// <param name="first">Pierwszy bajt.</param>
    public abstract void ShrA(bool first);

    /// <summary>Skok bezwarunkowy.</summary>
    /// <param name="label">Etykieta.</param>
    public abstract void Jump(string label);

    /// <summary>Skok warunkowy o dowolnym zasięgu.</summary>
    /// <param name="flag">Warunek.</param>
    /// <param name="label">Etykieta.</param>
    public abstract void JumpIf(ByteFlag flag, string label);

    /// <summary>Odłożenie A na stos.</summary>
    public abstract void PushA();

    /// <summary>Zdjęcie A ze stosu.</summary>
    public abstract void PopA();

    /// <summary>Wołanie podprogramu po symbolu.</summary>
    /// <param name="symbol">Symbol.</param>
    public abstract void Call(string symbol);

    /// <summary>Wołanie przez wskaźnik z komórki 2-bajtowej.</summary>
    /// <param name="cell">Symbol komórki z adresem.</param>
    public abstract void CallIndirect(string cell);

    /// <summary>Skok do funkcji w pozycji ogonowej (wynik już w miejscu docelowym).</summary>
    /// <param name="symbol">Symbol funkcji.</param>
    public virtual void TailCall(string symbol) => Jump(symbol);

    /// <summary>Powrót z podprogramu.</summary>
    public abstract void Return();

    /// <summary>Ustawia wskaźnik dostępu pośredniego na <c>[cell] + offset</c> (kolejne wywołania czytają/piszą bajty
    /// od najmłodszego).</summary>
    /// <param name="cell">Komórka 2-bajtowa z adresem.</param>
    /// <param name="offset">Stałe przesunięcie.</param>
    /// <param name="mustCopy">Wynik odczytu trafi do tej samej komórki co wskaźnik: adres trzeba zapamiętać poza nią.</param>
    public abstract void PtrSetup(string cell, int offset, bool mustCopy = false);

    /// <summary>A ← bajt pod wskaźnikiem (indeks liczony od ustawionego adresu; kolejne rosnąco).</summary>
    /// <param name="index">Numer bajtu.</param>
    public abstract void PtrLoad(int index);

    /// <summary>Bajt pod wskaźnikiem ← A.</summary>
    /// <param name="index">Numer bajtu.</param>
    public abstract void PtrStore(int index);

    /// <summary>Bajt adresu symbolu jako stała asemblera (relokacje Lo8/Hi8) albo <see langword="null"/>, gdy CPU nie ma
    /// takiej składni (wtedy adres leży w komórce danych).</summary>
    /// <param name="expression">Wyrażenie adresu (symbol ± stała).</param>
    /// <param name="index">0 = młodszy, 1 = starszy.</param>
    /// <returns>Tekst stałej albo null.</returns>
    public virtual string? AddressByte(string expression, int index) => null;

    /// <summary>Przypisuje komórkom rejestry; selektor dalej widzi nazwy symboliczne, a ISA tłumaczy operand przy emisji (jak strona
    /// zerowa w <see cref="Mos6502Isa"/>). Komórka 1-bajtowa dostaje rejestr z <see cref="CellRegisters"/>, 2-bajtowa parę z
    /// <see cref="CellPairs"/> (starszy rejestr pierwszy).</summary>
    /// <param name="cells">Symbol komórki z kodu pośredniego → rejestr albo para.</param>
    /// <param name="saves">Wołanie (instancja) → pary z <see cref="CellPairs"/> do zapisania wokół niego (<see cref="SavedAround"/>).</param>
    public void AssignRegisters(IReadOnlyDictionary<string, string> cells, IEnumerable<(Ir.Call Call, IReadOnlyList<string> Pairs)>? saves = null)
    {
        ArgumentNullException.ThrowIfNull(cells);
        foreach ((Ir.Call call, IReadOnlyList<string> pairs) in saves ?? [])
        {
            _callSaves[call] = pairs.All(CellPairs.Contains) ? pairs : throw new ArgumentException($"niedozwolone pary '{string.Join(",", pairs)}'", nameof(saves));
        }

        foreach ((string sym, string registers) in cells)
        {
            if (!(registers.Length == 1 ? CellRegisters : CellPairs).Contains(registers))
            {
                throw new ArgumentException($"niedozwolone rejestry '{registers}' dla {sym}", nameof(cells));
            }

            _registers[Sym(sym)] = registers[^1..];
            if (registers.Length == 2)
            {
                _registers[At(sym, 1)] = registers[..1];
            }
        }
    }

    /// <summary>Pary rejestrów komórek żywych za wołaniem: selektor odkłada je na stos przed <c>call</c> (<see cref="IPairStack.PushPair"/>, po
    /// argumentach) i zdejmuje w odwrotnej kolejności po nim (<see cref="IPairStack.PopPair"/>, przed zapisem wyniku).</summary>
    /// <param name="call">Wołanie (ta sama instancja co w module).</param>
    /// <returns>Pary albo pusta lista.</returns>
    public IReadOnlyList<string> SavedAround(Ir.Call call) => _callSaves.GetValueOrDefault(call) ?? [];

    /// <summary>Bajt komórki leży w rejestrze CPU (mapa <see cref="AssignRegisters"/>), nie w pamięci: nie trafia do sekcji danych.</summary>
    /// <param name="address">Adres bajtu (tekst jak z <see cref="Loc"/>).</param>
    /// <returns><see langword="true"/>, gdy bajt ma rejestr.</returns>
    public bool IsRegister(string address) => _registers.ContainsKey(address);

    /// <summary>Początek emisji funkcji: zbiera rejestry jej komórek. Parę pomocniczą wolną w tej funkcji prymitywy mogą niszczyć,
    /// bo rejestr komórki nie żyje przez wejście do funkcji ani przez wołanie bez zapisu.</summary>
    /// <param name="function">Funkcja.</param>
    public void BeginFunction(Ir.Function function)
    {
        ArgumentNullException.ThrowIfNull(function);
        _active = [.. function.Params.Concat(function.Body.SelectMany(IrFacts.Operands).OfType<Ir.Cell>())
            .SelectMany(c => Enumerable.Range(0, c.W).Select(i => Resolve(Loc(c.Sym, c.W, i))))
            .OfType<string>()];
    }

    /// <summary>Zdejmuje bajt ze stosu do pamięci z zachowaniem rejestru wyniku (epilog ramki przy
    /// wyniku w rejestrach; 6502 odkłada przez Y). Domyślnie przez A jak bez wyniku.</summary>
    /// <param name="address">Cel w pamięci.</param>
    /// <param name="keepResult">Rejestr wyniku niesie wynik i nie może się zmienić.</param>
    public virtual void PopByte(string address, bool keepResult)
    {
        _ = keepResult;
        PopA();
        StoreA(address);
    }

    /// <summary>Zwiększa albo zmniejsza o 1 liczbę zapisaną w kolejnych bajtach pamięci (od najmłodszego) jedną, krótką sekwencją
    /// CPU (np. <c>INC</c> pamięci z pominięciem starszego bajtu, gdy nie ma przeniesienia). Nie musi zachować
    /// flag; może zmienić A i flagi. Wywoływane tylko, gdy liczba ma 1 lub 2 bajty.</summary>
    /// <param name="bytes">Adresy bajtów od najmłodszego do najstarszego.</param>
    /// <param name="increment"><see langword="true"/>: +1, <see langword="false"/>: -1.</param>
    /// <returns><see langword="true"/>, gdy sekwencja została wyemitowana; inaczej selektor użyje ogólnego łańcucha ADD/SUB.</returns>
    public abstract bool TryStep(IReadOnlyList<string> bytes, bool increment);

    /// <summary>Zamienia tekst od znacznika na jego wersję po relaksacji skoków (krótkie skoki warunkowe tam, gdzie cel jest w zasięgu).</summary>
    /// <param name="mark">Znacznik z <see cref="Mark"/>.</param>
    public void RelaxFrom(int mark)
    {
        string text = _out.ToString(mark, _out.Length - mark);
        string relaxed = Relax(text);
        if (!ReferenceEquals(text, relaxed))
        {
            _out.Length = mark;
            _out.Append(relaxed);
        }
    }

    /// <summary>Kolejna unikalna etykieta lokalna instrukcji (dla krótkich skoków wewnątrz sekwencji).</summary>
    /// <returns>Nazwa etykiety.</returns>
    public string LocalLabel() => $"__i{++_localLabels}";

    /// <summary>Dopisuje linię surowego tekstu (etykieta, komentarz).</summary>
    /// <param name="line">Linia.</param>
    public void Raw(string line) => L(line);

    /// <summary>Starszy bajt leży tuż za młodszym: <c>x</c>/<c>x+1</c>, <c>x+2</c>/<c>x+3</c> albo para komórek crt0 <c>cc_x</c>/<c>cc_x_h</c>.</summary>
    /// <param name="word">Słowo w pamięci.</param>
    /// <returns><see langword="true"/>, gdy bajty są sąsiednie.</returns>
    protected static bool Adjacent(Word word)
    {
        if (word.Hi == word.Lo + "+1" || word.Hi == word.Lo + "_h")
        {
            return true;
        }

        int plus = word.Lo.LastIndexOf('+');
        return plus > 0 && int.TryParse(word.Lo.AsSpan(plus + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int offset)
            && word.Hi == $"{word.Lo[..plus]}+{offset + 1}";
    }

    /// <summary>Rejestr przypisany bajtowi komórki albo <see langword="null"/> (pamięć).</summary>
    /// <param name="address">Adres bajtu.</param>
    /// <returns>Rejestr albo null.</returns>
    protected string? Resolve(string address) => _registers.GetValueOrDefault(address);

    /// <summary>Oba bajty słowa w rejestrach.</summary>
    /// <param name="word">Słowo.</param>
    /// <returns><see langword="true"/>, gdy oba bajty mają rejestry.</returns>
    protected bool InRegisters(Word word) => !word.IsImmediate && IsRegister(word.Lo) && IsRegister(word.Hi);

    /// <summary>Słowo, które przeniesie para rejestrów: stała, oba bajty w rejestrach albo oba w pamięci obok siebie.</summary>
    /// <param name="word">Słowo.</param>
    /// <returns><see langword="true"/>, gdy słowo da się przenieść parą.</returns>
    protected bool Usable(Word word) => word.IsImmediate || InRegisters(word) || (!IsRegister(word.Lo) && !IsRegister(word.Hi) && Adjacent(word));

    /// <summary>Oba bajty słowa w pamięci, obok siebie (bez rejestrów).</summary>
    /// <param name="word">Słowo.</param>
    /// <returns><see langword="true"/>, gdy słowo da się przenieść jednym <c>ld hl,(n)</c>/<c>lhld n</c>.</returns>
    protected bool InMemoryWord(Word word) => !word.IsImmediate && !IsRegister(word.Lo) && !IsRegister(word.Hi) && Adjacent(word);

    /// <summary>Para z <see cref="CellPairs"/>, gdy słowo leży w niej w całości (młodszy bajt w młodszym rejestrze).</summary>
    /// <param name="word">Słowo.</param>
    /// <returns>Para (np. <c>bc</c>) albo null.</returns>
    protected string? PairOf(Word word) =>
        InRegisters(word) && CellPairs.Contains(_registers[word.Hi] + _registers[word.Lo]) ? _registers[word.Hi] + _registers[word.Lo] : null;

    /// <summary>Para pomocnicza bez rejestrów komórek bieżącej funkcji, od ostatniej z <see cref="CellPairs"/> (DE, potem BC);
    /// <see langword="null"/>, gdy wszystkie zajęte.</summary>
    /// <returns>Para albo null.</returns>
    protected string? Scratch() => CellPairs.Reverse().FirstOrDefault(p => !Taken(p[..1]) && !Taken(p[1..]));

    /// <summary>Relaksacja skoków w tekście jednej funkcji; domyślnie bez zmian (CPU z absolutnymi skokami warunkowymi).</summary>
    /// <param name="text">Tekst funkcji.</param>
    /// <returns>Ten sam obiekt, gdy nic się nie zmieniło.</returns>
    protected virtual string Relax(string text) => text;

    /// <summary>Dopisuje linię kodu.</summary>
    /// <param name="line">Linia.</param>
    protected void L(string line) => _out.AppendLine(line);

    /// <summary>Rejestr należy do komórki bieżącej funkcji (bez <see cref="BeginFunction"/>: do którejkolwiek komórki).</summary>
    private bool Taken(string register) =>
        _active is { } active
            ? active.Contains(register) || Model.AliasesOf(register).Any(active.Contains)
            : _registers.ContainsValue(register) || Model.AliasesOf(register).Any(_registers.ContainsValue);
}
