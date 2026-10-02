namespace CathodeRay.C;

/// <summary>CPU z flagą przepełnienia (V) po odejmowaniu: wynik ze znakiem to S xor V, więc selektor nie odwraca
/// najstarszych bajtów przy porównaniu ze znakiem (bias <c>xor 80h</c>), tylko odejmuje (SUB/SBC) i skacze tym interfacem.</summary>
internal interface ISignedBranch
{
    /// <summary>CPU ma flagę przepełnienia po odejmowaniu (wynik ze znakiem to S xor V).</summary>
    bool HasOverflowFlag { get; }

    /// <summary>Skoki po porównaniu ze znakiem ze stałą 16-bitową (odejmowanie już wyemitowane przez selektor,
    /// spadek = gałąź else). Przepełnienie (V) rozstrzyga samo tam, gdzie jego sens zgadza się z gałęzią prawdy;
    /// w przeciwną stronę wołający materializuje pustą etykietę else — i tak taniej niż trampolina S^V.
    /// Wołane tylko dla <c>Lt</c>/<c>Ge</c> (resztę selektor sprowadza do nich przez +1).</summary>
    /// <param name="less">Skok przy <c>x &lt; C</c>; inaczej przy <c>x &gt;= C</c>.</param>
    /// <param name="constant">Stała (ze znakiem, -32768..32767).</param>
    /// <param name="target">Etykieta gałęzi prawdy.</param>
    /// <returns><see langword="true"/>, gdy sekwencja została wyemitowana.</returns>
    bool TryBranchSignedConst(bool less, int constant, string target);

    /// <summary>Skok po odejmowaniu ze znakiem <c>x - y</c> (A = najstarszy bajt różnicy, flagi S i V po nim); może zmienić A.</summary>
    /// <param name="less"><see langword="true"/>: skok, gdy <c>x &lt; y</c>; inaczej, gdy <c>x &gt;= y</c>.</param>
    /// <param name="label">Etykieta.</param>
    void JumpIfSigned(bool less, string label);
}
