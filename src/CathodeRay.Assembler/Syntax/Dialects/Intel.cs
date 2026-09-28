using CathodeRay.Assembler.Directives;

namespace CathodeRay.Assembler.Syntax;

/// <summary>Dialekty składni.</summary>
public static partial class SyntaxDialects
{
    /// <summary>Intel ASM80: <c>0FFH</c>, <c>101B</c>, <c>17Q</c>, <c>$</c> = PC, <c>ORG/DB/DW/DS/END</c>, <c>nazwa EQU wyr</c>,
    /// operatory słowne (<c>AND</c>, <c>SHL</c>, <c>HIGH</c>, …), symbole bez rozróżniania wielkości liter.</summary>
    public static SyntaxDialect Intel { get; } = new()
    {
        Name = "intel",
        Numbers = NumberFormats.Intel,
        ProgramCounter = '$',
        WordOperators = true,
        AssignmentKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EQU", "SET", "=" },
        Directives = SyntaxDialect.DirectiveTable(
            ("ORG", Directive.Org),
            ("DB", Directive.Byte),
            ("DW", Directive.Word),
            ("DS", Directive.Reserve),
            ("END", Directive.End)),
    };
}
