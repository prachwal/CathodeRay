using CathodeRay.Assembler.Directives;

namespace CathodeRay.Assembler.Syntax;

/// <summary>Dialekty składni.</summary>
public static partial class SyntaxDialects
{
    /// <summary>Składnia zaślepki: <c>$FF</c> i <c>0xFF</c>, <c>.org</c>/<c>.byte</c>, etykiety z dwukropkiem, bez rozróżniania wielkości liter.</summary>
    public static SyntaxDialect Stub { get; } = new()
    {
        Name = "stub",
        Numbers = NumberFormats.Motorola | NumberFormats.CStyle,
        Directives = SyntaxDialect.DirectiveTable(
            (".org", Directive.Org),
            (".byte", Directive.Byte)),
    };
}
