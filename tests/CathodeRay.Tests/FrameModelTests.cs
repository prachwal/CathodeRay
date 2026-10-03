using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 44, T1: niezależny model ramek rekurencji. Ramkę liczy osobny algorytm
/// ( jawne wyliczanie ścieżek po wołaniach, bez <c>IrLiveness</c>), a wynik ma być równy
/// <c>Saved</c> z lowering. Klasa błędów jak regresja <c>646d204</c> (skok w przód po wołaniu).</summary>
public sealed class FrameModelTests
{
    private const string Fib = """
        int fib(int n) {
            if (n <= 1) return n;
            return fib(n - 1) + fib(n - 2);
        }
        int main() { return fib(10); }
        """;

    private const string EvenOdd = """
        int is_even(int n);
        int is_odd(int n);
        int is_even(int n) { return n == 0 ? 1 : is_odd(n - 1); }
        int is_odd(int n) { return n == 0 ? 0 : is_even(n - 1); }
        int main() { return is_even(10) + is_odd(9); }
        """;

    private const string ForwardJump = """
        int h(int n) {
            int a = n + 1;
            int r;
            if (n > 0) { r = h(n - 1); } else { r = a; }
            return r + a;
        }
        int main() { return h(3); }
        """;

    private const string LoopCall = """
        int s(int n) {
            if (n <= 0) return 0;
            int t = 0;
            int i = 0;
            while (i < 3) { t = t + i; i++; }
            return t + s(n - 1);
        }
        int main() { return s(4); }
        """;

    private const string AddrTaken = """
        int g(int n) {
            if (n <= 0) return 0;
            int x = n * 2;
            int *p = &x;
            int r = g(n - 1);
            return *p + r;
        }
        int main() { return g(5); }
        """;

    public static TheoryData<string> Corpus()
    {
        var data = new TheoryData<string>();
        data.Add(Fib);
        data.Add(EvenOdd);
        data.Add(ForwardJump);
        data.Add(LoopCall);
        data.Add(AddrTaken);
        return data;
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public void Saved_Matches_Independent_Model(string source)
    {
        Ir.Module module = Lower(source);
        var byName = module.Functions.ToDictionary(static f => f.Name);
        foreach (Ir.Function function in module.Functions.Where(static f => !IsRuntime(f.Name)))
        {
            ModelOf(module, function).Should().BeEquivalentTo(function.Saved.Select(static o => o.Sym));
        }
    }

    private static Ir.Module Lower(string source)
    {
        return Codegen.Lower(TypeChecker.Check(Parser.Parse(source, null)));
    }

    private static bool IsRuntime(string name)
    {
        return name.StartsWith("__cc_", StringComparison.Ordinal) || name is "putchar" or "puthex" or "putdec";
    }

    private static HashSet<string> ModelOf(Ir.Module module, Ir.Function function)
    {
        var edges = module.Functions.ToDictionary(
            static f => f.Name,
            static f => f.Body.OfType<Ir.Call>().Select(static c => c.Direct).OfType<string>().ToHashSet(StringComparer.Ordinal));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>(edges.GetValueOrDefault(function.Name) ?? []);
        while (stack.Count > 0)
        {
            string next = stack.Pop();
            if (seen.Add(next) && edges.TryGetValue(next, out var more))
            {
                foreach (string callee in more)
                {
                    stack.Push(callee);
                }
            }
        }

        var across = new HashSet<string>(StringComparer.Ordinal);
        if (!seen.Contains(function.Name))
        {
            return across;
        }

        across.UnionWith(function.Params.Select(static p => Base(p.Sym)));
        across.UnionWith(function.Body.SelectMany(static i => IrFacts.Uses(i)).OfType<Ir.AddrOf>().Select(static a => Base(a.Sym)));
        var succ = Successors(function.Body);
        var maxW = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Ir.Ins ins in function.Body)
        {
            if (IrFacts.Def(ins) is { } def)
            {
                maxW[Base(def.Sym)] = Math.Max(maxW.GetValueOrDefault(Base(def.Sym)), def.W);
            }
        }

        for (int i = 0; i < function.Body.Count; i++)
        {
            if (function.Body[i] is Ir.Call call)
            {
                string? result = call.Result is null ? null : Base(call.Result.Sym);
                foreach (string live in LiveAfter(function.Body, succ, i, maxW).Where(s => s != result))
                {
                    across.Add(live);
                }
            }
        }

        return across;
    }

    private static HashSet<string> LiveAfter(IReadOnlyList<Ir.Ins> body, int[][] succ, int callIndex, Dictionary<string, int> maxW)
    {
        var live = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<(int, string)>();
        var stack = new Stack<(int Index, HashSet<string> Killed)>();
        foreach (int s in succ[callIndex])
        {
            stack.Push((s, []));
        }

        while (stack.Count > 0)
        {
            (int index, HashSet<string> killed) = stack.Pop();
            string key = string.Join(",", killed.Order(StringComparer.Ordinal));
            if (!visited.Add((index, key)))
            {
                continue;
            }

            Ir.Ins ins = body[index];
            foreach (Ir.Cell read in Reads(ins))
            {
                if (!killed.Contains(Base(read.Sym)))
                {
                    live.Add(Base(read.Sym));
                }
            }

            var next = new HashSet<string>(killed, StringComparer.Ordinal);
            if (IrFacts.Def(ins) is { } written
                && !written.Sym.Contains('+', StringComparison.Ordinal)
                && written.W == maxW.GetValueOrDefault(Base(written.Sym)))
            {
                next.Add(Base(written.Sym));
            }

            foreach (int s in succ[index])
            {
                stack.Push((s, next));
            }
        }

        return live;
    }

    private static IEnumerable<Ir.Cell> Reads(Ir.Ins ins)
    {
        foreach (Ir.Op op in IrFacts.Uses(ins))
        {
            if (op is Ir.Cell cell)
            {
                yield return cell;
            }
            else if (op is Ir.AddrOf addr)
            {
                yield return new Ir.Cell(addr.Sym, 1);
            }
        }
    }

    private static int[][] Successors(IReadOnlyList<Ir.Ins> body)
    {
        var labels = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < body.Count; i++)
        {
            if (body[i] is Ir.Label label)
            {
                labels[label.Name] = i;
            }
        }

        var succ = new int[body.Count][];
        for (int i = 0; i < body.Count; i++)
        {
            succ[i] = body[i] switch
            {
                Ir.Jmp jmp => [labels[jmp.Target]],
                Ir.BrCmp br => [labels[br.Target], i + 1 < body.Count ? i + 1 : i],
                Ir.Ret => [],
                _ => i + 1 < body.Count ? [i + 1] : [],
            };
        }

        return succ;
    }

    private static string Base(string symbol)
    {
        int plus = symbol.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? symbol : symbol[..plus];
    }
}
