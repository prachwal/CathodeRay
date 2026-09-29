namespace CathodeRay.C;

/// <summary>Kod pośredni mini-C: trójadresowe operacje na komórkach pamięci. Komórka to nazwana
/// pamięć o szerokości 1 lub 2 bajtów; jej położenie, kolejność bajtów, ABI i dobór instrukcji należą
/// do celu (<see cref="ICTarget"/>). Operandy węższe od wyniku są rozszerzane zerem (uchar → int).</summary>
public static class Ir
{
    /// <summary>Operator dwuargumentowy.</summary>
    public enum BinOp
    {
        /// <summary>Dodawanie.</summary>
        Add,

        /// <summary>Odejmowanie.</summary>
        Sub,

        /// <summary>Koniunkcja bitowa.</summary>
        And,

        /// <summary>Alternatywa bitowa.</summary>
        Or,

        /// <summary>Alternatywa wykluczająca.</summary>
        Xor,

        /// <summary>Przesunięcie w lewo.</summary>
        Shl,

        /// <summary>Przesunięcie w prawo logiczne.</summary>
        Shr,

        /// <summary>Przesunięcie w prawo arytmetyczne (ze znakiem).</summary>
        Sar,

        /// <summary>Mnożenie (młodsze bity wyniku).</summary>
        Mul,

        /// <summary>Dzielenie bez znaku.</summary>
        Div,

        /// <summary>Reszta bez znaku.</summary>
        Mod,

        /// <summary>Dzielenie ze znakiem (do zera).</summary>
        DivS,

        /// <summary>Reszta ze znakiem (znak dzielnej).</summary>
        ModS,
    }

    /// <summary>Operator jednoargumentowy.</summary>
    public enum UnOp
    {
        /// <summary>Negacja arytmetyczna.</summary>
        Neg,

        /// <summary>Dopełnienie bitowe.</summary>
        Cpl,
    }

    /// <summary>Warunek skoku; przyrostek <c>u</c> oznacza porównanie bez znaku.</summary>
    public enum Cond
    {
        /// <summary>Równe.</summary>
        Eq,

        /// <summary>Różne.</summary>
        Ne,

        /// <summary>Mniejsze (ze znakiem).</summary>
        Lt,

        /// <summary>Mniejsze lub równe (ze znakiem).</summary>
        Le,

        /// <summary>Większe (ze znakiem).</summary>
        Gt,

        /// <summary>Większe lub równe (ze znakiem).</summary>
        Ge,

        /// <summary>Mniejsze (bez znaku).</summary>
        Ltu,

        /// <summary>Mniejsze lub równe (bez znaku).</summary>
        Leu,

        /// <summary>Większe (bez znaku).</summary>
        Gtu,

        /// <summary>Większe lub równe (bez znaku).</summary>
        Geu,
    }

    /// <summary>Operand instrukcji.</summary>
    public abstract record Op;

    /// <summary>Komórka pamięci o szerokości 1 lub 2 bajtów (zmienna, parametr albo tymczasowa).</summary>
    /// <param name="Sym">Symbol komórki.</param>
    /// <param name="W">Szerokość w bajtach.</param>
    public sealed record Cell(string Sym, int W) : Op;

    /// <summary>Stała.</summary>
    /// <param name="Value">Wartość (młodsze <paramref name="W"/> bajtów).</param>
    /// <param name="W">Szerokość w bajtach.</param>
    public sealed record Imm(int Value, int W) : Op;

    /// <summary>Adres symbolu z przesunięciem (wartość 16-bitowa; bez ukrytych komórek w generatorze).</summary>
    /// <param name="Sym">Symbol (zmienna, funkcja, napis).</param>
    /// <param name="Off">Przesunięcie w bajtach.</param>
    public sealed record AddrOf(string Sym, int Off) : Op;

    /// <summary>Instrukcja.</summary>
    public abstract record Ins;

    /// <summary>Kopia z konwersją szerokości (rozszerzenie zerem albo obcięcie).</summary>
    /// <param name="Dst">Cel.</param>
    /// <param name="Src">Źródło.</param>
    public sealed record Mov(Cell Dst, Op Src) : Ins;

    /// <summary>Działanie dwuargumentowe; szerokość wyniku wyznacza <paramref name="Dst"/> (operandy nie są szersze).</summary>
    /// <param name="Kind">Operator.</param>
    /// <param name="Dst">Wynik (może być tą samą komórką co operand).</param>
    /// <param name="A">Lewy operand.</param>
    /// <param name="B">Prawy operand (dla przesunięć: liczba pozycji).</param>
    public sealed record Bin(BinOp Kind, Cell Dst, Op A, Op B) : Ins;

    /// <summary>Działanie jednoargumentowe.</summary>
    /// <param name="Kind">Operator.</param>
    /// <param name="Dst">Wynik.</param>
    /// <param name="A">Operand.</param>
    public sealed record Un(UnOp Kind, Cell Dst, Op A) : Ins;

    /// <summary>Odczyt spod adresu <c>Ptr + Off</c> (<paramref name="Bytes"/> bajtów, rozszerzone zerem do szerokości <paramref name="Dst"/>).</summary>
    /// <param name="Dst">Cel.</param>
    /// <param name="Ptr">Adres bazowy: komórka 16-bitowa albo <see cref="AddrOf"/>.</param>
    /// <param name="Off">Stałe przesunięcie.</param>
    /// <param name="Bytes">Liczba czytanych bajtów (1 lub 2).</param>
    public sealed record Load(Cell Dst, Op Ptr, int Off, int Bytes) : Ins;

    /// <summary>Zapis pod adres <c>Ptr + Off</c> (młodsze <paramref name="Bytes"/> bajtów wartości).</summary>
    /// <param name="Ptr">Adres bazowy.</param>
    /// <param name="Off">Stałe przesunięcie.</param>
    /// <param name="Value">Wartość.</param>
    /// <param name="Bytes">Liczba zapisywanych bajtów (1 lub 2).</param>
    public sealed record Store(Op Ptr, int Off, Op Value, int Bytes) : Ins;

    /// <summary>Odczyt z tablicy o znanym adresie z indeksem w komórce: adres = <c>Sym + Off + (Index &lt;&lt; Shift)</c>. Powstaje dopiero
    /// tuż przed selektorem celu z indeksowaniem (<see cref="IndexFusion"/>), więc interpreter i przebiegi IR go nie widzą. Używany
    /// jest tylko młodszy bajt indeksu (tablica ma najwyżej 256 B).</summary>
    /// <param name="Dst">Cel.</param>
    /// <param name="Sym">Tablica.</param>
    /// <param name="Off">Stałe przesunięcie.</param>
    /// <param name="Index">Komórka indeksu.</param>
    /// <param name="Shift">Przesunięcie indeksu (rozmiar elementu = 1 &lt;&lt; Shift).</param>
    /// <param name="Bytes">Liczba czytanych bajtów.</param>
    public sealed record LoadIdx(Cell Dst, string Sym, int Off, Cell Index, int Shift, int Bytes) : Ins;

    /// <summary>Zapis do tablicy o znanym adresie z indeksem w komórce (zob. <see cref="LoadIdx"/>).</summary>
    /// <param name="Sym">Tablica.</param>
    /// <param name="Off">Stałe przesunięcie.</param>
    /// <param name="Index">Komórka indeksu.</param>
    /// <param name="Shift">Przesunięcie indeksu.</param>
    /// <param name="Value">Wartość.</param>
    /// <param name="Bytes">Liczba zapisywanych bajtów.</param>
    public sealed record StoreIdx(string Sym, int Off, Cell Index, int Shift, Op Value, int Bytes) : Ins;

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

    /// <summary>Skok warunkowy, gdy <c>A cond B</c> (operandy rozszerzone zerem do szerszego z nich).</summary>
    /// <param name="C">Warunek.</param>
    /// <param name="A">Lewy operand.</param>
    /// <param name="B">Prawy operand.</param>
    /// <param name="Target">Etykieta docelowa.</param>
    public sealed record BrCmp(Cond C, Op A, Op B, string Target) : Ins;

    /// <summary>Skok bezwarunkowy.</summary>
    /// <param name="Target">Etykieta docelowa.</param>
    public sealed record Jmp(string Target) : Ins;

    /// <summary>Etykieta.</summary>
    /// <param name="Name">Nazwa (unikalna w funkcji).</param>
    public sealed record Label(string Name) : Ins;

    /// <summary>Wywołanie funkcji po nazwie albo przez wskaźnik.</summary>
    /// <param name="Direct">Nazwa funkcji (albo <see langword="null"/> dla wołania pośredniego).</param>
    /// <param name="Indirect">Komórka z adresem funkcji (wołanie pośrednie).</param>
    /// <param name="Args">Argumenty.</param>
    /// <param name="ParamWidths">Szerokości parametrów (1/2), także dla argumentów wariadycznych.</param>
    /// <param name="Result">Komórka wyniku (null dla void); jej szerokość to szerokość wyniku.</param>
    public sealed record Call(string? Direct, Cell? Indirect, IReadOnlyList<Op> Args, IReadOnlyList<int> ParamWidths, Cell? Result) : Ins;

    /// <summary>Powrót z funkcji.</summary>
    /// <param name="Value">Zwracana wartość albo null.</param>
    /// <param name="W">Szerokość wyniku funkcji (0 dla void).</param>
    public sealed record Ret(Op? Value, int W) : Ins;

    /// <summary>Znacznik linii źródła C (mapa debugowa).</summary>
    /// <param name="File">Plik C albo null.</param>
    /// <param name="Line">Linia.</param>
    public sealed record Src(string? File, int Line) : Ins;

    /// <summary>Zakres pamięci zapisywany w ramce funkcji (callee-saves).</summary>
    /// <param name="Sym">Symbol.</param>
    /// <param name="Size">Rozmiar w bajtach.</param>
    /// <param name="Aggregate">Tablica/struktura (bajty po kolei), a nie komórka skalarna.</param>
    public sealed record Owned(string Sym, int Size, bool Aggregate);

    /// <summary>Funkcja.</summary>
    /// <param name="Name">Nazwa.</param>
    /// <param name="IsStatic">Symbol lokalny modułu.</param>
    /// <param name="Params">Komórki parametrów w kolejności deklaracji.</param>
    /// <param name="RetW">Szerokość wyniku (0 dla void).</param>
    /// <param name="Saved">Pamięć zapisywana w ramce.</param>
    /// <param name="Body">Instrukcje.</param>
    public sealed record Function(string Name, bool IsStatic, IReadOnlyList<Cell> Params, int RetW, IReadOnlyList<Owned> Saved, IReadOnlyList<Ins> Body);

    /// <summary>Fragment danych początkowych.</summary>
    public abstract record Piece;

    /// <summary>Bajty.</summary>
    /// <param name="Value">Wartości.</param>
    public sealed record Bytes(byte[] Value) : Piece;

    /// <summary>Słowo 16-bitowe z adresem symbolu (relokowane przez linker).</summary>
    /// <param name="Sym">Symbol.</param>
    /// <param name="Off">Przesunięcie.</param>
    public sealed record SymWord(string Sym, int Off) : Piece;

    /// <summary>Obiekt danych.</summary>
    /// <param name="Sym">Symbol.</param>
    /// <param name="Segment">Segment: <c>DATA</c> (z wartością), <c>BSS</c> (zerowany), <c>INIT</c> (tablica inicjalizatorów).</param>
    /// <param name="Size">Rozmiar w bajtach.</param>
    /// <param name="Init">Dane początkowe (null dla BSS).</param>
    /// <param name="Exported">Symbol widoczny dla innych modułów.</param>
    public sealed record Data(string Sym, string Segment, int Size, IReadOnlyList<Piece>? Init, bool Exported);

    /// <summary>Moduł: funkcje, dane i deklaracje zewnętrzne.</summary>
    /// <param name="Functions">Funkcje w kolejności źródła.</param>
    /// <param name="Data">Obiekty danych (także napisy, tablica INIT).</param>
    /// <param name="ExternFunctions">Funkcje zdefiniowane w innych modułach, do których moduł się odwołuje.</param>
    /// <param name="ExternCells">Zmienne <c>extern</c>.</param>
    /// <param name="ObjectMode">Tryb obiektowy (linker); w przeciwnym razie moduł jest całym programem.</param>
    public sealed record Module(
        IReadOnlyList<Function> Functions,
        IReadOnlyList<Data> Data,
        IReadOnlyList<string> ExternFunctions,
        IReadOnlyList<string> ExternCells,
        bool ObjectMode);
}
