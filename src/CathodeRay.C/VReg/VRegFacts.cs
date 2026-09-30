namespace CathodeRay.C;

/// <summary>Fakty o instrukcjach VReg wspólne dla przebiegów i analizy żywości: jedna definicja odczytywanych
/// operandów, zapisywanego rejestru i kluczy zbiorów żywości (wzór: <see cref="IrFacts"/>).</summary>
internal static class VRegFacts
{
    /// <summary>Operandy czytane przez instrukcję (także wskaźniki, argumenty i cel wołania pośredniego).</summary>
    /// <param name="ins">Instrukcja.</param>
    /// <returns>Czytane operandy.</returns>
    public static IEnumerable<VReg.Op> Uses(VReg.Ins ins) => ins switch
    {
        VReg.Mov mov => [mov.Src],
        VReg.Bin bin => [bin.A, bin.B],
        VReg.Un un => [un.A],
        VReg.Load load => [load.Ptr],
        VReg.Store store => [store.Ptr, store.Value],
        VReg.CopyBlock copy => [copy.Dst, copy.Src],
        VReg.Fill fill => [fill.Dst],
        VReg.Cmp cmp => [cmp.A, cmp.B],
        VReg.Br br => [br.C],
        VReg.Call call => (call.Indirect is null) ? call.Args : [.. call.Args, call.Indirect],
        VReg.Ret { Value: not null } ret => [ret.Value],
        _ => [],
    };

    /// <summary>Rejestr zapisywany przez instrukcję (wynik działania, odczytu z pamięci, porównania albo wołania).</summary>
    /// <param name="ins">Instrukcja.</param>
    /// <returns>Zapisywany rejestr albo <see langword="null"/>.</returns>
    public static VReg.Reg? Def(VReg.Ins ins) => ins switch
    {
        VReg.Mov mov => mov.Dst,
        VReg.Bin bin => bin.Dst,
        VReg.Un un => un.Dst,
        VReg.Load load => load.Dst,
        VReg.Cmp cmp => cmp.Dst,
        VReg.Call call => call.Result,
        _ => null,
    };

    /// <summary>Klucz operandu w zbiorach żywości: rejestr to <c>r{id}</c>, pamięć (<see cref="VReg.Pinned"/>,
    /// <see cref="VReg.Addr"/>) to <c>m{baza}</c> (jak <see cref="IrLiveness.BaseSymbol"/>), stała nie żyje.</summary>
    /// <param name="op">Operand.</param>
    /// <returns>Klucz albo <see langword="null"/> dla stałej.</returns>
    public static string? Key(VReg.Op op) => op switch
    {
        VReg.Reg reg => "r" + reg.Id,
        VReg.Pinned pinned => "m" + BaseSymbol(pinned.Sym),
        VReg.Addr addr => "m" + addr.Sym,
        _ => null,
    };

    /// <summary>Symbol obiektu bez przesunięcia połówki (<c>x+2</c> to część obiektu <c>x</c>).</summary>
    /// <param name="symbol">Symbol.</param>
    /// <returns>Symbol bazowy.</returns>
    public static string BaseSymbol(string symbol)
    {
        int plus = symbol.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? symbol : symbol[..plus];
    }
}
