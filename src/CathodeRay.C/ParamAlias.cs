namespace CathodeRay.C;

/// <summary>Aliasowanie parametrów na komórki argumentów (cele little-endian): parametr używa wprost <c>cc_argN</c> (starszy bajt
/// to <c>cc_argN_h</c>, sąsiedni w crt0), więc znika kopia w prologu. Wołany może niszczyć <c>cc_argN</c> (konwencja: wołający nic
/// nie zachowuje), a wołanie samo je zapisuje (argumenty po kolei, od pierwszego). Liść nie woła nikogo, więc nic ich nie nadpisze;
/// w funkcji z wołaniami (plan 35, krok 3) parametr nie może być żywy za żadnym wołaniem (<see cref="IrLiveness"/>: także pętla i
/// <c>goto</c> wstecz), nie może być wskaźnikiem wołania pośredniego ani argumentem na pozycji dalszej niż własna (wcześniejsze
/// argumenty nadpisałyby go przed odczytem). Parametr odpada też, gdy jest wzięty adresem, <c>volatile</c>, zapisywany w ramce
/// albo jego symbol występuje w innej funkcji (np. ciało wstawione przez <see cref="IrInliner"/> w wołającym).</summary>
internal static class ParamAlias
{
    /// <summary>Aliasuje parametry liści modułu.</summary>
    /// <param name="module">Moduł po legalizacji.</param>
    /// <param name="canAlias">Który parametr (funkcja, indeks) wolno położyć na <c>cc_argN</c>;
    /// null = wszystkie. ABI v2 wyklucza parametry niesione rejestrami (żyją w A/X, nie w pamięci).</param>
    /// <returns>Moduł z parametrami liści w <c>cc_argN</c> (bez komórek danych tych parametrów).</returns>
    public static Ir.Module Run(Ir.Module module, Func<Ir.Function, int, bool>? canAlias = null)
    {
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        var owner = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int f = 0; f < module.Functions.Count; f++)
        {
            Ir.Function function = module.Functions[f];
            blocked.UnionWith(function.Saved.Select(static o => Base(o.Sym)));
            foreach (string sym in function.Params.Select(static p => p.Sym).Concat(function.Body.SelectMany(Symbols)))
            {
                string key = Base(sym);
                if (owner.TryGetValue(key, out int other) && other != f)
                {
                    blocked.Add(key);
                }

                owner[key] = f;
            }

            foreach (Ir.Ins ins in function.Body)
            {
                blocked.UnionWith(IrFacts.Operands(ins).OfType<Ir.AddrOf>().Select(static a => Base(a.Sym)));
                if (ins is Ir.LoadIdx or Ir.StoreIdx)
                {
                    blocked.Add(Base(ins is Ir.LoadIdx load ? load.Sym : ((Ir.StoreIdx)ins).Sym));
                }
            }
        }

        blocked.UnionWith(module.Data.Where(static d => d.Init is not null).SelectMany(static d => d.Init!.OfType<Ir.SymWord>()).Select(static w => Base(w.Sym)));
        blocked.UnionWith(module.Data.Where(static d => d.Exported).Select(static d => Base(d.Sym)));
        blocked.UnionWith(module.ExternCells.Select(Base));
        if (module.Volatile is not null)
        {
            blocked.UnionWith(module.Volatile.Select(Base));
        }

        var sizes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Ir.Data data in module.Data)
        {
            sizes[data.Sym] = data.Size;
        }

        var removed = new HashSet<string>(StringComparer.Ordinal);
        var functions = new List<Ir.Function>();
        foreach (Ir.Function function in module.Functions)
        {
            Dictionary<string, string> alias = Aliases(function, blocked, sizes, canAlias);
            removed.UnionWith(alias.Keys.Select(Base));
            functions.Add(alias.Count == 0 ? function : function with
            {
                Params = [.. function.Params.Select(p => Rename(p, alias))],
                Body = [.. function.Body.Select(i => Rename(i, alias)).Where(i => !IsNoOpMov(i))],
            });
        }

        return module with { Functions = functions, Data = [.. module.Data.Where(d => !removed.Contains(d.Sym))] };
    }

    /// <summary>Następna komórka argumentu: <c>cc_argN</c> → <c>cc_argN+1</c> (starsza połowa aliasowanego parametru <c>long</c>).</summary>
    /// <param name="sym">Symbol komórki.</param>
    /// <returns>Symbol następnego argumentu albo <see langword="null"/>, gdy <paramref name="sym"/> nie jest komórką argumentu.</returns>
    public static string? NextArg(string sym) =>
        (sym.StartsWith("cc_arg", StringComparison.Ordinal) && int.TryParse(sym.AsSpan(6), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int n))
            ? $"cc_arg{n + 1}" : null;

    /// <summary>Symbol obiektu bez przesunięcia (<c>x+2</c> → <c>x</c>).</summary>
    private static string Base(string sym)
    {
        int cut = sym.IndexOfAny(['+', '-']);
        return (cut < 0) ? sym : sym[..cut];
    }

    private static IEnumerable<string> Symbols(Ir.Ins ins)
    {
        foreach (Ir.Op op in IrFacts.Operands(ins))
        {
            switch (op)
            {
                case Ir.Cell cell:
                    yield return cell.Sym;
                    break;
                case Ir.AddrOf address:
                    yield return address.Sym;
                    break;
            }
        }

        if (ins is Ir.LoadIdx load)
        {
            yield return load.Sym;
        }
        else if (ins is Ir.StoreIdx store)
        {
            yield return store.Sym;
        }
    }

    /// <summary>Parametry funkcji, które można przenieść do <c>cc_argN</c>: symbol parametru → symbol argumentu.</summary>
    private static Dictionary<string, string> Aliases(Ir.Function function, HashSet<string> blocked, Dictionary<string, int> sizes, Func<Ir.Function, int, bool>? canAlias)
    {
        var alias = new Dictionary<string, string>(StringComparer.Ordinal);
        var widths = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < Math.Min(function.Params.Count, TypeChecker.MaxArgs); i++)
        {
            Ir.Cell param = function.Params[i];
            if (!blocked.Contains(Base(param.Sym)) && widths.TryAdd(param.Sym, param.W)
                && (canAlias is null || canAlias(function, i)))
            {
                alias[param.Sym] = $"cc_arg{i + 1}";
            }
        }

        // każde użycie obiektu parametru musi być jedną z jego komórek-parametrów i nie szersze od niej; wyjątek: cały parametr
        // long, gdy obie połówki trafiają do kolejnych cc_argN, cc_argN+1 (ByteIsa.Loc składa je w jedną komórkę)
        var bad = new HashSet<string>(StringComparer.Ordinal);
        foreach (Ir.Cell cell in function.Body.SelectMany(IrFacts.Operands).OfType<Ir.Cell>().Concat(function.Body.OfType<Ir.LoadIdx>().Select(static l => l.Index)).Concat(function.Body.OfType<Ir.StoreIdx>().Select(static s => s.Index)))
        {
            bool whole = cell.W == 4 && alias.TryGetValue(cell.Sym, out string? low) && alias.TryGetValue(cell.Sym + "+2", out string? high) && high == NextArg(low);
            if (!whole && (!widths.TryGetValue(cell.Sym, out int width) || cell.W > width))
            {
                bad.Add(Base(cell.Sym));
            }
        }

        bad.UnionWith(ClobberedByCalls(function, alias, sizes));
        foreach (string sym in alias.Keys.Where(k => bad.Contains(Base(k))).ToList())
        {
            alias.Remove(sym);
        }

        return alias;
    }

    /// <summary>Obiekty parametrów, których <c>cc_argN</c> wołanie nadpisałoby, zanim przestaną być potrzebne: żywe za wołaniem,
    /// wskaźnik wołania pośredniego (czytany po zapisie argumentów) albo argument na pozycji dalszej niż własny numer.</summary>
    private static HashSet<string> ClobberedByCalls(Ir.Function function, Dictionary<string, string> alias, Dictionary<string, int> sizes)
    {
        var bad = new HashSet<string>(StringComparer.Ordinal);
        if (alias.Count == 0 || !function.Body.Any(static i => i is Ir.Call))
        {
            return bad;
        }

        var index = alias.ToDictionary(static a => a.Key, a => int.Parse(a.Value.AsSpan(6), System.Globalization.CultureInfo.InvariantCulture) - 1, StringComparer.Ordinal);
        IrLiveness live = IrLiveness.Of(function.Body, sym => sizes.GetValueOrDefault(sym));
        for (int i = 0; i < function.Body.Count; i++)
        {
            if (function.Body[i] is not Ir.Call call)
            {
                continue;
            }

            bad.UnionWith(live.LiveOut(i));
            if (call.Indirect is { } pointer)
            {
                bad.Add(Base(pointer.Sym));
            }

            for (int arg = 0; arg < call.Args.Count; arg++)
            {
                if (call.Args[arg] is Ir.Cell cell && index.TryGetValue(cell.Sym, out int own) && arg > own)
                {
                    bad.Add(Base(cell.Sym));
                }
            }
        }

        return bad;
    }

    private static Ir.Cell Rename(Ir.Cell cell, Dictionary<string, string> alias) =>
        alias.TryGetValue(cell.Sym, out string? sym) ? cell with { Sym = sym } : cell;

    private static Ir.Op Rename(Ir.Op op, Dictionary<string, string> alias) => (op is Ir.Cell cell) ? Rename(cell, alias) : op;

    /// <summary>Czy instrukcja to Mov z tą samą komórką po obu stronach (żaden efekt).</summary>
    private static bool IsNoOpMov(Ir.Ins ins)
    {
        if (ins is not Ir.Mov mov)
        {
            return false;
        }

        if (mov.Src is not Ir.Cell src)
        {
            return false;
        }

        return mov.Dst.Sym == src.Sym && mov.Dst.W == src.W;
    }

    private static Ir.Ins Rename(Ir.Ins ins, Dictionary<string, string> alias) => ins switch
    {
        Ir.Mov mov => new Ir.Mov(Rename(mov.Dst, alias), Rename(mov.Src, alias)),
        Ir.Bin bin => bin with { Dst = Rename(bin.Dst, alias), A = Rename(bin.A, alias), B = Rename(bin.B, alias) },
        Ir.Un un => un with { Dst = Rename(un.Dst, alias), A = Rename(un.A, alias) },
        Ir.Load load => load with { Dst = Rename(load.Dst, alias), Ptr = Rename(load.Ptr, alias) },
        Ir.Store store => store with { Ptr = Rename(store.Ptr, alias), Value = Rename(store.Value, alias) },
        Ir.LoadIdx load => load with { Dst = Rename(load.Dst, alias), Index = Rename(load.Index, alias) },
        Ir.StoreIdx store => store with { Index = Rename(store.Index, alias), Value = Rename(store.Value, alias) },
        Ir.CopyBlock copy => copy with { Dst = Rename(copy.Dst, alias), Src = Rename(copy.Src, alias) },
        Ir.Fill fill => fill with { Dst = Rename(fill.Dst, alias) },
        Ir.BrCmp branch => branch with { A = Rename(branch.A, alias), B = Rename(branch.B, alias) },
        Ir.Ret ret => ret with { Value = (ret.Value is null) ? null : Rename(ret.Value, alias) },
        Ir.Call call => call with
        {
            Indirect = (call.Indirect is null) ? null : Rename(call.Indirect, alias),
            Args = [.. call.Args.Select(a => Rename(a, alias))],
            Result = (call.Result is null) ? null : Rename(call.Result, alias),
        },
        _ => ins,
    };
}
