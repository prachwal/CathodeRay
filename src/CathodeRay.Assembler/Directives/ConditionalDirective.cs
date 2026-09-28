namespace CathodeRay.Assembler.Directives;

/// <summary>Rodzaj dyrektywy warunkowej.</summary>
internal enum ConditionalKind
{
    /// <summary>Początek bloku: <c>.if wyr</c>, <c>IF wyr</c>.</summary>
    If,

    /// <summary>Kolejna gałąź: <c>.elseif wyr</c>, <c>ELIF wyr</c>.</summary>
    ElseIf,

    /// <summary>Gałąź zapasowa: <c>.else</c>, <c>ELSE</c>.</summary>
    Else,

    /// <summary>Koniec bloku: <c>.endif</c>, <c>ENDIF</c>.</summary>
    EndIf,
}

/// <summary>Znacznik bloku warunkowego: logika w przebiegu (<c>Pass</c>), nie tutaj.
/// Wykonanie = linia warunkowa poza obsługą przebiegu (błąd wewnętrzny).</summary>
internal sealed class ConditionalDirective(ConditionalKind kind) : IDirective
{
    /// <summary>Rodzaj dyrektywy.</summary>
    public ConditionalKind Kind => kind;

    public void Execute(IAssemblyContext context, string? operand) =>
        throw context.Error("conditional directive outside pass handling (internal error).");
}
