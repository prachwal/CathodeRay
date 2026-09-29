namespace CathodeRay.C;

/// <summary>Wybór komórek modułu, które lądują na stronie zerowej 6502 (krótsze i szybsze operandy oraz wskaźniki wprost w
/// <c>(zp),Y</c>). Waga komórki to liczba odwołań, gdzie odwołanie w pętli liczy się 8× na każdy poziom zagnieżdżenia, a użycie jako
/// wskaźnika dostaje premię (oszczędza kopię do <c>__p</c>). Budżet na moduł jest mały, bo strona zerowa jest wspólna dla całego programu.</summary>
internal static class ZeroPageAllocator
{
    /// <summary>Największa liczba bajtów strony zerowej na moduł.</summary>
    public const int BudgetPerModule = 16;

    private const int PointerBonus = 12;

    /// <summary>Przenosi najczęściej używane komórki BSS do segmentu <c>ZP</c>.</summary>
    /// <param name="module">Moduł po legalizacji.</param>
    /// <returns>Moduł ze zmienionymi segmentami oraz nazwy (bez dopisków <c>+n</c>) komórek na stronie zerowej.</returns>
    public static (Ir.Module Module, HashSet<string> Names) Run(Ir.Module module)
    {
        var weights = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (Ir.Function function in module.Functions)
        {
            long[] depth = LoopWeights(function.Body);
            for (int i = 0; i < function.Body.Count; i++)
            {
                Ir.Ins ins = function.Body[i];
                foreach (Ir.Op op in Operands(ins))
                {
                    if (op is Ir.Cell cell)
                    {
                        Add(weights, cell.Sym, depth[i]);
                    }
                }

                if (ins is Ir.Load { Ptr: Ir.Cell loadPointer })
                {
                    Add(weights, loadPointer.Sym, depth[i] * PointerBonus);
                }
                else if (ins is Ir.Store { Ptr: Ir.Cell storePointer })
                {
                    Add(weights, storePointer.Sym, depth[i] * PointerBonus);
                }
            }
        }

        var chosen = new HashSet<string>(StringComparer.Ordinal);
        int used = 0;
        foreach (Ir.Data data in module.Data
            .Where(static d => d.Segment == "BSS" && !d.Exported && d.Size is >= 1 and <= 4 && !d.Sym.StartsWith("cc_g_", StringComparison.Ordinal) && d.Sym.Length > 0)
            .Where(d => weights.ContainsKey(d.Sym))
            .OrderByDescending(d => (double)weights[d.Sym] / d.Size)
            .ThenBy(static d => d.Sym, StringComparer.Ordinal))
        {
            if (used + data.Size <= BudgetPerModule)
            {
                chosen.Add(data.Sym);
                used += data.Size;
            }
        }

        Ir.Data[] moved = [.. module.Data.Select(d => chosen.Contains(d.Sym) ? d with { Segment = "ZP" } : d)];
        return (module with { Data = moved }, chosen);
    }

    private static void Add(Dictionary<string, long> weights, string sym, long weight)
    {
        int plus = sym.IndexOf('+', StringComparison.Ordinal);
        string key = plus < 0 ? sym : sym[..plus];
        weights[key] = weights.GetValueOrDefault(key) + weight;
    }

    /// <summary>Waga instrukcji: 8 do potęgi liczby pętli (przedziałów od etykiety do skoku wstecz do niej), które ją obejmują.</summary>
    private static long[] LoopWeights(IReadOnlyList<Ir.Ins> body)
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

    private static IEnumerable<Ir.Op> Operands(Ir.Ins ins) => ins switch
    {
        Ir.Mov mov => [mov.Dst, mov.Src],
        Ir.Bin bin => [bin.Dst, bin.A, bin.B],
        Ir.Un un => [un.Dst, un.A],
        Ir.Load load => [load.Dst, load.Ptr],
        Ir.Store store => [store.Ptr, store.Value],
        Ir.LoadIdx loadIdx => [loadIdx.Dst, loadIdx.Index],
        Ir.StoreIdx storeIdx => [storeIdx.Index, storeIdx.Value],
        Ir.BrCmp branch => [branch.A, branch.B],
        Ir.Call call => [.. call.Args, .. call.Result is null ? [] : new Ir.Op[] { call.Result }, .. call.Indirect is null ? [] : new Ir.Op[] { call.Indirect }],
        Ir.Ret { Value: { } value } => [value],
        _ => [],
    };
}
