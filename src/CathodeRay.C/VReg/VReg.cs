namespace CathodeRay.C;

/// <summary>Wirtualne rejestry mini-C: reprezentacja pośrednia między AST a komórkami absolutnymi
/// (<see cref="Ir"/>). Każda wartość skalarna dostaje własny rejestr (<see cref="Reg"/>) o stabilnej tożsamości,
/// bloki i krawędzie grafu przepływu są jawne (gotowość pod SSA), a pamięć widzą tylko <c>load/store</c>.
/// Agregaty (tablice, struktury) i komórki o wziętym adresie nie są rejestrami: przechodzą jako
/// <see cref="Pinned"/> albo adresy <see cref="Addr"/>. Projekt: <c>docs/vreg-design.md</c>.</summary>
public static class VReg
{
    /// <summary>Operand instrukcji.</summary>
    public abstract record Op;

    /// <summary>Wirtualny rejestr funkcji (lokalny, niematerialny do alokacji).</summary>
    /// <param name="Id">Numer rejestru w funkcji.</param>
    /// <param name="W">Szerokość w bajtach (1, 2, 4 albo 8).</param>
    public sealed record Reg(int Id, int W) : Op;

    /// <summary>Stała.</summary>
    /// <param name="Value">Wartość (młodsze <paramref name="W"/> bajtów; dla szerokości 8 młodsze 32 bity).</param>
    /// <param name="W">Szerokość w bajtach.</param>
    /// <param name="High">Starsze 32 bity stałej 8-bajtowej (dla pozostałych szerokości 0).</param>
    public sealed record Imm(int Value, int W, int High = 0) : Op;

    /// <summary>Adres symbolu z przesunięciem (wartość 16-bitowa).</summary>
    /// <param name="Sym">Symbol (zmienna, funkcja, napis).</param>
    /// <param name="Off">Przesunięcie w bajtach.</param>
    public sealed record Addr(string Sym, int Off) : Op;

    /// <summary>Komórka nieprzenoszona do rejestru (fragment agregatu albo symbol nielokalny):
    /// przechodzi przez potok VReg bez zmiany, z powrotem jako ta sama komórka <see cref="Ir"/>.</summary>
    /// <param name="Sym">Symbol komórki (z ewentualnym przyrostkiem połówki, np. <c>x+2</c>).</param>
    /// <param name="W">Szerokość w bajtach.</param>
    public sealed record Pinned(string Sym, int W) : Op;

    /// <summary>Instrukcja.</summary>
    public abstract record Ins;

    /// <summary>Kopia z konwersją szerokości (rozszerzenie zerem albo obcięcie).</summary>
    /// <param name="Dst">Cel.</param>
    /// <param name="Src">Źródło.</param>
    public sealed record Mov(Reg Dst, Op Src) : Ins;

    /// <summary>Działanie dwuargumentowe; szerokość wyniku wyznacza <paramref name="Dst"/>.</summary>
    /// <param name="Kind">Operator.</param>
    /// <param name="Dst">Wynik.</param>
    /// <param name="A">Lewy operand.</param>
    /// <param name="B">Prawy operand (dla przesunięć: liczba pozycji).</param>
    public sealed record Bin(Ir.BinOp Kind, Reg Dst, Op A, Op B) : Ins;

    /// <summary>Działanie jednoargumentowe.</summary>
    /// <param name="Kind">Operator.</param>
    /// <param name="Dst">Wynik.</param>
    /// <param name="A">Operand.</param>
    public sealed record Un(Ir.UnOp Kind, Reg Dst, Op A) : Ins;

    /// <summary>Odczyt spod adresu <c>Ptr + Off</c> (rozszerzony zerem do szerokości <paramref name="Dst"/>).</summary>
    /// <param name="Dst">Cel.</param>
    /// <param name="Ptr">Adres bazowy: rejestr 16-bitowy albo <see cref="Addr"/>.</param>
    /// <param name="Off">Stałe przesunięcie.</param>
    /// <param name="Bytes">Liczba czytanych bajtów (1, 2, 4 albo 8).</param>
    /// <param name="Volatile">Odczyt obiektu <c>volatile</c>: nie wolno go usunąć.</param>
    public sealed record Load(Reg Dst, Op Ptr, int Off, int Bytes, bool Volatile = false) : Ins;

    /// <summary>Zapis pod adres <c>Ptr + Off</c> (młodsze <paramref name="Bytes"/> bajtów wartości).</summary>
    /// <param name="Ptr">Adres bazowy.</param>
    /// <param name="Off">Stałe przesunięcie.</param>
    /// <param name="Value">Wartość.</param>
    /// <param name="Bytes">Liczba zapisywanych bajtów.</param>
    /// <param name="Volatile">Zapis obiektu <c>volatile</c>.</param>
    public sealed record Store(Op Ptr, int Off, Op Value, int Bytes, bool Volatile = false) : Ins;

    /// <summary>Kopiowanie bloku pamięci (rozłączne obszary).</summary>
    /// <param name="Dst">Adres celu.</param>
    /// <param name="Src">Adres źródła.</param>
    /// <param name="Size">Liczba bajtów.</param>
    public sealed record CopyBlock(Op Dst, Op Src, int Size) : Ins;

    /// <summary>Wypełnienie bloku pamięci stałą.</summary>
    /// <param name="Dst">Adres początku.</param>
    /// <param name="Value">Bajt wypełnienia.</param>
    /// <param name="Size">Liczba bajtów.</param>
    public sealed record Fill(Op Dst, int Value, int Size) : Ins;

    /// <summary>Porównanie, wynik 0/1 w <paramref name="Dst"/> (szerokość 1); zużywane przez <see cref="Br"/>.</summary>
    /// <param name="C">Warunek.</param>
    /// <param name="Dst">Wynik (0 albo 1).</param>
    /// <param name="A">Lewy operand.</param>
    /// <param name="B">Prawy operand.</param>
    public sealed record Cmp(Ir.Cond C, Reg Dst, Op A, Op B) : Ins;

    /// <summary>Skok warunkowy na wyniku <see cref="Cmp"/> (niezerowy → <paramref name="Then"/>).</summary>
    /// <param name="C">Wynik porównania.</param>
    /// <param name="Then">Etykieta dla wartości niezerowej.</param>
    /// <param name="Else">Etykieta dla zera (spadek, gdy równa następnemu blokowi).</param>
    public sealed record Br(Reg C, string Then, string Else) : Ins;

    /// <summary>Skok bezwarunkowy.</summary>
    /// <param name="Target">Etykieta docelowa.</param>
    public sealed record Jmp(string Target) : Ins;

    /// <summary>Wywołanie funkcji po nazwie albo przez rejestr.</summary>
    /// <param name="Direct">Nazwa funkcji (albo <see langword="null"/> dla wołania pośredniego).</param>
    /// <param name="Indirect">Rejestr z adresem funkcji (wołanie pośrednie).</param>
    /// <param name="Args">Argumenty.</param>
    /// <param name="ParamWidths">Szerokości parametrów.</param>
    /// <param name="Result">Rejestr wyniku (null dla void); jego szerokość to szerokość wyniku.</param>
    public sealed record Call(string? Direct, Reg? Indirect, IReadOnlyList<Op> Args, IReadOnlyList<int> ParamWidths, Reg? Result) : Ins;

    /// <summary>Powrót z funkcji.</summary>
    /// <param name="Value">Zwracana wartość albo null.</param>
    /// <param name="W">Szerokość wyniku funkcji (0 dla void).</param>
    public sealed record Ret(Op? Value, int W) : Ins;

    /// <summary>Znacznik linii źródła C (mapa debugowa).</summary>
    /// <param name="File">Plik C albo null.</param>
    /// <param name="Line">Linia.</param>
    public sealed record Src(string? File, int Line) : Ins;

    /// <summary>Blok podstawowy: etykieta i instrukcje do (włącznie) skoku, rozgałęzienia albo powrotu.</summary>
    /// <param name="Label">Etykieta bloku.</param>
    /// <param name="Explicit">Etykieta istniała w źródle IR (fałsz tylko dla syntezowanego wejścia funkcji).</param>
    /// <param name="Code">Instrukcje bloku (bez etykiety).</param>
    public sealed record Block(string Label, bool Explicit, IReadOnlyList<Ins> Code);

    /// <summary>Funkcja w rejestrach wirtualnych.</summary>
    /// <param name="Name">Nazwa.</param>
    /// <param name="IsStatic">Symbol lokalny modułu.</param>
    /// <param name="Params">Rejestry parametrów w kolejności deklaracji.</param>
    /// <param name="Sym">Rejestr → pierwotny symbol komórki (do odtworzenia <see cref="Ir"/>; rejestr bez wpisu jest syntezowany).</param>
    /// <param name="Aggregates">Agregaty funkcji (pamięć, nigdy rejestry).</param>
    /// <param name="RetW">Szerokość wyniku (0 dla void).</param>
    /// <param name="Blocks">Bloki w kolejności źródła.</param>
    public sealed record Function(string Name, bool IsStatic, IReadOnlyList<Reg> Params, IReadOnlyDictionary<int, string> Sym, IReadOnlyList<Ir.Owned> Aggregates, int RetW, IReadOnlyList<Block> Blocks);

    /// <summary>Moduł: funkcje w rejestrach, dane i deklaracje zewnętrzne jak w <see cref="Ir.Module"/>.</summary>
    /// <param name="Functions">Funkcje w kolejności źródła.</param>
    /// <param name="Data">Obiekty danych (także napisy, tablica INIT).</param>
    /// <param name="ExternFunctions">Funkcje zdefiniowane w innych modułach.</param>
    /// <param name="ExternCells">Zmienne <c>extern</c>.</param>
    /// <param name="ObjectMode">Tryb obiektowy (linker).</param>
    /// <param name="Volatile">Symbole obiektów <c>volatile</c>.</param>
    public sealed record Module(
        IReadOnlyList<Function> Functions,
        IReadOnlyList<Ir.Data> Data,
        IReadOnlyList<string> ExternFunctions,
        IReadOnlyList<string> ExternCells,
        bool ObjectMode,
        IReadOnlySet<string>? Volatile = null);
}
