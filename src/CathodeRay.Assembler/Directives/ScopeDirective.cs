namespace CathodeRay.Assembler.Directives;

/// <summary>Rodzaj dyrektywy zakresu leksykalnego.</summary>
internal enum ScopeKind
{
    /// <summary>Zakres nazwany lub anonimowy: <c>.scope [nazwa]</c> (nie definiuje etykiety).</summary>
    Scope,

    /// <summary>Procedura: <c>.proc nazwa</c> (definiuje też globalną etykietę).</summary>
    Proc,

    /// <summary>Koniec zakresu: <c>.endscope</c>.</summary>
    EndScope,

    /// <summary>Koniec procedury: <c>.endproc</c>.</summary>
    EndProc,
}

/// <summary>Znacznik zakresu: logika w przebiegu (<c>Pass</c>), nie tutaj.
/// Wykonanie = linia zakresu poza obsługą przebiegu (błąd wewnętrzny).</summary>
internal sealed class ScopeDirective(ScopeKind kind) : IDirective
{
    /// <summary>Rodzaj dyrektywy.</summary>
    public ScopeKind Kind => kind;

    public void Execute(IAssemblyContext context, string? operand) =>
        throw context.Error("scope directive outside pass handling (internal error).");
}
