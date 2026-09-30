namespace CathodeRay.C;

/// <summary>Aliasowanie parametrów liści na komórki argumentów (cele little-endian): parametr funkcji bez wywołań używa wprost
/// <c>cc_argN</c> (starszy bajt to <c>cc_argN_h</c>, sąsiedni w crt0), więc znika kopia w prologu. Wołany może niszczyć
/// <c>cc_argN</c> (konwencja: wołający nic nie zachowuje), a liść nie woła nikogo, więc nic innego ich nie nadpisze. Parametr
/// odpada, gdy jest wzięty adresem, <c>volatile</c>, zapisywany w ramce albo jego symbol występuje w innej funkcji (np. ciało
/// wstawione przez <see cref="IrInliner"/> w wołającym).</summary>
internal static class ParamAlias
{
    /// <summary>Aliasuje parametry liści modułu.</summary>
    /// <param name="module">Moduł po legalizacji.</param>
    /// <returns>Moduł z parametrami liści w <c>cc_argN</c> (bez komórek danych tych parametrów).</returns>
    public static Ir.Module Run(Ir.Module module)
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

        var removed = new HashSet<string>(StringComparer.Ordinal);
        var functions = new List<Ir.Function>();
        foreach (Ir.Function function in module.Functions)
        {
            Dictionary<string, string> alias = Aliases(function, blocked);
            removed.UnionWith(alias.Keys.Select(Base));
            functions.Add(alias.Count == 0 ? function : function with
            {
                Params = [.. function.Params.Select(p => Rename(p, alias))],
                Body = [.. function.Body.Select(i => Rename(i, alias))],
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
    private static Dictionary<string, string> Aliases(Ir.Function function, HashSet<string> blocked)
    {
        var alias = new Dictionary<string, string>(StringComparer.Ordinal);
        if (function.Body.Any(static i => i is Ir.Call))
        {
            return alias;
        }

        var widths = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < Math.Min(function.Params.Count, TypeChecker.MaxArgs); i++)
        {
            Ir.Cell param = function.Params[i];
            if (!blocked.Contains(Base(param.Sym)) && widths.TryAdd(param.Sym, param.W))
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

        foreach (string sym in alias.Keys.Where(k => bad.Contains(Base(k))).ToList())
        {
            alias.Remove(sym);
        }

        return alias;
    }

    private static Ir.Cell Rename(Ir.Cell cell, Dictionary<string, string> alias) =>
        alias.TryGetValue(cell.Sym, out string? sym) ? cell with { Sym = sym } : cell;

    private static Ir.Op Rename(Ir.Op op, Dictionary<string, string> alias) => (op is Ir.Cell cell) ? Rename(cell, alias) : op;

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
        _ => ins,
    };
}
