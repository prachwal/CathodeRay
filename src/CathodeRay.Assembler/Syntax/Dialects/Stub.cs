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
            (".word", Directive.Word),
            (".res", Directive.Reserve),
            (".include", Directive.Include),
            (".incbin", Directive.Incbin),
            (".macro", Directive.Macro),
            (".endmacro", Directive.EndMacro),
            (".local", Directive.Local),
            (".align", Directive.Align),
            (".scope", Directive.Scope),
            (".proc", Directive.Proc),
            (".endscope", Directive.EndScope),
            (".endproc", Directive.EndProc),
            (".out", Directive.Out),
            (".warning", Directive.Warning),
            (".error", Directive.Error),
            (".assert", Directive.Assert),
            (".define", Directive.Define),
            (".ifblank", Directive.IfBlank),
            (".ifnblank", Directive.IfNBlank),
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
