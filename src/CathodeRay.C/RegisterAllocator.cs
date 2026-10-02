namespace CathodeRay.C;

/// <summary>Przydział rejestrów komórkom celów z mapą rejestrów w ISA (<see cref="ByteIsa.CellRegisters"/>, <see cref="ByteIsa.CellPairs"/>:
/// Z80 i 8080 — B, C, D, E; konwencja caller-saved: wołany niszczy wszystko). Kandydat
/// to komórka BSS modułu o zasięgu odwołań 1 albo 2 bajty, której symbol występuje w dokładnie jednej funkcji (nie przedrostek
/// nazwy: tymczasowe <c>__lg*</c>/<c>__wl*</c> i komórki z inline'owania bywają wspólne), nie jest żywa na wejściu funkcji (wartość
/// z poprzedniej aktywacji, np. lokalnej <c>static</c>, nigdy nie jest czytana), nie jest wzięta adresem, <c>volatile</c>,
/// agregatem, parametrem, zewnętrzna ani eksportowana. Komórka żywa za wołaniem (<see cref="Ir.Call"/>, także pośrednim i procedurą
/// wykonawczą wstawioną przez legalizację) dostaje rejestr tylko z zyskiem netto: jej para idzie na stos wokół takiego wołania
/// (<see cref="CellMap.SavedAround"/>: 2 B i ok. 21 T na parę i wołanie, razy waga pętli), a zysk to odwołania w rejestrze i, dla
/// komórki z <see cref="Ir.Function.Saved"/>, zniknięcie jej zapisu w ramce (push/pop wokół wołania zachowuje wartość jak ramka). Żywość na grafie przepływu
/// (<see cref="IrLiveness"/>) jest dokładna także w pętlach i przy <c>goto</c>. Przydział zachłanny po wadze (odwołania, w pętli
/// ×8 na poziom); komórki, których przedziały się nie przecinają, dzielą rejestr. Komórka 1-bajtowa dostaje jeden rejestr,
/// 2-bajtowa parę (A i HL należą do prymitywów ISA).</summary>
internal static class RegisterAllocator
{
    /// <summary>Wybiera rejestry komórek modułu.</summary>
    /// <param name="module">Moduł po legalizacji (i po <see cref="ParamAlias"/>).</param>
    /// <param name="isa">Prymitywy celu: rejestry i pary dostępne dla komórek.</param>
    /// <returns>Symbol komórki → rejestr (<c>c</c>) albo para (<c>bc</c>, starszy pierwszy), jak w <see cref="CellMap.AssignRegisters"/>.</returns>
    public static Dictionary<string, string> Run(Ir.Module module, ByteIsa isa) => Allocate(module, isa).Map;

    /// <summary>Przydział rejestrów dla selektora: przekazuje ISA mapę i pary zapisywane wokół wołań, a z
    /// <see cref="Ir.Function.Saved"/> usuwa komórki w rejestrach (zachowuje je push/pop wokół wołania).</summary>
    /// <param name="module">Moduł po legalizacji (i po <see cref="ParamAlias"/>).</param>
    /// <param name="isa">Prymitywy celu.</param>
    /// <returns>Moduł dla selektora (te same instrukcje).</returns>
    public static Ir.Module Tune(Ir.Module module, ByteIsa isa)
    {
        (Dictionary<string, string> map, Dictionary<Ir.Call, List<string>> saves) = Allocate(module, isa);
        isa.Cells.AssignRegisters(map, saves.Select(static p => (p.Key, (IReadOnlyList<string>)p.Value)));
        return module with
        {
            Functions = [.. module.Functions.Select(f => f.Saved.Any(o => map.ContainsKey(o.Sym)) ? f with { Saved = [.. f.Saved.Where(o => !map.ContainsKey(o.Sym))] } : f)],
        };
    }

    private static (Dictionary<string, string> Map, Dictionary<Ir.Call, List<string>> Saves) Allocate(Ir.Module module, ByteIsa isa)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(isa);
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        var owner = new Dictionary<string, int>(StringComparer.Ordinal);
        var extent = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int f = 0; f < module.Functions.Count; f++)
        {
            Ir.Function function = module.Functions[f];
            blocked.UnionWith(function.Saved.Where(static o => o.Aggregate).Select(static o => IrLiveness.BaseSymbol(o.Sym)));
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
        var saves = new Dictionary<Ir.Call, List<string>>(ReferenceEqualityComparer.Instance);
        foreach ((int f, List<string> cells) in candidates)
        {
            foreach ((string sym, string registers) in Color(module.Functions[f], cells, extent, isa, saves))
            {
                map[sym] = registers;
            }
        }

        return (map, saves);
    }

    /// <summary>Przesunięcie bajtu w symbolu komórki (<c>x+1</c> → 1, <c>x+4+2</c> → 6).</summary>
    private static int Offset(string sym)
    {
        // WideLegalizer składa przesunięcia (x+4+2)
        return sym.Split('+').Skip(1).Sum(static part => int.Parse(part, System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Przydział w jednej funkcji: odrzuca kandydatów żywych na wejściu, buduje interferencję (komórka żywa za zapisem
    /// innej) i koloruje zachłannie po wadze. Komórce żywej za wołaniami wybiera rejestr, którego para jest już zapisywana przy
    /// największej liczbie tych wołań, i przydziela go tylko, gdy zysk w bajtach i w taktach przewyższa koszt nowych push/pop.
    /// Pary do zapisania wokół wołań (w kolejności <see cref="ByteIsa.CellPairs"/>) dopisuje do <paramref name="saves"/>.</summary>
    private static Dictionary<string, string> Color(Ir.Function function, List<string> cells, Dictionary<string, int> extent, ByteIsa isa, Dictionary<Ir.Call, List<string>> saves)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        IReadOnlyList<Ir.Ins> body = function.Body;
        if (body.Count == 0)
        {
            return result;
        }

        int Size(string sym) => extent.GetValueOrDefault(sym);
        var live = IrLiveness.Of(body, Size);
        var pool = new HashSet<string>(cells, StringComparer.Ordinal);

        // Parametr jest „żywy na wejściu", ale jego wartość wstawia prolog (kopia z cc_argN do rejestru), więc
        // zostaje w puli; inne komórki żywe na wejściu (np. lokalna static) odpadają — ich stanu nikt nie odtworzy.
        var paramSyms = new HashSet<string>(function.Params.Select(static p => IrLiveness.BaseSymbol(p.Sym)), StringComparer.Ordinal);
        pool.ExceptWith(live.LiveIn(0).Where(s => !paramSyms.Contains(s)));
        var crossings = pool.ToDictionary(static s => s, static _ => new List<int>(), StringComparer.Ordinal);
        for (int i = 0; i < body.Count; i++)
        {
            if (body[i] is Ir.Call call)
            {
                string? killed = IrLiveness.Killed(call, Size);
                foreach (string sym in live.LiveOut(i).Where(s => s != killed && pool.Contains(s)))
                {
                    crossings[sym].Add(i);
                }
            }
        }

        long[] depth = IrFacts.LoopWeights(body);
        var weight = new Dictionary<string, long>(StringComparer.Ordinal);
        var uses = new Dictionary<string, int>(StringComparer.Ordinal);
        var edges = pool.ToDictionary(static s => s, static _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        for (int i = 0; i < body.Count; i++)
        {
            foreach (Ir.Cell cell in IrFacts.Operands(body[i]).OfType<Ir.Cell>())
            {
                string key = IrLiveness.BaseSymbol(cell.Sym);
                if (pool.Contains(key))
                {
                    weight[key] = weight.GetValueOrDefault(key) + depth[i];
                    uses[key] = uses.GetValueOrDefault(key) + 1;
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

        // Parametry są żywe na wejściu, a brak zapisu w IR nie tworzy krawędzi — dodaj klikę, by nie dzieliły rejestru.
        var liveParams = pool.Where(paramSyms.Contains).ToList();
        foreach (string p in liveParams)
        {
            foreach (string q in liveParams.Where(q => q != p))
            {
                edges[p].Add(q);
            }
        }

        // pary zapisywane wokół wołań tej funkcji (indeks instrukcji → pary)
        var saved = new Dictionary<int, HashSet<string>>();
        foreach (string sym in pool.OrderByDescending(s => weight.GetValueOrDefault(s)).ThenBy(static s => s, StringComparer.Ordinal))
        {
            var taken = new HashSet<char>(edges[sym].Where(result.ContainsKey).SelectMany(n => result[n]));
            string? choice = null;
            int fresh = int.MaxValue;
            foreach (string registers in (extent[sym] == 1 ? isa.CellRegisters : isa.CellPairs).Where(r => !r.Any(taken.Contains)))
            {
                string pair = isa.CellPairs.First(p => registers.All(p.Contains));
                int count = crossings[sym].Count(i => !saved.TryGetValue(i, out HashSet<string>? pairs) || !pairs.Contains(pair));
                if (count < fresh)
                {
                    (choice, fresh) = (registers, count);
                }
            }

            if (choice is null || (fresh > 0 && !Profitable(function, sym, extent[sym], uses[sym], weight[sym], choice, crossings[sym], saved, isa, depth)))
            {
                continue;
            }

            result[sym] = choice;
            string chosen = isa.CellPairs.First(p => choice.All(p.Contains));
            foreach (int i in crossings[sym])
            {
                if (!saved.TryGetValue(i, out HashSet<string>? pairs))
                {
                    saved[i] = pairs = new HashSet<string>(StringComparer.Ordinal);
                }

                pairs.Add(chosen);
            }
        }

        foreach ((int i, HashSet<string> pairs) in saved)
        {
            // ta sama instancja wołania w dwóch funkcjach (po inliningu): suma par, nadmiarowy push/pop jest nieszkodliwy
            var call = (Ir.Call)body[i];
            List<string> old = saves.GetValueOrDefault(call) ?? [];
            saves[call] = [.. isa.CellPairs.Where(p => pairs.Contains(p) || old.Contains(p))];
        }

        return result;
    }

    /// <summary>Zysk z rejestru dla komórki żywej za wołaniami: odwołanie w rejestrze zamiast w pamięci to ok. 2 B (bajt) albo 1 B
    /// (słowo przez HL) i ok. 9 T razy waga; komórka z <see cref="Ir.Function.Saved"/> oszczędza też zapis w ramce (na bajt 8 B i
    /// ok. 47 T na aktywację). Koszt: 2 B i ok. 21 T razy waga wołania za każde wołanie, przy którym para nie jest jeszcze zapisywana.</summary>
    private static bool Profitable(Ir.Function function, string sym, int size, int uses, long weight, string registers, List<int> crossings, Dictionary<int, HashSet<string>> saved, ByteIsa isa, long[] depth)
    {
        string pair = isa.CellPairs.First(p => registers.All(p.Contains));
        List<int> fresh = [.. crossings.Where(i => !saved.TryGetValue(i, out HashSet<string>? pairs) || !pairs.Contains(pair))];
        bool framed = function.Saved.Any(o => o.Sym == sym);
        long bytes = ((size == 1 ? 2L : 1L) * uses) + (framed ? 8L * size : 0) - (2L * fresh.Count);
        long cycles = (9L * weight) + (framed ? 47L * size : 0) - (21L * fresh.Sum(i => depth[i]));
        return bytes > 0 && cycles > 0;
    }
}
