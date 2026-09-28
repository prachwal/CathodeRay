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
            (".END", Directive.End)),
    };
}
