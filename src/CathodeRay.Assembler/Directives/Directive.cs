namespace CathodeRay.Assembler.Directives;

/// <summary>Standardowe dyrektywy wspólne dla dialektów (bezstanowe, współdzielone instancje).</summary>
public static class Directive
{
    /// <summary>Ustawia adres: <c>.org</c>, <c>ORG</c>, <c>*=</c>.</summary>
    public static IDirective Org { get; } = new OrgDirective();

    /// <summary>Bajty i łańcuchy: <c>.byte</c>, <c>DB</c>, <c>.BYTE</c>.</summary>
    public static IDirective Byte { get; } = new DataDirective(width: 1);

    /// <summary>Słowa w kolejności bajtów celu: <c>.word</c>, <c>DW</c>, <c>.WORD</c>.</summary>
    public static IDirective Word { get; } = new DataDirective(width: 2);

    /// <summary>Rezerwuje n bajtów wypełnionych zerem lub podaną wartością: <c>.res n[,wartość]</c>, <c>DS n</c>.</summary>
    public static IDirective Reserve { get; } = new ReserveDirective();

    /// <summary>Kończy asemblację: <c>END</c>, <c>.end</c>.</summary>
    public static IDirective End { get; } = new EndDirective();

    /// <summary>Wstawia plik (<c>.include</c>, <c>INCLUDE</c>): ekspansja przed pierwszym przebiegiem.</summary>
    public static IDirective Include { get; } = new IncludeDirective();

    /// <summary>Wstawia plik binarny do obrazu (<c>.incbin</c>, <c>INCBIN</c>).</summary>
    public static IDirective Incbin { get; } = new IncbinDirective();

    /// <summary>Symbole lokalne per rozwinięcie (<c>.local</c>, <c>LOCAL</c>).</summary>
    public static IDirective Local { get; } = new MacroDirective(MacroKind.Local);

    /// <summary>Definicja makra (<c>.macro</c>, <c>MACRO</c>).</summary>
    public static IDirective Macro { get; } = new MacroDirective(MacroKind.Macro);

    /// <summary>Koniec definicji makra (<c>.endmacro</c>, <c>ENDM</c>).</summary>
    public static IDirective EndMacro { get; } = new MacroDirective(MacroKind.EndMacro);

    /// <summary>Wyrównuje adres (<c>.align</c>, <c>ALIGN</c>).</summary>
    public static IDirective Align { get; } = new AlignDirective();

    /// <summary>Nazwany segment emitujący (<c>.segment</c>, <c>SEGMENT</c>).</summary>
    public static IDirective Segment { get; } = new SegmentDirective(SegmentKind.Segment);

    /// <summary>Segment kodu (<c>.code</c>, <c>CODE</c>).</summary>
    public static IDirective Code { get; } = new SegmentDirective(SegmentKind.Code);

    /// <summary>Segment danych (<c>.data</c>, <c>DATA</c>).</summary>
    public static IDirective Data { get; } = new SegmentDirective(SegmentKind.Data);

    /// <summary>Segment nieemitowany (<c>.bss</c>, <c>BSS</c>).</summary>
    public static IDirective Bss { get; } = new SegmentDirective(SegmentKind.Bss);

    /// <summary>Blok warunkowy: <c>.if</c> / <c>IF</c> (logika w przebiegu).</summary>
    public static IDirective If { get; } = new ConditionalDirective(ConditionalKind.If);

    /// <summary>Blok warunkowy: <c>.elseif</c> / <c>ELIF</c>.</summary>
    public static IDirective ElseIf { get; } = new ConditionalDirective(ConditionalKind.ElseIf);

    /// <summary>Blok warunkowy: <c>.else</c> / <c>ELSE</c>.</summary>
    public static IDirective Else { get; } = new ConditionalDirective(ConditionalKind.Else);

    /// <summary>Blok warunkowy: <c>.endif</c> / <c>ENDIF</c>.</summary>
    public static IDirective EndIf { get; } = new ConditionalDirective(ConditionalKind.EndIf);
}
