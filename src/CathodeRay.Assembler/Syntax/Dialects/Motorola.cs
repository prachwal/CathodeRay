using CathodeRay.Assembler.Directives;

namespace CathodeRay.Assembler.Syntax;

/// <summary>Dialekty składni.</summary>
public static partial class SyntaxDialects
{
    /// <summary>Motorola 6800 (jak as6800, małe litery): liczby <c>$FF</c>, dziesiętne;
    /// <c>*</c> = PC; <c>nazwa EQU wyr</c>; <c>ORG/FCB/FDB/FCC/RMB/END</c>;
    /// symbole bez rozróżniania wielkości liter.</summary>
    public static SyntaxDialect Motorola { get; } = new()
    {
        Name = "motorola",
        Numbers = NumberFormats.Motorola,
        ProgramCounter = '*',
        LowHighPrefixes = true,
        AssignmentKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EQU", "=" },
        Directives = SyntaxDialect.DirectiveTable(
            ("ORG", Directive.Org),
            (".org", Directive.Org),
            ("FCB", Directive.Byte),
            (".fcb", Directive.Byte),
            (".byte", Directive.Byte),
            ("FDB", Directive.Word),
            (".fdb", Directive.Word),
            (".word", Directive.Word),
            ("FCC", Directive.Byte),
            ("ASCII", Directive.Byte),
            (".ascii", Directive.Byte),
            (".fcc", Directive.Byte),
            ("RMB", Directive.Reserve),
            (".rmb", Directive.Reserve),
            (".blkb", Directive.Reserve),
            ("INCLUDE", Directive.Include),
            ("INCBIN", Directive.Incbin),
            ("IF", Directive.If),
            ("ELSE", Directive.Else),
            ("ENDIF", Directive.EndIf),
            ("END", Directive.End)),
    };
}
