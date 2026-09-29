namespace CathodeRay.C;

/// <summary>Przebiegi optymalizujące na kodzie pośrednim (niezależne od celu).</summary>
internal static class IrPasses
{
    /// <summary>Uruchamia wszystkie przebiegi na ciele funkcji.</summary>
    /// <param name="body">Instrukcje.</param>
    /// <returns>Instrukcje po optymalizacji.</returns>
    public static List<Ir.Ins> Optimize(List<Ir.Ins> body) => ForwardTemporaries(body);

    private static bool IsTemporary(Ir.Cell cell) => cell.Sym.Contains("__t@", StringComparison.Ordinal);

    private static bool Reads(Ir.Op op, string symbol) => op is Ir.Cell cell && cell.Sym == symbol;

    private static IEnumerable<Ir.Op> ReadOperands(Ir.Ins ins) => ins switch
    {
        Ir.Mov mov => [mov.Src],
        Ir.Bin bin => [bin.A, bin.B],
        Ir.Un un => [un.A],
        Ir.Load load => [load.Ptr],
        Ir.Store store => [store.Ptr, store.Value],
        Ir.CopyBlock copy => [copy.Dst, copy.Src],
        Ir.Fill fill => [fill.Dst],
        Ir.BrCmp branch => [branch.A, branch.B],
        Ir.Call call => call.Indirect is null ? call.Args : [.. call.Args, call.Indirect],
        Ir.Ret { Value: not null } ret => [ret.Value],
        _ => [],
    };

    private static Ir.Cell? Defined(Ir.Ins ins) => ins switch
    {
        Ir.Mov mov => mov.Dst,
        Ir.Bin bin => bin.Dst,
        Ir.Un un => un.Dst,
        Ir.Load load => load.Dst,
        Ir.Call call => call.Result,
        _ => null,
    };

    private static Ir.Ins WithDestination(Ir.Ins ins, Ir.Cell destination) => ins switch
    {
        Ir.Mov mov => mov with { Dst = destination },
        Ir.Bin bin => bin with { Dst = destination },
        Ir.Un un => un with { Dst = destination },
        Ir.Load load => load with { Dst = destination },
        Ir.Call call => call with { Result = destination },
        _ => ins,
    };

    /// <summary>Czy tymczasowa <paramref name="symbol"/> jest martwa od instrukcji <paramref name="from"/>: kolejne odczyty
    /// ją ożywiają, zapis albo znacznik początku instrukcji C (<see cref="Ir.Src"/>: tymczasowe nie żyją między instrukcjami)
    /// ją zabija; etykiety, skoki i powroty przerywają analizę (zakładamy, że żyje).</summary>
    private static bool IsDeadAfter(List<Ir.Ins> body, int from, string symbol)
    {
        for (int i = from; i < body.Count; i++)
        {
            Ir.Ins ins = body[i];
            if (ins is Ir.Src)
            {
                return true;
            }

            if (ins is Ir.Label or Ir.Jmp or Ir.Ret)
            {
                return false;
            }

            if (ReadOperands(ins).Any(op => Reads(op, symbol)))
            {
                return false;
            }

            if (Defined(ins) is { } written && written.Sym == symbol)
            {
                return true;
            }
        }

        return true;
    }

    /// <summary>Zamienia <c>t = op …; v = t</c> na <c>v = op …</c>, gdy <c>t</c> jest tymczasową martwą po kopii, a szerokości się zgadzają.</summary>
    private static List<Ir.Ins> ForwardTemporaries(List<Ir.Ins> body)
    {
        var result = new List<Ir.Ins>(body.Count);
        for (int i = 0; i < body.Count; i++)
        {
            if (i + 1 < body.Count
                && Defined(body[i]) is { } temporary && IsTemporary(temporary)
                && body[i] is not Ir.Mov
                && body[i + 1] is Ir.Mov { Src: Ir.Cell source } copy && source.Sym == temporary.Sym
                && copy.Dst.W == temporary.W && source.W == temporary.W
                && !ReadOperands(body[i]).Any(op => Reads(op, copy.Dst.Sym) && body[i] is Ir.Bin { Kind: not (Ir.BinOp.Add or Ir.BinOp.Sub or Ir.BinOp.And or Ir.BinOp.Or or Ir.BinOp.Xor) })
                && IsDeadAfter(body, i + 2, temporary.Sym))
            {
                result.Add(WithDestination(body[i], copy.Dst));
                i++;
                continue;
            }

            result.Add(body[i]);
        }

        return result;
    }
}
