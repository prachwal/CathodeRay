using System.Text;

namespace CathodeRay.C;

/// <summary>Zestaw prymitywów procesora akumulatorowego, z których <see cref="ByteSelector"/> składa całą generację
/// kodu: wszystkie komórki są w pamięci, a każda operacja IR to ciąg „A ← bajt; A ← A op bajt; bajt ← A”. Klasa
/// pochodna zna tylko składnię i instrukcje swojego CPU (rejestr A, jeden rejestr adresowy dla wskaźników).</summary>
internal abstract class ByteIsa
{
    private readonly StringBuilder _out = new();

    /// <summary>Bajty słowa w pamięci od najstarszego (6800).</summary>
    public virtual bool BigEndian => false;

    /// <summary>Symbole wspólne wołania pośredniego (definiuje crt0), do zadeklarowania w module.</summary>
    public virtual IEnumerable<string> IndirectSymbols => ["__icall", "cc_fp"];

    /// <summary>Tekst dotychczas wyemitowanych instrukcji.</summary>
    public string Text => _out.ToString();

    /// <summary>Nazwy, których asembler nie przyjmie jako symbole użytkownika (bez rozróżniania wielkości liter).</summary>
    protected virtual IReadOnlySet<string> Reserved { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Adres bajtu komórki (indeks 0 = młodszy).</summary>
    /// <param name="sym">Symbol komórki.</param>
    /// <param name="width">Szerokość komórki.</param>
    /// <param name="index">Numer bajtu od najmłodszego.</param>
    /// <returns>Wyrażenie adresu.</returns>
    public string Loc(string sym, int width, int index) =>
        width == 1 ? Sym(sym) : BigEndian ? At(sym, width - 1 - index) : At(sym, index);

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

    /// <summary>Powrót z podprogramu.</summary>
    public abstract void Return();

    /// <summary>Ustawia wskaźnik dostępu pośredniego na <c>[cell] + offset</c> (kolejne wywołania czytają/piszą bajty
    /// od najmłodszego).</summary>
    /// <param name="cell">Komórka 2-bajtowa z adresem.</param>
    /// <param name="offset">Stałe przesunięcie.</param>
    public abstract void PtrSetup(string cell, int offset);

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

    /// <summary>Dopisuje linię surowego tekstu (etykieta, komentarz).</summary>
    /// <param name="line">Linia.</param>
    public void Raw(string line) => L(line);

    /// <summary>Dopisuje linię kodu.</summary>
    /// <param name="line">Linia.</param>
    protected void L(string line) => _out.AppendLine(line);
}
