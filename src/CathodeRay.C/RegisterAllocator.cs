namespace CathodeRay.C;

/// <summary>Przydział rejestrów komórkom celów z mapą rejestrów w ISA (<see cref="ByteIsa.CellRegisters"/>, <see cref="ByteIsa.CellPairs"/>:
/// Z80 i 8080 — B, C, D, E; wersja 1, konwencja caller-saved: wołany niszczy wszystko). Kandydat
/// to komórka BSS modułu o zasięgu odwołań 1 albo 2 bajty, której symbol występuje w dokładnie jednej funkcji (nie przedrostek
/// nazwy: tymczasowe <c>__lg*</c>/<c>__wl*</c> i komórki z inline'owania bywają wspólne), nie jest żywa na wejściu funkcji (wartość
/// z poprzedniej aktywacji, np. lokalnej <c>static</c>, nigdy nie jest czytana), nie jest żywa za żadnym <see cref="Ir.Call"/>
/// (także wywołaniem pośrednim i procedurą wykonawczą wstawioną przez legalizację), nie jest wzięta adresem, <c>volatile</c>,
/// zapisywana w ramce (<see cref="Ir.Function.Saved"/>), parametrem, zewnętrzna ani eksportowana. Żywość na grafie przepływu
/// (<see cref="IrLiveness"/>) jest dokładna także w pętlach i przy <c>goto</c>. Przydział zachłanny po wadze (odwołania, w pętli
/// ×8 na poziom); komórki, których przedziały się nie przecinają, dzielą rejestr. Komórka 1-bajtowa dostaje jeden rejestr,
/// 2-bajtowa parę (A i HL należą do prymitywów ISA).</summary>
internal static class RegisterAllocator
{
    /// <summary>Wybiera rejestry komórek modułu.</summary>
    /// <param name="module">Moduł po legalizacji (i po <see cref="ParamAlias"/>).</param>
    /// <param name="isa">Prymitywy celu: rejestry i pary dostępne dla komórek.</param>
    /// <returns>Symbol komórki → rejestr (<c>c</c>) albo para (<c>bc</c>, starszy pierwszy), jak w <see cref="ByteIsa.AssignRegisters"/>.</returns>
    public static Dictionary<string, string> Run(Ir.Module module, ByteIsa isa)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(isa);
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        var owner = new Dictionary<string, int>(StringComparer.Ordinal);
        var extent = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int f = 0; f < module.Functions.Count; f++)
        {
            Ir.Function function = module.Functions[f];
            blocked.UnionWith(function.Saved.Select(static o => IrLiveness.BaseSymbol(o.Sym)));
            blocked.UnionWith(function.Params.Select(static p => IrLiveness.BaseSymbol(p.Sym)));
            foreach (Ir.Ins ins in function.Body)
            {
                foreach (Ir.Op op in IrFacts.Operands(ins))
                {
                    if (op is Ir.AddrOf address)
                    {
                        blocked.Add(IrLiveness.BaseSymbol(address.Sym));
                    }
                    else if (op is Ir.Cell cell)
                    {
                        string key = IrLiveness.BaseSymbol(cell.Sym);
                        if (owner.TryGetValue(key, out int other) && other != f)
                        {
                            blocked.Add(key);
                        }

                        owner[key] = f;
                        extent[key] = Math.Max(extent.GetValueOrDefault(key), Offset(cell.Sym) + cell.W);
                    }
                }

                if (ins is Ir.LoadIdx or Ir.StoreIdx)
                {
                    blocked.Add(IrLiveness.BaseSymbol(ins is Ir.LoadIdx load ? load.Sym : ((Ir.StoreIdx)ins).Sym));
                }
            }
        }

        blocked.UnionWith(module.Data.Where(static d => d.Init is not null).SelectMany(static d => d.Init!.OfType<Ir.SymWord>()).Select(static w => IrLiveness.BaseSymbol(w.Sym)));
        blocked.UnionWith(module.ExternCells.Select(IrLiveness.BaseSymbol));
        if (module.Volatile is not null)
        {
            blocked.UnionWith(module.Volatile.Select(IrLiveness.BaseSymbol));
        }

        var candidates = new Dictionary<int, List<string>>();
        foreach (Ir.Data data in module.Data)
        {
            if (data.Segment == "BSS" && !data.Exported && data.Sym.Length > 0 && !blocked.Contains(data.Sym)
                && owner.TryGetValue(data.Sym, out int f) && extent[data.Sym] is >= 1 and <= 2 && extent[data.Sym] <= data.Size)
            {
                if (!candidates.TryGetValue(f, out List<string>? list))
                {
                    candidates[f] = list = [];
                }

                list.Add(data.Sym);
            }
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((int f, List<string> cells) in candidates)
        {
            foreach ((string sym, string registers) in Allocate(module.Functions[f], cells, extent, isa))
            {
                map[sym] = registers;
            }
        }

        return map;
    }

    /// <summary>Przesunięcie bajtu w symbolu komórki (<c>x+1</c> → 1, <c>x+4+2</c> → 6).</summary>
    private static int Offset(string sym)
    {
        // WideLegalizer składa przesunięcia (x+4+2)
        return sym.Split('+').Skip(1).Sum(static part => int.Parse(part, System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Przydział w jednej funkcji: odrzuca kandydatów żywych na wejściu i za wołaniem, buduje interferencję (komórka
    /// żywa za zapisem innej) i koloruje zachłannie po wadze.</summary>
    private static Dictionary<string, string> Allocate(Ir.Function function, List<string> cells, Dictionary<string, int> extent, ByteIsa isa)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        IReadOnlyList<Ir.Ins> body = function.Body;
        if (body.Count == 0)
        {
            return result;
        }

        var live = IrLiveness.Of(body, sym => extent.GetValueOrDefault(sym));
        var pool = new HashSet<string>(cells, StringComparer.Ordinal);
        pool.ExceptWith(live.LiveIn(0));
        for (int i = 0; i < body.Count; i++)
        {
            if (body[i] is Ir.Call)
            {
                pool.ExceptWith(live.LiveOut(i));
            }
        }

        long[] depth = IrFacts.LoopWeights(body);
        var weight = new Dictionary<string, long>(StringComparer.Ordinal);
        var edges = pool.ToDictionary(static s => s, static _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        for (int i = 0; i < body.Count; i++)
        {
            foreach (Ir.Cell cell in IrFacts.Operands(body[i]).OfType<Ir.Cell>())
            {
                string key = IrLiveness.BaseSymbol(cell.Sym);
                if (pool.Contains(key))
                {
                    weight[key] = weight.GetValueOrDefault(key) + depth[i];
                }
            }

            if (IrFacts.Def(body[i]) is { } def && pool.Contains(IrLiveness.BaseSymbol(def.Sym)))
            {
                string written = IrLiveness.BaseSymbol(def.Sym);
                foreach (string other in live.LiveOut(i).Where(s => s != written && pool.Contains(s)))
                {
                    edges[written].Add(other);
                    edges[other].Add(written);
                }
            }
        }

        foreach (string sym in pool.OrderByDescending(s => weight.GetValueOrDefault(s)).ThenBy(static s => s, StringComparer.Ordinal))
        {
            var taken = new HashSet<char>(edges[sym].Where(result.ContainsKey).SelectMany(n => result[n]));
            string? choice = (extent[sym] == 1 ? isa.CellRegisters : isa.CellPairs).FirstOrDefault(r => !r.Any(taken.Contains));
            if (choice is not null)
            {
                result[sym] = choice;
            }
        }

        return result;
    }
}
