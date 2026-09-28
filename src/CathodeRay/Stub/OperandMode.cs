namespace CathodeRay.Stub;

/// <summary>Tryb adresowania instrukcji zaślepki (pole <c>operands[0].type</c> w JSON).</summary>
public enum OperandMode
{
    /// <summary>Bez operandu (1 słowo).</summary>
    None,

    /// <summary>Wartość natychmiastowa 8-bit (<c>immediate8</c>, 2 słowa).</summary>
    Immediate8,

    /// <summary>Adres bezwzględny 16-bit (<c>address16</c>, 3 słowa).</summary>
    Address16,

    /// <summary>Adres 16-bit + X (<c>address16_x</c>, 3 słowa); w asemblerze <c>adres,X</c>.</summary>
    Address16X,
}
