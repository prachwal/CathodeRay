namespace CathodeRay.C;

/// <summary>Lowering: które funkcje potrzebują ramki. Komórki są statyczne, więc zapisywać je trzeba tylko wtedy, gdy
/// funkcja może być aktywna dwa razy naraz, czyli leży na cyklu grafu wołań. Wołanie pośrednie ma krawędzie do wszystkich
/// funkcji o wziętym adresie, wołanie nieznanej funkcji zewnętrznej — do funkcji eksportowanych i o wziętym adresie
/// (zewnętrzny kod może wołać z powrotem); funkcje biblioteki standardowej i konsola nie wołają kodu użytkownika.</summary>
internal sealed partial class Lowering
{
    private static IEnumerable<Ir.Op> OperandsOf(Ir.Ins ins) => ins switch
    {
        Ir.Mov mov => [mov.Src],
        Ir.Bin bin => [bin.A, bin.B],
        Ir.Un un => [un.A],
        Ir.Load load => [load.Ptr],
        Ir.Store store => [store.Ptr, store.Value],
        Ir.CopyBlock copy => [copy.Dst, copy.Src],
        Ir.Fill fill => [fill.Dst],
        Ir.BrCmp branch => [branch.A, branch.B],
        Ir.Call call => call.Args,
        Ir.Ret { Value: not null } ret => [ret.Value],
        _ => [],
    };

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

    private static void CollectLive(Ir.Ins ins, HashSet<string> liveSymbols, HashSet<string> alreadyWritten)
    {
        // Zbierz zmienne czytane.
        foreach (Ir.Op op in OperandsOf(ins))
        {
            if (op is Ir.Cell cell && !alreadyWritten.Contains(cell.Sym))
            {
                liveSymbols.Add(cell.Sym);
            }
            else if (op is Ir.AddrOf addr && !alreadyWritten.Contains(addr.Sym))
            {
                liveSymbols.Add(addr.Sym);
            }
        }

        // Zaznacz zmienne zapisane.
        if (ins is Ir.Mov mov)
        {
            alreadyWritten.Add(mov.Dst.Sym);
        }
        else if (ins is Ir.Bin bin)
        {
            alreadyWritten.Add(bin.Dst.Sym);
        }
        else if (ins is Ir.Un un)
        {
            alreadyWritten.Add(un.Dst.Sym);
        }
        else if (ins is Ir.Load load)
        {
            alreadyWritten.Add(load.Dst.Sym);
        }
        else if (ins is Ir.LoadIdx loadIdx)
        {
            alreadyWritten.Add(loadIdx.Dst.Sym);
        }
        else if (ins is Ir.Call call && call.Result != null)
        {
            alreadyWritten.Add(call.Result.Sym);
        }
    }

    private void FinalizeFrames()
    {
        var defined = new HashSet<string>(_pending.Select(static p => p.Function.Name), StringComparer.Ordinal);
        var addressTaken = new HashSet<string>(StringComparer.Ordinal);
        foreach (Ir.Function function in _pending.Select(static p => p.Function).Concat(_initFunction is null ? [] : [_initFunction]))
        {
            foreach (Ir.Ins ins in function.Body)
            {
                foreach (Ir.Op op in OperandsOf(ins))
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
                // Zbierz komórki żywe po wołaniach: skanuj od każdego Call do końca,
                // zbieraj zmienne czytane bez wcześniejszego zapisu.
                var liveSymbols = new HashSet<string>(StringComparer.Ordinal);

                // Zawsze zbierz parametry — muszą być na stosie w funkcji rekurencyjnej.
                foreach (Ir.Cell param in pending.Function.Params)
                {
                    liveSymbols.Add(param.Sym);
                }

                // Mapuj etykiety do indeksów i identyfikuj te z skokami wstecz.
                var labelToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
                var hasBackwardJump = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < pending.Function.Body.Count; i++)
                {
                    var ins = pending.Function.Body[i];
                    if (ins is Ir.Label label)
                    {
                        labelToIndex[label.Name] = i;
                    }
                    else if (ins is Ir.Jmp jmp)
                    {
                        if (labelToIndex.TryGetValue(jmp.Target, out int targetIdx) && targetIdx < i)
                        {
                            hasBackwardJump.Add(jmp.Target);
                        }
                    }
                    else if (ins is Ir.BrCmp br)
                    {
                        if (labelToIndex.TryGetValue(br.Target, out int targetIdx) && targetIdx < i)
                        {
                            hasBackwardJump.Add(br.Target);
                        }
                    }
                }

                // Znajdź wszystkie Call i skanuj od każdego do końca funkcji.
                for (int i = 0; i < pending.Function.Body.Count; i++)
                {
                    if (pending.Function.Body[i] is not Ir.Call)
                    {
                        continue;
                    }

                    var alreadyWritten = new HashSet<string>(StringComparer.Ordinal);

                    for (int j = i + 1; j < pending.Function.Body.Count; j++)
                    {
                        var ins = pending.Function.Body[j];

                        // Jeśli etykieta ma skok wstecz, skanuj całą funkcję od niej bez ograniczeń alreadyWritten.
                        if (ins is Ir.Label label && hasBackwardJump.Contains(label.Name))
                        {
                            var noWrites = new HashSet<string>(StringComparer.Ordinal);
                            for (int k = j; k < pending.Function.Body.Count; k++)
                            {
                                CollectLive(pending.Function.Body[k], liveSymbols, noWrites);
                            }

                            break;
                        }
                        else
                        {
                            CollectLive(ins, liveSymbols, alreadyWritten);
                        }
                    }
                }

                // Dodaj do saved tylko zmienne żywe.
                saved.AddRange(pending.Scalars.Where(s => liveSymbols.Contains(s.Sym)));
                foreach (Ir.Owned aggregate in pending.Aggregates)
                {
                    if (liveSymbols.Contains(aggregate.Sym))
                    {
                        if (aggregate.Size > MaxSavedAggregate)
                        {
                            throw new CCodegenException($"recursive function '{name}' has local '{aggregate.Sym.Split("__").Last()}' of {aggregate.Size} B (max {MaxSavedAggregate}).");
                        }

                        saved.Add(aggregate);
                    }
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
