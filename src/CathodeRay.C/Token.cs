namespace CathodeRay.C;

/// <summary>Rodzaj tokenu mini-C.</summary>
public enum TokenKind
{
    /// <summary>Koniec wejścia.</summary>
    End,

    /// <summary>Identyfikator.</summary>
    Ident,

    /// <summary>Literał liczbowy (dziesiętny, <c>0x</c> hex).</summary>
    Number,

    /// <summary>Słowo kluczowe (<c>uchar/int/void/if/else/while/for/return</c>).</summary>
    Keyword,

    /// <summary>Operator lub znak przestankowy.</summary>
    Punct,
}

/// <summary>Token z pozycją (linie i kolumny od 1, jak w edytorach).</summary>
/// <param name="Kind">Rodzaj.</param>
/// <param name="Text">Tekst źródłowy.</param>
/// <param name="Line">Linia (od 1).</param>
/// <param name="Column">Kolumna (od 1).</param>
public sealed record Token(TokenKind Kind, string Text, int Line, int Column);
