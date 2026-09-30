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
            long[] depth = IrFacts.LoopWeights(function.Body);
            for (int i = 0; i < function.Body.Count; i++)
            {
                Ir.Ins ins = function.Body[i];
                foreach (Ir.Op op in IrFacts.Operands(ins))
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
}
