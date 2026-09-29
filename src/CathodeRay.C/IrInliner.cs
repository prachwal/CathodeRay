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
                foreach (Ir.Op op in Operands(ins))
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
        foreach (Ir.Function function in module.Functions)
        {
            var body = new List<Ir.Ins>();
            var inlined = new HashSet<string>(StringComparer.Ordinal);
            foreach (Ir.Ins ins in function.Body)
            {
                if (ins is Ir.Call { Direct: { } name } call && call.Indirect is null && candidates.TryGetValue(name, out Ir.Function? callee)
                    && name != function.Name && call.Args.Count == callee.Params.Count)
                {
                    Expand(call, callee, body, ++counter);
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

                foreach (Ir.Op op in Operands(ins))
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
        return module with { Functions = kept };
    }

    private static void Expand(Ir.Call call, Ir.Function callee, List<Ir.Ins> output, int id)
    {
        for (int i = 0; i < callee.Params.Count; i++)
        {
            output.Add(new Ir.Mov(callee.Params[i], call.Args[i]));
        }

        string end = $"inl_end_{id}";
        Ir.Ins[] body = [.. callee.Body.Where(static i => i is not Ir.Src)];
        for (int i = 0; i < body.Length; i++)
        {
            switch (body[i])
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
                        output.Add(new Ir.Mov(call.Result, ret.Value));
                    }

                    if (i != body.Length - 1)
                    {
                        output.Add(new Ir.Jmp(end));
                    }

                    break;
                default:
                    output.Add(body[i]);
                    break;
            }
        }

        output.Add(new Ir.Label(end));
    }

    private static IEnumerable<Ir.Op> Operands(Ir.Ins ins) => ins switch
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
}
