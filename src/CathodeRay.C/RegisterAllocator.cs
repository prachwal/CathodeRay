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
        (HashSet<string> blocked, Dictionary<string, int> owner, Dictionary<string, int> extent, Dictionary<string, int> size, _, _) = Analyze(module);
        Dictionary<string, string> map0 = Allocate(module, isa).Map;
        Dictionary<int, Dictionary<string, List<(int Start, int End)>>> plan = PlanSplits(module, map0, blocked, owner, extent, size);
        Ir.Module split = module;
        Dictionary<string, string> map;
        Dictionary<Ir.Call, List<string>> saves;
        if (plan.Count == 0)
        {
            (map, saves) = Allocate(module, isa);
        }
        else
        {
            Ir.Module tried = BuildSplit(module, size, plan, static (_, _) => true);
            Dictionary<string, string> map1 = Allocate(tried, isa).Map;
            bool Keep(int fi, string b)
            {
                List<(int Start, int End)> tails = plan[fi][b];
                for (int t = 0; t < tails.Count; t++)
                {
                    if (!map1.ContainsKey($"{b}_s{t + 1}"))
                    {
                        return false;
                    }
                }

                return true;
            }

            split = BuildSplit(module, size, plan, Keep);
            (map, saves) = Allocate(split, isa);
        }

        isa.Cells.AssignRegisters(map, saves.Select(static p => (p.Key, (IReadOnlyList<string>)p.Value)));
        return split with
        {
            Functions = [.. split.Functions.Select(f => f.Saved.Any(o => map.ContainsKey(o.Sym)) ? f with { Saved = [.. f.Saved.Where(o => !map.ContainsKey(o.Sym))] } : f)],
        };
    }

    /// <summary>Raport przyczyn: które komórki BSS modułu zostają w pamięci i dlaczego (plan 43, poz. 1).
    /// Nie zmienia przydziału — tylko klasyfikuje wynik <see cref="Allocate"/>.</summary>
    /// <param name="module">Moduł po legalizacji (i po <see cref="ParamAlias"/>).</param>
    /// <param name="isa">Prymitywy celu.</param>
    /// <returns>Nazwa funkcji → (symbol komórki, przyczyna): <c>register</c> albo przyczyna
    /// (<c>multi</c>, <c>addrof</c>, <c>loadidx</c>, <c>volatile</c>, <c>extern</c>, <c>init</c>,
    /// <c>saved-aggregate</c>, <c>exported</c>, <c>wide</c>, <c>param-alias</c>, <c>no-color</c>, <c>unprofitable</c>).</returns>
    public static Dictionary<string, List<(string Sym, string Cause)>> Report(Ir.Module module, ByteIsa isa)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(isa);
        (_, _, Dictionary<string, int> extent, _, Dictionary<string, string> reason, _) = Analyze(module);
        var rejects = new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, string> map = Allocate(module, isa, rejects).Map;
        var dataSyms = new HashSet<string>(module.Data.Select(static d => d.Sym), StringComparer.Ordinal);
        var exported = new HashSet<string>(module.Data.Where(static d => d.Exported).Select(static d => d.Sym), StringComparer.Ordinal);
        var dataSize = module.Data.ToDictionary(static d => d.Sym, static d => d.Size, StringComparer.Ordinal);
        var report = new Dictionary<string, List<(string Sym, string Cause)>>(StringComparer.Ordinal);
        for (int f = 0; f < module.Functions.Count; f++)
        {
            Ir.Function function = module.Functions[f];
            var paramSyms = new HashSet<string>(function.Params.Select(static p => IrLiveness.BaseSymbol(p.Sym)), StringComparer.Ordinal);
            var live0 = IrLiveness.Of(function.Body, s => extent.GetValueOrDefault(s, 0)).LiveIn(0);
            var rows = new List<(string Sym, string Cause)>();
            foreach (string sym in Symbols(function.Body).Order(StringComparer.Ordinal))
            {
                if (!dataSyms.Contains(sym))
                {
                    if (paramSyms.Contains(sym))
                    {
                        rows.Add((sym, "param-alias"));
                    }

                    continue;
                }

                if (map.TryGetValue(sym, out _))
                {
                    rows.Add((sym, "register"));
                }
                else if (reason.TryGetValue(sym, out string? cause))
                {
                    rows.Add((sym, cause));
                }
                else if (exported.Contains(sym))
                {
                    rows.Add((sym, "exported"));
                }
                else if (extent.GetValueOrDefault(sym) > 2)
                {
                    rows.Add((sym, "wide"));
                }
                else if (extent.GetValueOrDefault(sym) > dataSize.GetValueOrDefault(sym))
                {
                    rows.Add((sym, "size-overflow"));
                }
                else if (live0.Contains(sym) && !paramSyms.Contains(sym))
                {
                    rows.Add((sym, "live-on-entry"));
                }
                else if (rejects.TryGetValue(sym, out string? reject))
                {
                    rows.Add((sym, reject));
                }
                else
                {
                    rows.Add((sym, "other"));
                }
            }

            report[function.Name] = rows;
        }

        return report;
    }

    /// <summary>Plan dzielenia przedziałów: dla każdej funkcji symbole-kandydatów, które po zwykłym przydziale zostały
    /// w pamięci i mają więcej niż jeden rozłączny przedział żywotności. Zwraca per funkcja: symbol → przedziały do
    /// wydzielenia (bez pierwszego).</summary>
    private static Dictionary<int, Dictionary<string, List<(int Start, int End)>>> PlanSplits(
        Ir.Module module, IReadOnlyDictionary<string, string> map, HashSet<string> blocked, Dictionary<string, int> owner, Dictionary<string, int> extent, Dictionary<string, int> size)
    {
        var plan = new Dictionary<int, Dictionary<string, List<(int Start, int End)>>>();
        for (int fi = 0; fi < module.Functions.Count; fi++)
        {
            Ir.Function function = module.Functions[fi];
            IReadOnlyList<Ir.Ins> body = function.Body;
            var live = IrLiveness.Of(body, s => size.GetValueOrDefault(s, 0));
            var paramSyms = new HashSet<string>(function.Params.Select(static p => IrLiveness.BaseSymbol(p.Sym)), StringComparer.Ordinal);
            var savedSyms = new HashSet<string>(function.Saved.Select(static o => IrLiveness.BaseSymbol(o.Sym)), StringComparer.Ordinal);
            Dictionary<string, List<(int Start, int End)>>? functionPlan = null;
            foreach (string sym in Symbols(body))
            {
                if (map.ContainsKey(sym) || extent.GetValueOrDefault(sym) is < 1 or > 2 || !size.ContainsKey(sym)
                    || !owner.TryGetValue(sym, out int o) || o != fi
                    || paramSyms.Contains(sym) || savedSyms.Contains(sym) || blocked.Contains(sym))
                {
                    continue;
                }

                List<(int Start, int End)> runs = Runs(live, body.Count, sym);
                if (runs.Count < 2 || FlowsBetweenRuns(live, runs, sym))
                {
                    continue;
                }

                (functionPlan ??= [])[sym] = runs.GetRange(1, runs.Count - 1);
            }

            if (functionPlan is not null)
            {
                plan[fi] = functionPlan;
            }
        }

        return plan;
    }

    /// <summary>Buduje moduł z wydzielonymi przedziałami wg planu, o ile <paramref name="keep"/> pozwala na dany symbol
    /// (użyte do odrzucenia splitu, gdy któryś potomny symbol nie dostał rejestru).</summary>
    private static Ir.Module BuildSplit(Ir.Module module, Dictionary<string, int> size, Dictionary<int, Dictionary<string, List<(int Start, int End)>>> plan, Func<int, string, bool> keep)
    {
        var data = new List<Ir.Data>(module.Data);
        var functions = new List<Ir.Function>(module.Functions.Count);
        for (int fi = 0; fi < module.Functions.Count; fi++)
        {
            Ir.Function function = module.Functions[fi];
            IReadOnlyList<Ir.Ins> body = function.Body;
            var aliases = new Dictionary<int, Dictionary<string, string>>();
            if (plan.TryGetValue(fi, out Dictionary<string, List<(int Start, int End)>>? functionPlan))
            {
                foreach ((string sym, List<(int Start, int End)> tails) in functionPlan)
                {
                    if (!keep(fi, sym))
                    {
                        continue;
                    }

                    for (int t = 0; t < tails.Count; t++)
                    {
                        string nsym = $"{sym}_s{t + 1}";
                        data.Add(new Ir.Data(nsym, "BSS", size.GetValueOrDefault(sym), null, false));
                        for (int i = tails[t].Start; i <= tails[t].End; i++)
                        {
                            if (!aliases.TryGetValue(i, out Dictionary<string, string>? alias))
                            {
                                aliases[i] = alias = new Dictionary<string, string>(StringComparer.Ordinal);
                            }

                            alias[sym] = nsym;
                        }
                    }
                }
            }

            if (aliases.Count == 0)
            {
                functions.Add(function);
                continue;
            }

            var rewritten = new List<Ir.Ins>(body.Count);
            for (int i = 0; i < body.Count; i++)
            {
                rewritten.Add(aliases.TryGetValue(i, out Dictionary<string, string>? alias) ? Rename(body[i], alias) : body[i]);
            }

            functions.Add(function with { Body = rewritten });
        }

        return module with { Functions = functions, Data = data };
    }

    /// <summary>Symbole bazowe komórek użyte w ciele funkcji.</summary>
    private static IEnumerable<string> Symbols(IReadOnlyList<Ir.Ins> body)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Ir.Ins ins in body)
        {
            foreach (Ir.Op op in IrFacts.Operands(ins))
            {
                string? sym = op switch
                {
                    Ir.Cell cell => IrLiveness.BaseSymbol(cell.Sym),
                    Ir.AddrOf address => IrLiveness.BaseSymbol(address.Sym),
                    _ => null,
                };
                if (sym is not null)
                {
                    seen.Add(sym);
                }
            }
        }

        return seen;
    }

    /// <summary>Rozłączne przedziały (start, koniec) instrukcji, w których symbol jest żywy.</summary>
    private static List<(int Start, int End)> Runs(IrLiveness live, int count, string sym)
    {
        var runs = new List<(int Start, int End)>();
        int start = -1;
        for (int i = 0; i < count; i++)
        {
            bool on = live.LiveIn(i).Contains(sym) || live.LiveOut(i).Contains(sym);
            if (on && start < 0)
            {
                start = i;
            }
            else if (!on && start >= 0)
            {
                runs.Add((start, i - 1));
                start = -1;
            }
        }

        if (start >= 0)
        {
            runs.Add((start, count - 1));
        }

        return runs;
    }

    /// <summary>Czy wartość symbolu płynie z jednego przedziału do innego po krawędzi grafu przepływu (skok wsteczny albo w przód).
    /// Przedziały są liczone po indeksach instrukcji, więc zakres żywy przez krawędź wsteczną (zapis na końcu pętli, odczyt w
    /// nagłówku, martwy pomiędzy) wygląda jak dwa rozłączne; dzielenie go odcięłoby zapis od odczytu.</summary>
    private static bool FlowsBetweenRuns(IrLiveness live, List<(int Start, int End)> runs, string sym)
    {
        int RunOf(int index)
        {
            for (int r = 0; r < runs.Count; r++)
            {
                if (index >= runs[r].Start && index <= runs[r].End)
                {
                    return r;
                }
            }

            return -1;
        }

        foreach ((int start, int end) in runs)
        {
            for (int i = start; i <= end; i++)
            {
                if (!live.LiveOut(i).Contains(sym))
                {
                    continue;
                }

                foreach (int next in live.Successors(i))
                {
                    if (live.LiveIn(next).Contains(sym) && RunOf(next) != RunOf(i))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Podmienia symbole komórek w instrukcji.</summary>
    private static Ir.Ins Rename(Ir.Ins ins, Dictionary<string, string> alias) => ins switch
    {
        Ir.Mov mov => new Ir.Mov(Rename(mov.Dst, alias), RenameOp(mov.Src, alias)),
        Ir.Bin bin => bin with { Dst = Rename(bin.Dst, alias), A = RenameOp(bin.A, alias), B = RenameOp(bin.B, alias) },
        Ir.Un un => un with { Dst = Rename(un.Dst, alias), A = RenameOp(un.A, alias) },
        Ir.Load load => load with { Dst = Rename(load.Dst, alias), Ptr = RenameOp(load.Ptr, alias) },
        Ir.Store store => store with { Ptr = RenameOp(store.Ptr, alias), Value = RenameOp(store.Value, alias) },
        Ir.LoadIdx load => load with { Dst = Rename(load.Dst, alias), Index = Rename(load.Index, alias) },
        Ir.StoreIdx store => store with { Index = Rename(store.Index, alias), Value = RenameOp(store.Value, alias) },
        Ir.CopyBlock copy => copy with { Dst = RenameOp(copy.Dst, alias), Src = RenameOp(copy.Src, alias) },
        Ir.Fill fill => fill with { Dst = RenameOp(fill.Dst, alias) },
        Ir.BrCmp branch => branch with { A = RenameOp(branch.A, alias), B = RenameOp(branch.B, alias) },
        Ir.Ret ret => ret with { Value = ret.Value is null ? null : RenameOp(ret.Value, alias) },
        Ir.Call call => call with
        {
            Indirect = call.Indirect is null ? null : Rename(call.Indirect, alias),
            Args = [.. call.Args.Select(a => RenameOp(a, alias))],
            Result = call.Result is null ? null : Rename(call.Result, alias),
        },
        _ => ins,
    };

    private static Ir.Cell Rename(Ir.Cell cell, Dictionary<string, string> alias) =>
        alias.TryGetValue(cell.Sym, out string? sym) ? cell with { Sym = sym } : cell;

    private static Ir.Op RenameOp(Ir.Op op, Dictionary<string, string> alias) => op is Ir.Cell cell ? Rename(cell, alias) : op;

    /// <summary>Analiza modułu dla przydziału: symbole zablokowane, właściciel (jedna funkcja), zakres bajtów i rozmiar obiektu.</summary>
    /// <param name="module">Moduł.</param>
    /// <returns>Zablokowane, właściciele, zakresy, rozmiary, przyczyny blokad (pierwsza wygrywa) i symbole współdzielone.</returns>
    private static (HashSet<string> Blocked, Dictionary<string, int> Owner, Dictionary<string, int> Extent, Dictionary<string, int> Size, Dictionary<string, string> Reason, HashSet<string> Multi) Analyze(Ir.Module module)
    {
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        var reason = new Dictionary<string, string>(StringComparer.Ordinal);
        void Block(string sym, string cause)
        {
            if (blocked.Add(sym))
            {
                reason[sym] = cause;
            }
        }

        var owner = new Dictionary<string, int>(StringComparer.Ordinal);
        var multi = new HashSet<string>(StringComparer.Ordinal);
        var extent = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int f = 0; f < module.Functions.Count; f++)
        {
            Ir.Function function = module.Functions[f];
            foreach (Ir.Owned saved in function.Saved.Where(static o => o.Aggregate))
            {
                Block(IrLiveness.BaseSymbol(saved.Sym), "saved-aggregate");
            }

            foreach (Ir.Ins ins in function.Body)
            {
                foreach (Ir.Op op in IrFacts.Operands(ins))
                {
                    if (op is Ir.AddrOf address)
                    {
                        Block(IrLiveness.BaseSymbol(address.Sym), "addrof");
                    }
                    else if (op is Ir.Cell cell)
                    {
                        string key = IrLiveness.BaseSymbol(cell.Sym);
                        if (owner.TryGetValue(key, out int other) && other != f)
                        {
                            Block(key, "multi");
                            multi.Add(key);
                        }

                        owner[key] = f;
                        extent[key] = Math.Max(extent.GetValueOrDefault(key), Offset(cell.Sym) + cell.W);
                    }
                }

                if (ins is Ir.LoadIdx or Ir.StoreIdx)
                {
                    Block(IrLiveness.BaseSymbol(ins is Ir.LoadIdx load ? load.Sym : ((Ir.StoreIdx)ins).Sym), "loadidx");
                }
            }
        }

        var size = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Ir.Data data in module.Data)
        {
            if (data.Sym.Length > 0)
            {
                size[data.Sym] = Math.Max(size.GetValueOrDefault(data.Sym), data.Size);
                if (data.Init is not null)
                {
                    foreach (Ir.SymWord word in data.Init.OfType<Ir.SymWord>())
                    {
                        Block(IrLiveness.BaseSymbol(word.Sym), "init");
                    }
                }
            }
        }

        foreach (string sym in module.ExternCells.Select(IrLiveness.BaseSymbol))
        {
            Block(sym, "extern");
        }

        if (module.Volatile is not null)
        {
            foreach (string sym in module.Volatile.Select(IrLiveness.BaseSymbol))
            {
                Block(sym, "volatile");
            }
        }

        return (blocked, owner, extent, size, reason, multi);
    }

    private static (Dictionary<string, string> Map, Dictionary<Ir.Call, List<string>> Saves) Allocate(Ir.Module module, ByteIsa isa, Dictionary<string, string>? rejects = null)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(isa);
        (HashSet<string> blocked, Dictionary<string, int> owner, Dictionary<string, int> extent, _, _, _) = Analyze(module);

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

        // Pre-kolor liczników pętli (plan 43, poz. 6): tylko cele z ICountLoop (Z80).
        // Licznik wymuszony do "b" przechodzi normalną ścieżkę (zysk, SavedAround),
        // więc wołania w ciele dostają push/pop pary za darmo; odrzucenie przez zysk
        // zostawia starą emisję (selektory wycofują się bez "b" w domu).
        var precolor = new Dictionary<int, HashSet<string>>();
        if (isa is ICountLoop)
        {
            for (int f = 0; f < module.Functions.Count; f++)
            {
                Ir.Function function = module.Functions[f];
                for (int i = 0; i < function.Body.Count; i++)
                {
                    if (function.Body[i] is Ir.Label && ByteSelector.MatchCountLoop(function, i) is { } match)
                    {
                        (precolor.TryGetValue(f, out HashSet<string>? set) ? set : precolor[f] = []).Add(match.Counter);
                    }
                }
            }
        }

        if (precolor.Count == 0)
        {
            RunColor(null, null, map, saves);
            return (map, saves);
        }

        // Pre-kolor nie może nikogo eksmitować do pamięci względem świata bez licznika
        // (licznik w pamięci jak przy aliasie): funkcje ze stratą wracają do zwykłego
        // przydziału z licznikiem w puli (wtedy też brak djnz — stara emisja).
        var excluded = precolor.Values.SelectMany(static s => s).ToHashSet(StringComparer.Ordinal);
        var baseline = new Dictionary<string, string>(StringComparer.Ordinal);
        RunColor(null, excluded, baseline, new Dictionary<Ir.Call, List<string>>(ReferenceEqualityComparer.Instance));
        var natural = new Dictionary<string, string>(StringComparer.Ordinal);
        var naturalSaves = new Dictionary<Ir.Call, List<string>>(ReferenceEqualityComparer.Instance);
        RunColor(null, null, natural, naturalSaves);
        RunColor(precolor, null, map, saves);
        System.Console.Error.WriteLine("DBG natural=" + string.Join(",", natural.Select(kv => kv.Key + ">" + kv.Value)) + " pre=" + string.Join(",", map.Select(kv => kv.Key + ">" + kv.Value)));
        var callFunc = new Dictionary<Ir.Call, int>(ReferenceEqualityComparer.Instance);
        for (int f = 0; f < module.Functions.Count; f++)
        {
            foreach (Ir.Call call in module.Functions[f].Body.OfType<Ir.Call>())
            {
                callFunc[call] = f;
            }
        }

        var dropped = new HashSet<int>();
        System.Console.Error.WriteLine("DBG precolor=" + string.Join(";", precolor.Select(kv => kv.Key + ":" + string.Join(",", kv.Value))));
        foreach ((int f, _) in precolor)
        {
            foreach ((string sym, _) in baseline)
            {
                if (owner.GetValueOrDefault(sym, -1) == f && !map.ContainsKey(sym))
                {
                    dropped.Add(f);
                    break;
                }
            }
        }

        foreach ((int f, _) in precolor)
        {
            if (!dropped.Contains(f))
            {
                continue;
            }

            foreach (string sym in map.Keys.Where(s => owner.GetValueOrDefault(s, -1) == f).ToList())
            {
                map.Remove(sym);
            }

            foreach ((string sym, string registers) in natural)
            {
                if (owner.GetValueOrDefault(sym, -1) == f)
                {
                    map[sym] = registers;
                }
            }
        }

        foreach ((Ir.Call call, List<string> pairs) in naturalSaves)
        {
            if (dropped.Contains(callFunc.GetValueOrDefault(call, -1)))
            {
                saves[call] = pairs;
            }
        }

        foreach (Ir.Call call in saves.Keys.Where(c => dropped.Contains(callFunc.GetValueOrDefault(c, -1)) && !naturalSaves.ContainsKey(c)).ToList())
        {
            saves.Remove(call);
        }

        System.Console.Error.WriteLine("DBG dropped=" + string.Join(",", dropped) + " map=" + string.Join(",", map.Select(kv => kv.Key + ">" + kv.Value)));
        return (map, saves);

        void RunColor(Dictionary<int, HashSet<string>>? pre, HashSet<string>? excluded, Dictionary<string, string> intoMap, Dictionary<Ir.Call, List<string>> intoSaves)
        {
            foreach ((int f, List<string> cells) in candidates)
            {
                List<string> run = excluded is null ? cells : [.. cells.Where(c => !excluded.Contains(c))];
                foreach ((string sym, string registers) in Color(module.Functions[f], run, extent, isa, intoSaves, rejects, pre?.GetValueOrDefault(f)))
                {
                    intoMap[sym] = registers;
                }
            }
        }
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
    private static Dictionary<string, string> Color(Ir.Function function, List<string> cells, Dictionary<string, int> extent, ByteIsa isa, Dictionary<Ir.Call, List<string>> saves, Dictionary<string, string>? rejects = null, IReadOnlySet<string>? precolored = null)
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
        System.Console.Error.WriteLine("DBG order " + function.Name + ": " + string.Join(",", pool.OrderByDescending(s => weight.GetValueOrDefault(s)).ThenBy(static s => s, StringComparer.Ordinal).Select(s => s + "=" + weight.GetValueOrDefault(s))));
        var saved = new Dictionary<int, HashSet<string>>();
        foreach (string sym in pool
            .OrderByDescending(s => precolored is not null && precolored.Contains(s))
            .ThenByDescending(s => weight.GetValueOrDefault(s))
            .ThenBy(static s => s, StringComparer.Ordinal))
        {
            var taken = new HashSet<char>(edges[sym].Where(result.ContainsKey).SelectMany(n => result[n]));
            string? choice = null;
            int fresh = int.MaxValue;
            IEnumerable<string> options = precolored is not null && precolored.Contains(sym) && extent[sym] == 1
                && isa.CellRegisters.Contains("b")
                ? ["b"]
                : extent[sym] == 1 ? isa.CellRegisters : isa.CellPairs;
            foreach (string registers in options.Where(r => !r.Any(taken.Contains)))
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
                rejects?.Add(sym, choice is null ? "no-color" : "unprofitable");
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
