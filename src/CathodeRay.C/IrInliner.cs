namespace CathodeRay.C;

/// <summary>Inlining małych funkcji liści na poziomie IR (przed wyrocznią i celami, więc wszystkie widzą ten sam kod). Kandydat: funkcja
/// bez wołań, bez ramki, którą wołamy z tego samego modułu po nazwie; mała (do <see cref="AlwaysLimit"/> instrukcji) albo <c>static</c>
/// z jednym miejscem wołania (do <see cref="SingleLimit"/>). Parametry i wynik przechodzą przez komórki wołanej funkcji (kopie <c>Mov</c>),
/// więc rozmiar i zachowanie jak przy zwykłym wywołaniu, a przebiegi IR skracają wynik. Nieużywane funkcje <c>static</c> znikają.</summary>
internal static class IrInliner
{
    private const int AlwaysLimit = 10;

    private const int SingleLimit = 40;

    /// <summary>Wstawia ciała kandydatów w miejsca wołań.</summary>
    /// <param name="module">Moduł po obniżeniu.</param>
    /// <returns>Moduł po inliningu.</returns>
    public static Ir.Module Run(Ir.Module module)
    {
        Dictionary<string, Ir.Function> byName = module.Functions.ToDictionary(static f => f.Name, StringComparer.Ordinal);
        var addressTaken = new HashSet<string>(StringComparer.Ordinal);
        foreach (Ir.Function function in module.Functions)
        {
            foreach (Ir.Ins ins in function.Body)
            {
                foreach (Ir.Op op in IrFacts.Uses(ins))
                {
                    if (op is Ir.AddrOf address)
                    {
                        addressTaken.Add(address.Sym);
                    }
                }
            }
        }

        foreach (Ir.SymWord word in module.Data.Where(static d => d.Init is not null).SelectMany(static d => d.Init!.OfType<Ir.SymWord>()))
        {
            addressTaken.Add(word.Sym);
        }

        var callSites = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Ir.Call call in module.Functions.SelectMany(static f => f.Body).OfType<Ir.Call>())
        {
            if (call.Direct is not null)
            {
                callSites[call.Direct] = callSites.GetValueOrDefault(call.Direct) + 1;
            }
        }

        var candidates = new Dictionary<string, Ir.Function>(StringComparer.Ordinal);
        foreach (Ir.Function function in module.Functions)
        {
            int size = function.Body.Count(static i => i is not (Ir.Src or Ir.Label));
            bool leaf = !function.Body.Any(static i => i is Ir.Call);
            bool small = size <= AlwaysLimit || (function.IsStatic && callSites.GetValueOrDefault(function.Name) == 1 && size <= SingleLimit);
            if (leaf && small && function.Saved.Count == 0 && !addressTaken.Contains(function.Name) && function.Name is not ("main" or "__cc_init"))
            {
                candidates[function.Name] = function;
            }
        }

        if (candidates.Count == 0)
        {
            return module;
        }

        int counter = 0;
        var functions = new List<Ir.Function>();
        var newData = new List<Ir.Data>();
        var dataBySym = module.Data.ToDictionary(static d => d.Sym, StringComparer.Ordinal);
        foreach (Ir.Function function in module.Functions)
        {
            var body = new List<Ir.Ins>();
            var inlined = new HashSet<string>(StringComparer.Ordinal);
            foreach (Ir.Ins ins in function.Body)
            {
                if (ins is Ir.Call { Direct: { } name } call && call.Indirect is null && candidates.TryGetValue(name, out Ir.Function? callee)
                    && name != function.Name && call.Args.Count == callee.Params.Count)
                {
                    Expand(call, callee, body, ++counter, dataBySym, newData);
                    inlined.Add(name);
                }
                else
                {
                    body.Add(ins);
                }
            }

            functions.Add(inlined.Count == 0 ? function : function with { Body = IrPasses.Optimize(body, function.Name, inlined, module.Volatile) });
        }

        // po inliningu usuń funkcje static, do których nikt już nie woła
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (Ir.Function function in functions)
        {
            foreach (Ir.Ins ins in function.Body)
            {
                if (ins is Ir.Call { Direct: { } direct })
                {
                    referenced.Add(direct);
                }

                foreach (Ir.Op op in IrFacts.Uses(ins))
                {
                    if (op is Ir.AddrOf address)
                    {
                        referenced.Add(address.Sym);
                    }
                }
            }
        }

        referenced.UnionWith(addressTaken);
        Ir.Function[] kept = [.. functions.Where(f => !f.IsStatic || f.Name == "__cc_init" || referenced.Contains(f.Name) || !candidates.ContainsKey(f.Name))];
        return module with { Functions = kept, Data = [.. module.Data, .. newData] };
    }

    private static void Expand(Ir.Call call, Ir.Function callee, List<Ir.Ins> output, int id, IReadOnlyDictionary<string, Ir.Data> dataBySym, List<Ir.Data> newData)
    {
        // Unikalne komórki per miejsce wstawienia (plan 43, poz. 3): parametry zawsze, reszta
        // tylko gdy kopia odblokowuje djnz (funkcja z pętlą licznikową) — inaczej split
        // fragmentuje stronę zerową/direct bez zysku (19_struct +24 B na 6502). Kopia nie dzieli
        // komórek z oryginałem, więc alokator ich nie blokuje jako multi-owner. Bez przyrostka
        // zostają: globale, statyczne (żywe na wejściu), napisy i dane z inicjalizatorem (współdzielone),
        // adresy brane wskaźnikiem i bloki (tablice/struktury przez LoadIdx/CopyBlock/Fill).
        var ren = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Ir.Cell param in callee.Params)
        {
            ren[param.Sym] = $"{param.Sym}__i{id}";
            newData.Add(new Ir.Data(ren[param.Sym], "BSS", param.W, null, false));
        }

        if (HasCountLoop(callee))
        {
            var sizes = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Ir.Data data in dataBySym.Values)
            {
                sizes[data.Sym] = data.Size;
            }

            foreach (Ir.Cell param in callee.Params)
            {
                sizes[param.Sym] = param.W;
            }

            var liveIn = IrLiveness.Of(callee.Body, s => sizes.GetValueOrDefault(s, 0)).LiveIn(0);
            var blockOps = new HashSet<string>(StringComparer.Ordinal);
            foreach (Ir.Ins scan in callee.Body)
            {
                if (scan is Ir.LoadIdx || scan is Ir.StoreIdx || scan is Ir.CopyBlock || scan is Ir.Fill)
                {
                    foreach (Ir.Op op in IrFacts.Operands(scan))
                    {
                        if (op is Ir.Cell cell)
                        {
                            blockOps.Add(IrLiveness.BaseSymbol(cell.Sym));
                        }
                        else if (op is Ir.AddrOf addr)
                        {
                            blockOps.Add(IrLiveness.BaseSymbol(addr.Sym));
                        }
                    }
                }

                foreach (Ir.AddrOf taken in IrFacts.Operands(scan).OfType<Ir.AddrOf>())
                {
                    blockOps.Add(IrLiveness.BaseSymbol(taken.Sym));
                }
            }

            foreach (Ir.Ins scan in callee.Body)
            {
                foreach (Ir.Op op in IrFacts.Operands(scan))
                {
                    string? sym = op is Ir.Cell cell ? cell.Sym : op is Ir.AddrOf address ? address.Sym : null;
                    string? foundation = sym is null ? null : IrLiveness.BaseSymbol(sym);
                    if (foundation is not null && !ren.ContainsKey(foundation) && foundation.StartsWith(callee.Name + "__", StringComparison.Ordinal)
                        && !liveIn.Contains(foundation) && !blockOps.Contains(foundation)
                        && dataBySym.TryGetValue(foundation, out Ir.Data? data) && data.Init is null && data.Size <= 4)
                    {
                        ren[foundation] = $"{foundation}__i{id}";
                        newData.Add(new Ir.Data(ren[foundation], "BSS", data.Size, null, false));
                    }
                }
            }
        }

        for (int i = 0; i < callee.Params.Count; i++)
        {
            output.Add(new Ir.Mov(new Ir.Cell(ren[callee.Params[i].Sym], callee.Params[i].W), RenameOp(call.Args[i], ren)));
        }

        string end = $"inl_end_{id}";
        Ir.Ins[] body = [.. callee.Body.Where(static i => i is not Ir.Src)];
        for (int i = 0; i < body.Length; i++)
        {
            switch (Rename(body[i], ren))
            {
                case Ir.Label label:
                    output.Add(new Ir.Label($"{label.Name}_i{id}"));
                    break;
                case Ir.Jmp jump:
                    output.Add(new Ir.Jmp($"{jump.Target}_i{id}"));
                    break;
                case Ir.BrCmp branch:
                    output.Add(branch with { Target = $"{branch.Target}_i{id}" });
                    break;
                case Ir.Ret ret:
                    if (ret.Value is not null && call.Result is not null)
                    {
                        output.Add(new Ir.Mov(call.Result, RenameOp(ret.Value, ren)));
                    }

                    if (i != body.Length - 1)
                    {
                        output.Add(new Ir.Jmp(end));
                    }

                    break;
                default:
                    output.Add(Rename(body[i], ren));
                    break;
            }
        }

        output.Add(new Ir.Label(end));
    }

    /// <summary>Czy funkcja zawiera pętlę licznikową (wtedy split tymczasowych odblokowuje <c>djnz</c>).</summary>
    /// <param name="callee">Wstawiana funkcja.</param>
    /// <returns>Czy któraś etykieta pasuje do wzorca.</returns>
    private static bool HasCountLoop(Ir.Function callee)
    {
        for (int i = 0; i < callee.Body.Count; i++)
        {
            if (callee.Body[i] is Ir.Label && ByteSelector.MatchCountLoop(callee, i) is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Podmienia symbole komórek w instrukcji kopiowanej do miejsca wołania.</summary>
    /// <param name="ins">Instrukcja oryginału.</param>
    /// <param name="ren">Stary symbol → nowy.</param>
    /// <returns>Instrukcja z podmienionymi komórkami.</returns>
    private static Ir.Ins Rename(Ir.Ins ins, Dictionary<string, string> ren) => ins switch
    {
        Ir.Mov mov => new Ir.Mov(RenameCell(mov.Dst, ren), RenameOp(mov.Src, ren)),
        Ir.Bin bin => bin with { Dst = RenameCell(bin.Dst, ren), A = RenameOp(bin.A, ren), B = RenameOp(bin.B, ren) },
        Ir.Un un => un with { Dst = RenameCell(un.Dst, ren), A = RenameOp(un.A, ren) },
        Ir.Load load => load with { Dst = RenameCell(load.Dst, ren), Ptr = RenameOp(load.Ptr, ren) },
        Ir.Store store => store with { Ptr = RenameOp(store.Ptr, ren), Value = RenameOp(store.Value, ren) },
        Ir.LoadIdx load => load with { Dst = RenameCell(load.Dst, ren), Index = RenameCell(load.Index, ren) },
        Ir.StoreIdx store => store with { Index = RenameCell(store.Index, ren), Value = RenameOp(store.Value, ren) },
        Ir.CopyBlock copy => copy with { Dst = RenameOp(copy.Dst, ren), Src = RenameOp(copy.Src, ren) },
        Ir.Fill fill => fill with { Dst = RenameOp(fill.Dst, ren) },
        Ir.BrCmp branch => branch with { A = RenameOp(branch.A, ren), B = RenameOp(branch.B, ren) },
        Ir.Ret ret => ret with { Value = ret.Value is null ? null : RenameOp(ret.Value, ren) },
        Ir.Call call => call with
        {
            Args = [.. call.Args.Select(a => RenameOp(a, ren))],
            Result = call.Result is null ? null : RenameCell(call.Result, ren),
        },
        _ => ins,
    };

    private static Ir.Cell RenameCell(Ir.Cell cell, Dictionary<string, string> ren) =>
        cell with { Sym = RenameSym(cell.Sym, ren) };

    private static Ir.Op RenameOp(Ir.Op op, Dictionary<string, string> ren) => op switch
    {
        Ir.Cell cell => RenameCell(cell, ren),
        Ir.AddrOf address => address with { Sym = RenameSym(address.Sym, ren) },
        _ => op,
    };

    /// <summary>Symbol po przyrostku miejsca wstawienia (połówki <c>x+1</c> idą za bazą).</summary>
    private static string RenameSym(string sym, Dictionary<string, string> ren)
    {
        string foundation = IrLiveness.BaseSymbol(sym);
        return ren.TryGetValue(foundation, out string? renamed) ? renamed + sym[foundation.Length..] : sym;
    }
}
