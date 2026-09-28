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
}
