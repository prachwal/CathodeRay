namespace CathodeRay.Assembler.Isa;

/// <summary>Rodzaj pola operandu w kodzie maszynowym.</summary>
public enum FieldKind
{
    /// <summary>Bajt 0..255 (<c>{b}</c>).</summary>
    Byte,

    /// <summary>Słowo 0..65535 w kolejności bajtów celu (<c>{w}</c>).</summary>
    Word,

    /// <summary>Przesunięcie −128..127 względem adresu następnej instrukcji (<c>{r}</c>).</summary>
    Relative8,

    /// <summary>Przesunięcie ze znakiem −128..127 zapisane w kodzie U2, np. <c>(IX+d)</c> w Z80 (<c>{d}</c>).</summary>
    Displacement8,

    /// <summary>Stała wybierająca opcode (<c>{c=N}</c>): wartość musi być równa N, nie trafia do kodu.</summary>
    Constant,
}
