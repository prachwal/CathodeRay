using CathodeRay.Assembler.Directives;

namespace CathodeRay.Assembler.Syntax;

/// <summary>Dialekty składni.</summary>
public static partial class SyntaxDialects
{
    /// <summary>Oryginalny MOS/Commodore: <c>*= $1000</c>, <c>.BYTE</c>, <c>.WORD</c>, etykiety od kolumny 1 bez dwukropka.</summary>
    public static SyntaxDialect Mos { get; } = new()
    {
        Name = "mos",
        Numbers = NumberFormats.Motorola,
        ProgramCounter = '*',
        OrgByAssignment = true,
        LabelsWithoutColon = true,
        LowHighPrefixes = true,
        Directives = SyntaxDialect.DirectiveTable(
            ("*=", Directive.Org),
            (".BYTE", Directive.Byte),
            (".WORD", Directive.Word),
            (".INCLUDE", Directive.Include),
            (".INCBIN", Directive.Incbin),
            (".MACRO", Directive.Macro),
            (".ENDMACRO", Directive.EndMacro),
            (".LOCAL", Directive.Local),
            (".ALIGN", Directive.Align),
            (".SCOPE", Directive.Scope),
            (".PROC", Directive.Proc),
            (".ENDSCOPE", Directive.EndScope),
            (".ENDPROC", Directive.EndProc),
            (".SEGMENT", Directive.Segment),
            (".CODE", Directive.Code),
            (".DATA", Directive.Data),
            (".BSS", Directive.Bss),
            (".GLOBAL", Directive.Global),
            (".EXTERN", Directive.Extern),
            (".IF", Directive.If),
            (".ELSEIF", Directive.ElseIf),
            (".ELSE", Directive.Else),
            (".ENDIF", Directive.EndIf),
            (".END", Directive.End)),
    };
}
