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
            (".byte", Directive.Byte),
            (".include", Directive.Include),
            (".incbin", Directive.Incbin),
            (".macro", Directive.Macro),
            (".endmacro", Directive.EndMacro),
            (".local", Directive.Local),
            (".align", Directive.Align),
            (".segment", Directive.Segment),
            (".code", Directive.Code),
            (".data", Directive.Data),
            (".bss", Directive.Bss),
            (".global", Directive.Global),
            (".extern", Directive.Extern),
            (".if", Directive.If),
            (".elseif", Directive.ElseIf),
            (".else", Directive.Else),
            (".endif", Directive.EndIf)),
    };
}
