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
    public sealed record Program(
        IReadOnlyList<Decl> Globals,
        IReadOnlyList<Function> Functions,
        IReadOnlyDictionary<Node, int>? Lines = null) : Node;

    /// <summary>Definicja funkcji.</summary>
    /// <param name="ReturnType">Typ wyniku.</param>
    /// <param name="Name">Nazwa.</param>
    /// <param name="Params">Parametry.</param>
    /// <param name="Body">Ciało (puste dla prototypu).</param>
    /// <param name="IsExtern">Prototyp bez ciała (definicja w .s).</param>
    public sealed record Function(string ReturnType, string Name, IReadOnlyList<Param> Params, Block Body, bool IsExtern = false) : Node;

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
    public sealed record Decl(string Type, string Name, Expr? Init, int PointerDepth = 0, int ArrayLength = 0) : Stmt;

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

    /// <summary>Instrukcja <c>break</c> (wyjście z najbliższej pętli).</summary>
    public sealed record Break : Stmt;

    /// <summary>Instrukcja <c>continue</c> (następna iteracja najbliższej pętli).</summary>
    public sealed record Continue : Stmt;

    /// <summary>Pusta instrukcja <c>;</c> (bez kodu).</summary>
    public sealed record Nop : Stmt;

    /// <summary>Wspólny korzeń wyrażeń.</summary>
    public abstract record Expr : Node;

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

    /// <summary>Adres zmiennej (<c>&amp;x</c>).</summary>
    /// <param name="Name">Nazwa zmiennej.</param>
    public sealed record AddressOf(string Name) : Expr;

    /// <summary>Indeksowanie (<c>p[i]</c>).</summary>
    /// <param name="Base">Wskaźnik.</param>
    /// <param name="Offset">Indeks.</param>
    public sealed record Index(Expr Base, Expr Offset) : Expr;
}
