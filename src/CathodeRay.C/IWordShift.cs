namespace CathodeRay.C;

/// <summary>CPU umie podwoić słowo 16-bitowe jedną sekwencją (Z80 <c>add hl,hl</c>, 8080 <c>dad h</c>).</summary>
internal interface IWordShift
{
    /// <summary>Słowo &lt;&lt; 1 przez HL; po niej HL niesie wynik.</summary>
    /// <param name="dst">Cel (pamięć albo para).</param>
    /// <param name="src">Źródło: stała, pamięć albo para.</param>
    /// <returns>Wynik jawny (<see cref="WordResult"/>).</returns>
    WordResult TryShlWord1(Word dst, Word src);
}
