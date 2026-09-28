namespace CathodeRay.Assembler.Directives;

/// <summary>Rodzaj znacznika makra.</summary>
internal enum MacroKind
{
    /// <summary>Definicja: <c>.macro nazwa args</c>, <c>NAZWA: MACRO args</c>.</summary>
    Macro,

    /// <summary>Koniec definicji: <c>.endmacro</c>, <c>ENDM</c>.</summary>
    EndMacro,

    /// <summary>Symbole lokalne per rozwinięcie: <c>.local a, b</c>, <c>LOCAL a, b</c>.</summary>
    Local,
}

/// <summary>Znacznik makra: logika w pre-passie (<c>MacroExpander</c>), nie tutaj.
/// Wykonanie = linia makra poza pre-passem (błąd wewnętrzny).</summary>
internal sealed class MacroDirective(MacroKind kind) : IDirective
{
    /// <summary>Rodzaj znacznika.</summary>
    public MacroKind Kind => kind;

    public void Execute(IAssemblyContext context, string? operand) =>
        throw context.Error("macro directive outside pre-pass handling (internal error).");
}
