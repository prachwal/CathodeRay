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

    /// <summary>Przesunięcie względne 16-bit względem końca instrukcji (<c>{r16}</c>).</summary>
    Relative16,

    /// <summary>Przesunięcie ze znakiem −128..127 zapisane w kodzie U2, np. <c>(IX+d)</c> w Z80 (<c>{d}</c>).</summary>
    Displacement8,

    /// <summary>Indeksowe przesunięcie ze znakiem −32768..32767.</summary>
    Displacement16,

    /// <summary>Offset −16..15 zakodowany razem z indeksem X w postbyte Motorola 6809.</summary>
    Indexed5X,

    /// <summary>Offset −16..15 zakodowany razem z indeksem Y w postbyte Motorola 6809.</summary>
    Indexed5Y,

    /// <summary>Offset −16..15 zakodowany razem ze wskaźnikiem U w postbyte Motorola 6809.</summary>
    Indexed5U,

    /// <summary>Offset −16..15 zakodowany razem ze wskaźnikiem S w postbyte Motorola 6809.</summary>
    Indexed5S,

    /// <summary>Stała wybierająca opcode (<c>{c=N}</c>): wartość musi być równa N, nie trafia do kodu.</summary>
    Constant,
}
