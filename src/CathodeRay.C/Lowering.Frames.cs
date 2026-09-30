namespace CathodeRay.C;

/// <summary>Lowering: które funkcje potrzebują ramki. Komórki są statyczne, więc zapisywać je trzeba tylko wtedy, gdy
/// funkcja może być aktywna dwa razy naraz, czyli leży na cyklu grafu wołań. Wołanie pośrednie ma krawędzie do wszystkich
/// funkcji o wziętym adresie, wołanie nieznanej funkcji zewnętrznej — do funkcji eksportowanych i o wziętym adresie
/// (zewnętrzny kod może wołać z powrotem); funkcje biblioteki standardowej i konsola nie wołają kodu użytkownika.</summary>
internal sealed partial class Lowering
{
    private static bool IsKnownLeaf(string function) =>
        function is "putchar" or "puthex" or "putdec" || StdLib.Modules.Any(m => m.Defines.Contains(function));

    private static HashSet<string> Reach(string start, Dictionary<string, HashSet<string>> edges)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>(edges.GetValueOrDefault(start) ?? []);
        while (stack.Count > 0)
        {
            string next = stack.Pop();
            if (seen.Add(next) && edges.TryGetValue(next, out HashSet<string>? more))
            {
                foreach (string callee in more)
                {
                    stack.Push(callee);
                }
            }
        }

        return seen;
    }

    private void FinalizeFrames()
    {
        var defined = new HashSet<string>(_pending.Select(static p => p.Function.Name), StringComparer.Ordinal);
        var addressTaken = new HashSet<string>(StringComparer.Ordinal);
        foreach (Ir.Function function in _pending.Select(static p => p.Function).Concat(_initFunction is null ? [] : [_initFunction]))
        {
            foreach (Ir.Ins ins in function.Body)
            {
                foreach (Ir.Op op in IrFacts.Uses(ins))
                {
                    if (op is Ir.AddrOf { Sym: var symbol } && _functions.ContainsKey(symbol))
                    {
                        addressTaken.Add(symbol);
                    }
                }
            }
        }

        foreach (Ir.SymWord word in _dataOut.Where(static d => d.Init is not null).SelectMany(static d => d.Init!.OfType<Ir.SymWord>()))
        {
            if (_functions.ContainsKey(word.Sym) && word.Sym != "__cc_init")
            {
                addressTaken.Add(word.Sym);
            }
        }

        var takenHere = new HashSet<string>(addressTaken.Where(defined.Contains), StringComparer.Ordinal);
        var unknownTargets = new HashSet<string>(takenHere, StringComparer.Ordinal);
        unknownTargets.UnionWith(_pending.Where(static p => !p.Function.IsStatic).Select(static p => p.Function.Name));
        var edges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (Pending pending in _pending)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (string callee in _calls.GetValueOrDefault(pending.Function.Name) ?? [])
            {
                if (defined.Contains(callee))
                {
                    set.Add(callee);
                }
                else if (!IsKnownLeaf(callee))
                {
                    set.UnionWith(unknownTargets);
                }
            }

            if (pending.Function.Body.OfType<Ir.Call>().Any(static c => c.Indirect is not null))
            {
                set.UnionWith(takenHere);
                if (!_calls.TryGetValue(pending.Function.Name, out HashSet<string>? recorded))
                {
                    _calls[pending.Function.Name] = recorded = new HashSet<string>(StringComparer.Ordinal);
                }

                recorded.UnionWith(takenHere);
            }

            edges[pending.Function.Name] = set;
        }

        foreach (Pending pending in _pending)
        {
            string name = pending.Function.Name;
            bool framed = Reach(name, edges).Contains(name);
            var saved = new List<Ir.Owned>();
            if (framed)
            {
                saved.AddRange(pending.Scalars);
                foreach (Ir.Owned aggregate in pending.Aggregates)
                {
                    if (aggregate.Size > MaxSavedAggregate)
                    {
                        throw new CCodegenException($"recursive function '{name}' has local '{aggregate.Sym.Split("__").Last()}' of {aggregate.Size} B (max {MaxSavedAggregate}).");
                    }

                    saved.Add(aggregate);
                }
            }

            _frames[name] = saved.Sum(static o => o.Size) + 2;
            _functionsOut.Add(pending.Function with { Saved = saved });
        }

        if (_initFunction is not null)
        {
            _functionsOut.Add(_initFunction);
        }
    }
}
