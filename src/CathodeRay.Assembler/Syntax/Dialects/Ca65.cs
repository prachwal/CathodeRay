using CathodeRay.Assembler.Directives;

namespace CathodeRay.Assembler.Syntax;

/// <summary>Dialekty składni.</summary>
public static partial class SyntaxDialects
{
    /// <summary>ca65 (cc65): <c>$FF</c>, <c>%101</c>, <c>*</c>, <c>&lt;</c>/<c>&gt;</c>, etykiety z dwukropkiem, symbole z rozróżnieniem wielkości liter.</summary>
    public static SyntaxDialect Ca65 { get; } = new()
    {
        Name = "ca65",
        Numbers = NumberFormats.Motorola,
        ProgramCounter = '*',
        CaseSensitiveSymbols = true,
        LowHighPrefixes = true,
        Directives = SyntaxDialect.DirectiveTable(
            (".org", Directive.Org),
            (".byte", Directive.Byte),
            (".byt", Directive.Byte),
            (".word", Directive.Word),
            (".addr", Directive.Word),
            (".res", Directive.Reserve),
            (".include", Directive.Include),
            (".incbin", Directive.Incbin),
            (".macro", Directive.Macro),
            (".endmacro", Directive.EndMacro),
            (".local", Directive.Local),
            (".align", Directive.Align),
            (".if", Directive.If),
            (".elseif", Directive.ElseIf),
            (".else", Directive.Else),
            (".endif", Directive.EndIf),
            (".end", Directive.End)),
    };
}
