namespace CathodeRay.C;

/// <summary>Fakty o instrukcjach IR wspólne dla przebiegów: jedna definicja odczytywanych operandów, zapisywanej komórki
/// i wag pętli.</summary>
internal static class IrFacts
{
    /// <summary>Operandy czytane przez instrukcję (także wskaźniki, argumenty i cel wołania pośredniego).</summary>
    /// <param name="ins">Instrukcja.</param>
    /// <returns>Czytane operandy.</returns>
    public static IEnumerable<Ir.Op> Uses(Ir.Ins ins) => ins switch
    {
        Ir.Mov mov => [mov.Src],
        Ir.Bin bin => [bin.A, bin.B],
        Ir.Un un => [un.A],
        Ir.Load load => [load.Ptr],
        Ir.Store store => [store.Ptr, store.Value],
        Ir.LoadIdx loadIdx => [loadIdx.Index],
        Ir.StoreIdx storeIdx => [storeIdx.Index, storeIdx.Value],
        Ir.CopyBlock copy => [copy.Dst, copy.Src],
        Ir.Fill fill => [fill.Dst],
        Ir.BrCmp branch => [branch.A, branch.B],
        Ir.Call call => (call.Indirect is null) ? call.Args : [.. call.Args, call.Indirect],
        Ir.Ret { Value: not null } ret => [ret.Value],
        _ => [],
    };

    /// <summary>Komórka zapisywana przez instrukcję (wynik działania, odczytu z pamięci albo wołania).</summary>
    /// <param name="ins">Instrukcja.</param>
    /// <returns>Zapisywana komórka albo <see langword="null"/>.</returns>
    public static Ir.Cell? Def(Ir.Ins ins) => ins switch
    {
        Ir.Mov mov => mov.Dst,
        Ir.Bin bin => bin.Dst,
        Ir.Un un => un.Dst,
        Ir.Load load => load.Dst,
        Ir.LoadIdx loadIdx => loadIdx.Dst,
        Ir.Call call => call.Result,
        _ => null,
    };

    /// <summary>Wszystkie operandy instrukcji: zapisywana komórka i operandy czytane.</summary>
    /// <param name="ins">Instrukcja.</param>
    /// <returns>Operandy.</returns>
    public static IEnumerable<Ir.Op> Operands(Ir.Ins ins) => (Def(ins) is { } def) ? Uses(ins).Prepend(def) : Uses(ins);

    /// <summary>Waga instrukcji: 8 do potęgi liczby pętli (przedziałów od etykiety do skoku wstecz do niej), które ją obejmują.</summary>
    /// <param name="body">Ciało funkcji.</param>
    /// <returns>Waga każdej instrukcji.</returns>
    public static long[] LoopWeights(IReadOnlyList<Ir.Ins> body)
    {
        var labels = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < body.Count; i++)
        {
            if (body[i] is Ir.Label label)
            {
                labels[label.Name] = i;
            }
        }

        var nesting = new int[body.Count];
        for (int i = 0; i < body.Count; i++)
        {
            string? target = body[i] switch
            {
                Ir.Jmp jump => jump.Target,
                Ir.BrCmp branch => branch.Target,
                _ => null,
            };
            if (target is not null && labels.TryGetValue(target, out int start) && start <= i)
            {
                for (int k = start; k <= i; k++)
                {
                    nesting[k]++;
                }
            }
        }

        return [.. nesting.Select(static n => (long)Math.Pow(8, Math.Min(n, 4)))];
    }
}
