using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 37, zadanie 3: żywość VReg na grafie przepływu — regresje skoku w przód i pętli
/// (odpowiedniki testów ramek z <c>RecursionFrameTests</c>).</summary>
public sealed class VRegLivenessTests
{
    private const string OverwrittenSource = """
        int k(int n) {
            int x;
            int r;
            x = n * 5;
            if (n == 0) return x + 1;
            r = k(n - 1);
            x = n + 2;
            return x + r;
        }
        int main() { return k(3); }
        """;

    private const string GotoSource = """
        int g(int n) {
            int x;
            x = n * 3;
            if (n == 0) return 0;
            g(n - 1);
            if (n & 1) goto skip;
            x = 100;
            skip:
            return x;
        }
        int main() { return g(1) * 10 + g(3); }
        """;

    private const string LoopSource = """
        int h(int n) {
            int s;
            int i;
            if (n == 0) return 1;
            s = n;
            for (i = 0; i < 2; i = i + 1) {
                s = s + h(n - 1);
            }
            return s;
        }
        int main() { return h(3); }
        """;

    private static Ir.Module LowerCell(string source) =>
        Codegen.Lower(TypeChecker.Check(Parser.Parse(source, StdLib.HeaderReader)), "t.c");

    private static VReg.Function LiftFunc(string source, string name) =>
        VRegLift.Run(LowerCell(source)).Functions.Single(f => f.Name == name);

    private static int RegOf(VReg.Function function, string sym) =>
        function.Sym.Single(kv => kv.Value == sym).Key;

    private static int CallIndex(VRegLiveness live)
    {
        for (int i = 0; i < live.Count; i++)
        {
            if (live.At(i) is VReg.Call)
            {
                return i;
            }
        }

        throw new InvalidOperationException("brak wołania w funkcji.");
    }

    /// <summary>Zbiór komórek żywych przez wołania wg Cell IR (reguła <c>LiveAcrossCalls</c>: parametry + żywe po wołaniu + adresy).</summary>
    private static HashSet<string> CellAcross(Ir.Function function)
    {
        // Pełne rozmiary jak w Lowering (Scalars + Aggregates): z definicji, parametrów i Saved.
        var sizes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Ir.Ins ins in function.Body)
        {
            if (IrFacts.Def(ins) is { } defined)
            {
                sizes.TryAdd(defined.Sym, defined.W);
            }
        }

        foreach (Ir.Cell param in function.Params)
        {
            sizes.TryAdd(param.Sym, param.W);
        }

        foreach (Ir.Owned owned in function.Saved)
        {
            sizes.TryAdd(owned.Sym, owned.Size);
        }

        int Size(string sym) => sizes.GetValueOrDefault(sym);
        var live = IrLiveness.Of(function.Body, Size);
        var across = new HashSet<string>(function.Params.Select(static p => IrLiveness.BaseSymbol(p.Sym)), StringComparer.Ordinal);
        for (int i = 0; i < function.Body.Count; i++)
        {
            if (function.Body[i] is Ir.Call call)
            {
                string? result = IrLiveness.Killed(call, Size);
                across.UnionWith(live.LiveOut(i).Where(sym => sym != result));
            }

            across.UnionWith(IrFacts.Uses(function.Body[i]).OfType<Ir.AddrOf>().Select(static a => IrLiveness.BaseSymbol(a.Sym)));
        }

        return across;
    }

    /// <summary>Ten sam zbiór wg VReg (klucze <c>r{id}</c> przez mapę symboli, <c>m{baza}</c> wprost).</summary>
    private static HashSet<string> VRegAcross(VReg.Function function)
    {
        VRegLiveness live = VRegLiveness.Of(function);
        var across = new HashSet<string>(function.Params.Select(p => VRegFacts.BaseSymbol(function.Sym[p.Id])), StringComparer.Ordinal);
        for (int i = 0; i < live.Count; i++)
        {
            if (live.At(i) is VReg.Call call)
            {
                string? result = call.Result is null ? null : "r" + call.Result.Id;
                foreach (string key in live.LiveOut(i))
                {
                    if (key != result && RegSym(function, key) is { } sym)
                    {
                        across.Add(sym);
                    }
                }
            }

            foreach (VReg.Op op in VRegFacts.Uses(live.At(i)))
            {
                if (op is VReg.Addr addr)
                {
                    across.Add(addr.Sym);
                }
                else if (op is VReg.Pinned pinned)
                {
                    across.Add(VRegFacts.BaseSymbol(pinned.Sym));
                }
            }
        }

        return across;
    }

    private static string? RegSym(VReg.Function function, string key) =>
        key.StartsWith('r') && int.TryParse(key[1..], out int id) && function.Sym.TryGetValue(id, out string? sym)
            ? VRegFacts.BaseSymbol(sym)
            : key.StartsWith('m') ? key[1..] : null;

    [Theory]
    [InlineData(OverwrittenSource, "k")]
    [InlineData(GotoSource, "g")]
    [InlineData(LoopSource, "h")]
    public void Across_Calls_Matches_Cell_IR(string source, string name)
    {
        Ir.Module cell = LowerCell(source);
        VReg.Function lifted = VRegLift.Run(cell).Functions.Single(f => f.Name == name);
        VRegAcross(lifted).Should().BeEquivalentTo(CellAcross(cell.Functions.Single(f => f.Name == name)));
    }

    [Fact]
    public void Overwritten_Local_Is_Dead_Across_Call()
    {
        VReg.Function k = LiftFunc(OverwrittenSource, "k");
        VRegLiveness live = VRegLiveness.Of(k);
        int call = CallIndex(live);

        // n: parametr (żyje), r: wynik wołania (żyje), x: nadpisane przed odczytem (martwe)
        live.LiveOut(call).Should().Contain("r" + RegOf(k, "k__n"));
        live.LiveOut(call).Should().Contain("r" + RegOf(k, "k__r"));
        live.LiveOut(call).Should().NotContain("r" + RegOf(k, "k__x"));
    }

    [Fact]
    public void Local_Survives_Forward_Goto_Across_Call()
    {
        VReg.Function g = LiftFunc(GotoSource, "g");
        VRegLiveness live = VRegLiveness.Of(g);
        int call = CallIndex(live);
        live.LiveOut(call).Should().Contain("r" + RegOf(g, "g__x"), "skok w przód nie może ukryć odczytu x");
    }

    [Fact]
    public void Jump_To_Trailing_Label_Is_Exit()
    {
        // Inliner dokleja pustą etykietę inl_end_* na końcu; skok do niej to wyjście, nie awaria.
        var r0 = new VReg.Reg(0, 1);
        var c = new VReg.Reg(1, 1);
        var function = new VReg.Function("f", false, [r0], new Dictionary<int, string>(), [], [], 1, [
            new VReg.Block("a", true, [new VReg.Cmp(Ir.Cond.Ne, c, r0, new VReg.Imm(0, 1)), new VReg.Br(c, "end", "b")]),
            new VReg.Block("b", true, [new VReg.Ret(new VReg.Imm(2, 1), 1)]),
            new VReg.Block("end", true, []),
        ]);
        VRegLiveness live = VRegLiveness.Of(function);
        live.LiveIn(0).Should().Contain("r0");
    }

    [Fact]
    public void Fallthrough_Past_End_Is_Exit()
    {
        // Spadek za ostatni blok ("__end" z lifta): nie rzuca, traktowany jak wyjście.
        var r0 = new VReg.Reg(0, 1);
        var c = new VReg.Reg(1, 1);
        var function = new VReg.Function("f", false, [r0], new Dictionary<int, string>(), [], [], 1, [
            new VReg.Block("a", true, [new VReg.Cmp(Ir.Cond.Ne, c, r0, new VReg.Imm(0, 1)), new VReg.Br(c, "b", "__end")]),
            new VReg.Block("b", true, [new VReg.Ret(new VReg.Imm(2, 1), 1)]),
        ]);
        VRegLiveness live = VRegLiveness.Of(function);
        live.LiveIn(0).Should().Contain("r0");
    }

    [Fact]
    public void Branch_Reaches_Both_Targets()
    {
        var r0 = new VReg.Reg(0, 1);
        var c = new VReg.Reg(1, 1);
        var function = new VReg.Function("f", false, [r0], new Dictionary<int, string>(), [], [], 1, [
            new VReg.Block("a", true, [new VReg.Cmp(Ir.Cond.Ne, c, r0, new VReg.Imm(0, 1)), new VReg.Br(c, "b", "c")]),
            new VReg.Block("b", true, [new VReg.Ret(new VReg.Imm(1, 1), 1)]),
            new VReg.Block("c", true, [new VReg.Ret(new VReg.Imm(2, 1), 1)]),
        ]);
        VRegLiveness live = VRegLiveness.Of(function);
        live.LiveIn(0).Should().Contain("r0", "porównanie czyta parametr");
        live.LiveOut(1).Should().BeEmpty("po rozgałęzieniu nic nie żyje");
    }
}
