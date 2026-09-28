using CathodeRay.Assembler.Directives;

namespace CathodeRay.Assembler.Syntax;

/// <summary>Dialekty składni.</summary>
public static partial class SyntaxDialects
{
    /// <summary>Zilog (jak z80asm/z88dk): liczby <c>0FFH</c>, <c>$FF</c>, <c>0xFF</c>, <c>%1010</c>; <c>$</c> = PC;
    /// <c>nazwa EQU/DEFL wyr</c>; <c>ORG/DB/DEFB/DEFM/DW/DEFW/DS/DEFS/END</c>; symbole bez rozróżniania wielkości liter.</summary>
    public static SyntaxDialect Zilog { get; } = new()
    {
        Name = "zilog",
        Numbers = NumberFormats.Intel | NumberFormats.Motorola | NumberFormats.CStyle,
        ProgramCounter = '$',
        AssignmentKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EQU", "DEFL", "=" },
        Directives = SyntaxDialect.DirectiveTable(
            ("ORG", Directive.Org),
            ("DB", Directive.Byte),
            ("DEFB", Directive.Byte),
            ("DEFM", Directive.Byte),
            ("DW", Directive.Word),
            ("DEFW", Directive.Word),
            ("DS", Directive.Reserve),
            ("DEFS", Directive.Reserve),
            ("INCLUDE", Directive.Include),
            ("END", Directive.End)),
    };
}
