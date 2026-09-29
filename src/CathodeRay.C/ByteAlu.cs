namespace CathodeRay.C;

/// <summary>Działanie arytmetyczno-logiczne na akumulatorze.</summary>
internal enum ByteAlu
{
    /// <summary>A = A + b (z przeniesieniem, gdy nie pierwszy bajt).</summary>
    Add,

    /// <summary>A = A - b (z pożyczką, gdy nie pierwszy bajt).</summary>
    Sub,

    /// <summary>A = A and b.</summary>
    And,

    /// <summary>A = A or b.</summary>
    Or,

    /// <summary>A = A xor b.</summary>
    Xor,
}
