namespace CathodeRay.C;

/// <summary>Drzewo składni mini-C: program to lista funkcji; typy to <c>uchar</c>/<c>int</c>/<c>void</c>.</summary>
public static class Ast
{
    /// <summary>Wspólny korzeń węzłów.</summary>
    public abstract record Node;

    /// <summary>Program: globale i funkcje.</summary>
    /// <param name="Globals">Zmienne globalne.</param>
    /// <param name="Functions">Funkcje w kolejności źródła.</param>
    /// <param name="Lines">Linie węzłów (funkcje, instrukcje) do mapy debug.</param>
    /// <param name="Structs">Definicje struktur.</param>
    public sealed record Program(
        IReadOnlyList<Decl> Globals,
        IReadOnlyList<Function> Functions,
        IReadOnlyDictionary<Node, int>? Lines = null,
        IReadOnlyList<StructDef>? Structs = null) : Node;

    /// <summary>Pole definicji struktury.</summary>
    /// <param name="Type">Nazwa typu (<c>uchar</c>, <c>int</c>, <c>struct S</c>).</param>
    /// <param name="Stars">Liczba <c>*</c>.</param>
    /// <param name="Name">Nazwa pola.</param>
    /// <param name="ArrayLength">Długość tablicy (0 = skalar).</param>
    public sealed record FieldDecl(string Type, int Stars, string Name, int ArrayLength) : Node;

    /// <summary>Definicja <c>struct Nazwa { pola };</c>.</summary>
    /// <param name="Name">Nazwa struktury (anonimowe dostają nazwę syntetyczną).</param>
    /// <param name="Fields">Pola w kolejności deklaracji.</param>
    public sealed record StructDef(string Name, IReadOnlyList<FieldDecl> Fields) : Node;

    /// <summary>Definicja funkcji.</summary>
    /// <param name="ReturnType">Typ wyniku.</param>
    /// <param name="Name">Nazwa.</param>
    /// <param name="Params">Parametry.</param>
    /// <param name="Body">Ciało (puste dla prototypu).</param>
    /// <param name="IsExtern">Prototyp bez ciała (definicja w .s).</param>
    /// <param name="ReturnStars">Liczba <c>*</c> typu wyniku (wskaźnik).</param>
    /// <param name="IsStatic">Funkcja <c>static</c> (symbol lokalny modułu).</param>
    /// <param name="IsVariadic">Prototyp z <c>...</c>: dodatkowe argumenty idą jako 16-bit w kolejnych komórkach.</param>
    public sealed record Function(string ReturnType, string Name, IReadOnlyList<Param> Params, Block Body, bool IsExtern = false, int ReturnStars = 0, bool IsStatic = false, bool IsVariadic = false) : Node;

    /// <summary>Parametr formalny.</summary>
    /// <param name="Type">Typ.</param>
    /// <param name="Name">Nazwa.</param>
    /// <param name="PointerDepth">Liczba <c>*</c> (wskaźnik).</param>
    public sealed record Param(string Type, string Name, int PointerDepth = 0) : Node;

    /// <summary>Wspólny korzeń instrukcji.</summary>
    public abstract record Stmt : Node;

    /// <summary>Blok <c>{ ... }</c>.</summary>
    /// <param name="Items">Instrukcje i deklaracje.</param>
    public sealed record Block(IReadOnlyList<Stmt> Items) : Stmt;

    /// <summary>Deklaracja zmiennej z opcjonalną inicjalizacją.</summary>
    /// <param name="Type">Typ.</param>
    /// <param name="Name">Nazwa.</param>
    /// <param name="Init">Inicjalizator lub <see langword="null"/>.</param>
    /// <param name="PointerDepth">Liczba <c>*</c> (wskaźnik).</param>
    /// <param name="ArrayLength">Długość tablicy (0 = skalar).</param>
    /// <param name="LengthExpr">Długość jako stałe wyrażenie liczone przez checker (np. <c>sizeof(struct S)</c>).</param>
    /// <param name="Flags"><c>static</c>/<c>extern</c>.</param>
    public sealed record Decl(string Type, string Name, Expr? Init, int PointerDepth = 0, int ArrayLength = 0, Expr? LengthExpr = null, DeclFlags Flags = DeclFlags.None) : Stmt;

    /// <summary>Warunek z gałęzią else.</summary>
    /// <param name="Cond">Warunek.</param>
    /// <param name="Then">Gałąź prawdy.</param>
    /// <param name="Else">Gałąź fałszu lub <see langword="null"/>.</param>
    public sealed record If(Expr Cond, Stmt Then, Stmt? Else) : Stmt;

    /// <summary>Pętla <c>while</c>.</summary>
    /// <param name="Cond">Warunek.</param>
    /// <param name="Body">Ciało.</param>
    public sealed record While(Expr Cond, Stmt Body) : Stmt;

    /// <summary>Pętla <c>for</c>.</summary>
    /// <param name="Init">Inicjalizacja (deklaracja, wyrażenie lub <see langword="null"/>).</param>
    /// <param name="Cond">Warunek lub <see langword="null"/> (= prawda).</param>
    /// <param name="Step">Krok lub <see langword="null"/>.</param>
    /// <param name="Body">Ciało.</param>
    public sealed record For(Stmt? Init, Expr? Cond, Expr? Step, Stmt Body) : Stmt;

    /// <summary>Zwrot z funkcji.</summary>
    /// <param name="Value">Wartość lub <see langword="null"/>.</param>
    public sealed record Return(Expr? Value) : Stmt;

    /// <summary>Wyrażenie jako instrukcja.</summary>
    /// <param name="Value">Wyrażenie.</param>
    public sealed record ExprStmt(Expr Value) : Stmt;

    /// <summary>Pętla <c>do … while</c>.</summary>
    /// <param name="Body">Ciało.</param>
    /// <param name="Cond">Warunek (sprawdzany po ciele).</param>
    public sealed record DoWhile(Stmt Body, Expr Cond) : Stmt;

    /// <summary>Gałąź <c>switch</c>.</summary>
    /// <param name="Value">Stała <c>case</c> lub <see langword="null"/> dla <c>default</c>.</param>
    /// <param name="Body">Instrukcje do następnej etykiety (przechodzą dalej jak w C).</param>
    public sealed record SwitchCase(Expr? Value, IReadOnlyList<Stmt> Body) : Node;

    /// <summary>Instrukcja <c>switch</c> na <c>uchar</c>/<c>int</c> ze stałymi <c>case</c>.</summary>
    /// <param name="Value">Wyrażenie wybierające.</param>
    /// <param name="Cases">Gałęzie w kolejności źródła.</param>
    public sealed record Switch(Expr Value, IReadOnlyList<SwitchCase> Cases) : Stmt;

    /// <summary>Etykieta <c>nazwa:</c> (cel <c>goto</c>).</summary>
    /// <param name="Name">Nazwa.</param>
    public sealed record Label(string Name) : Stmt;

    /// <summary>Skok <c>goto nazwa;</c>.</summary>
    /// <param name="Name">Nazwa etykiety w tej samej funkcji.</param>
    public sealed record Goto(string Name) : Stmt;

    /// <summary>Instrukcja <c>break</c> (wyjście z najbliższej pętli).</summary>
    public sealed record Break : Stmt;

    /// <summary>Instrukcja <c>continue</c> (następna iteracja najbliższej pętli).</summary>
    public sealed record Continue : Stmt;

    /// <summary>Pusta instrukcja <c>;</c> (bez kodu).</summary>
    public sealed record Nop : Stmt;

    /// <summary>Wspólny korzeń wyrażeń.</summary>
    public abstract record Expr : Node;

    /// <summary>Rozmiar zmiennej lub tablicy w bajtach (<c>sizeof x</c>); <c>sizeof(typ)</c> składa parser.</summary>
    /// <param name="Name">Nazwa zmiennej.</param>
    public sealed record SizeOf(string Name) : Expr;

    /// <summary>Lista <c>{a, b, c}</c> inicjalizująca tablicę.</summary>
    /// <param name="Items">Elementy.</param>
    public sealed record InitList(IReadOnlyList<Expr> Items) : Expr;

    /// <summary>Literał napisowy: <c>uchar*</c> na bajty zakończone zerem.</summary>
    /// <param name="Value">Znaki (po rozwinięciu sekwencji <c>\</c>).</param>
    public sealed record Str(string Value) : Expr;

    /// <summary>Literał liczbowy (tekst źródłowy, wartość liczy codegen).</summary>
    /// <param name="Text">Tekst literału.</param>
    public sealed record Number(string Text) : Expr;

    /// <summary>Odczyt zmiennej.</summary>
    /// <param name="Name">Nazwa.</param>
    public sealed record Var(string Name) : Expr;

    /// <summary>Wywołanie funkcji.</summary>
    /// <param name="Name">Nazwa.</param>
    /// <param name="Args">Argumenty.</param>
    public sealed record Call(string Name, IReadOnlyList<Expr> Args) : Expr;

    /// <summary>Operator jednoargumentowy (<c>- ~ ! &amp; *</c>).</summary>
    /// <param name="Op">Operator.</param>
    /// <param name="Operand">Operand.</param>
    public sealed record Unary(string Op, Expr Operand) : Expr;

    /// <summary>Operator dwuargumentowy.</summary>
    /// <param name="Op">Operator.</param>
    /// <param name="Left">Lewa strona.</param>
    /// <param name="Right">Prawa strona.</param>
    public sealed record Binary(string Op, Expr Left, Expr Right) : Expr;

    /// <summary>Przypisanie (<c>=</c> i złożone, te znormalizowane do prostego).</summary>
    /// <param name="Name">Nazwa zmiennej.</param>
    /// <param name="Value">Wartość.</param>
    public sealed record Assign(string Name, Expr Value) : Expr;

    /// <summary>Operator warunkowy <c>a ? b : c</c>.</summary>
    /// <param name="Cond">Warunek.</param>
    /// <param name="Then">Wartość dla prawdy.</param>
    /// <param name="Else">Wartość dla fałszu.</param>
    public sealed record Ternary(Expr Cond, Expr Then, Expr Else) : Expr;

    /// <summary>Dereferencja wskaźnika (<c>*p</c>).</summary>
    /// <param name="Pointer">Wskaźnik.</param>
    public sealed record Deref(Expr Pointer) : Expr;

    /// <summary>Przypisanie przez wskaźnik/indeks (<c>*p = v</c>, <c>p[i] = v</c>).</summary>
    /// <param name="Target">Cel (<c>Deref</c> lub <c>Index</c>).</param>
    /// <param name="Value">Wartość.</param>
    public sealed record AssignTo(Expr Target, Expr Value) : Expr;

    /// <summary>Złożone przypisanie przez wskaźnik: <c>*p += v</c>, <c>p[i]++</c> (adres celu liczony raz).</summary>
    /// <param name="Target">Cel: <c>*p</c> lub <c>p[i]</c>.</param>
    /// <param name="Op">Operator bez <c>=</c>.</param>
    /// <param name="Value">Prawa strona.</param>
    /// <param name="Combined">Wyrażenie <c>Target Op Value</c> do kontroli typów.</param>
    public sealed record AssignOpTo(Expr Target, string Op, Expr Value, Expr Combined) : Expr;

    /// <summary>Dostęp do pola: <c>s.f</c> albo <c>p-&gt;f</c>.</summary>
    /// <param name="Base">Struktura (lwartość) albo wskaźnik do niej.</param>
    /// <param name="Name">Nazwa pola.</param>
    /// <param name="Arrow"><see langword="true"/> dla <c>-&gt;</c>.</param>
    public sealed record Member(Expr Base, string Name, bool Arrow) : Expr;

    /// <summary>Wywołanie przez wyrażenie: <c>tbl[i](x)</c>, <c>(*f)(x)</c>, <c>s.cb(x)</c> (wskaźnik do funkcji).</summary>
    /// <param name="Callee">Wyrażenie o typie wskaźnika do funkcji.</param>
    /// <param name="Args">Argumenty.</param>
    public sealed record CallExpr(Expr Callee, IReadOnlyList<Expr> Args) : Expr;

    /// <summary>Adres lwartości innej niż zmienna: <c>&amp;s.f</c>, <c>&amp;a[i]</c>, <c>&amp;*p</c>.</summary>
    /// <param name="Target">Lwartość.</param>
    public sealed record AddressOfExpr(Expr Target) : Expr;

    /// <summary>Rozmiar wyrażenia (<c>sizeof(*p)</c>, <c>sizeof(a[0])</c>, <c>sizeof s.f</c>); nie jest wykonywane.</summary>
    /// <param name="Operand">Operand.</param>
    public sealed record SizeOfExpr(Expr Operand) : Expr;

    /// <summary>Rozmiar typu strukturalnego (<c>sizeof(struct S)</c>).</summary>
    /// <param name="Type">Nazwa typu (<c>struct S</c>).</param>
    /// <param name="Stars">Liczba <c>*</c>.</param>
    public sealed record SizeOfType(string Type, int Stars) : Expr;

    /// <summary>Rzutowanie <c>(T)x</c> między typami skalarnymi (uchar, int, uint, wskaźniki, wskaźniki do funkcji).</summary>
    /// <param name="Type">Nazwa typu bazowego (także <c>fptr&lt;…&gt;</c>).</param>
    /// <param name="Stars">Liczba <c>*</c>.</param>
    /// <param name="Value">Rzutowane wyrażenie.</param>
    public sealed record Cast(string Type, int Stars, Expr Value) : Expr;

    /// <summary>Adres zmiennej (<c>&amp;x</c>).</summary>
    /// <param name="Name">Nazwa zmiennej.</param>
    public sealed record AddressOf(string Name) : Expr;

    /// <summary>Indeksowanie (<c>p[i]</c>).</summary>
    /// <param name="Base">Wskaźnik.</param>
    /// <param name="Offset">Indeks.</param>
    public sealed record Index(Expr Base, Expr Offset) : Expr;
}
