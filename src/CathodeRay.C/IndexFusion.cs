namespace CathodeRay.C;

/// <summary>Przebieg tuż przed selektorem celu z adresowaniem indeksowanym (6502: <c>LDA tab,X</c>): ciąg
/// <c>[s = i &lt;&lt; k;] t = &amp;tab + s; Load/Store przez t</c> na tablicy o znanym adresie i rozmiarze do 256 B staje się
/// <see cref="Ir.LoadIdx"/> / <see cref="Ir.StoreIdx"/>. Zakłada, że indeks mieści się w tablicy (poza nią zachowanie jest niezdefiniowane, jak w C).</summary>
internal static class IndexFusion
{
    private const int MaxArray = 256;

    /// <summary>Zamienia dopasowane ciągi w każdej funkcji.</summary>
    /// <param name="module">Moduł po legalizacji.</param>
    /// <returns>Moduł z instrukcjami indeksowanymi.</returns>
    public static Ir.Module Run(Ir.Module module)
    {
        var sizes = module.Data.Where(static d => d.Sym.Length > 0).ToDictionary(static d => d.Sym, static d => d.Size, StringComparer.Ordinal);
        return module with { Functions = [.. module.Functions.Select(f => f with { Body = Fuse([.. f.Body], sizes) })] };
    }

    private static IEnumerable<Ir.Op> Reads(Ir.Ins ins) => ins switch
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
        Ir.LoadIdx loadIdx => [loadIdx.Index],
        Ir.StoreIdx storeIdx => [storeIdx.Index, storeIdx.Value],
        _ => [],
    };

    private static Ir.Cell? Written(Ir.Ins ins) => ins switch
    {
        Ir.Mov mov => mov.Dst,
        Ir.Bin bin => bin.Dst,
        Ir.Un un => un.Dst,
        Ir.Load load => load.Dst,
        Ir.Call call => call.Result,
        Ir.LoadIdx loadIdx => loadIdx.Dst,
        _ => null,
    };

    private static bool Uses(Ir.Ins ins, string symbol) => Reads(ins).Any(op => op is Ir.Cell cell && cell.Sym == symbol);

    /// <summary>Komórka jest martwa od instrukcji <paramref name="from"/>: zapis albo znacznik instrukcji C ją zabija, odczyt ożywia,
    /// a etykieta, skok i powrót przerywają analizę (zakładamy, że żyje).</summary>
    private static bool DeadFrom(List<Ir.Ins> body, int from, string symbol)
    {
        for (int i = from; i < body.Count; i++)
        {
            Ir.Ins ins = body[i];
            if (ins is Ir.Src)
            {
                return true;
            }

            if (ins is Ir.Label or Ir.Jmp or Ir.Ret or Ir.BrCmp)
            {
                return false;
            }

            if (Uses(ins, symbol))
            {
                return false;
            }

            if (Written(ins) is { } written && written.Sym == symbol)
            {
                return true;
            }
        }

        return true;
    }

    private static List<Ir.Ins> Fuse(List<Ir.Ins> body, Dictionary<string, int> sizes)
    {
        var result = new List<Ir.Ins>(body.Count);
        int i = 0;
        while (i < body.Count)
        {
            if (TryFuse(body, i, sizes, out List<Ir.Ins>? replacement, out int consumed))
            {
                result.AddRange(replacement!);
                i += consumed;
                continue;
            }

            result.Add(body[i]);
            i++;
        }

        return result;
    }

    private static bool TryFuse(List<Ir.Ins> body, int at, Dictionary<string, int> sizes, out List<Ir.Ins>? replacement, out int consumed)
    {
        replacement = null;
        consumed = 0;
        int cursor = at;
        Ir.Cell index;
        int shift = 0;
        Ir.Cell? shifted = null;
        if (body[cursor] is Ir.Bin { Kind: Ir.BinOp.Shl, A: Ir.Cell source, B: Ir.Imm { Value: 1 or 2 or 3 } count, Dst: { W: 2 } shiftedCell } && source.W <= 2 && shiftedCell.Sym != source.Sym)
        {
            index = source;
            shift = count.Value;
            shifted = shiftedCell;
            cursor++;
        }
        else
        {
            index = default!;
        }

        if (cursor >= body.Count || body[cursor] is not Ir.Bin { Kind: Ir.BinOp.Add, Dst: { W: 2 } sum } add)
        {
            return false;
        }

        (Ir.AddrOf? array, Ir.Op? other) = add.A is Ir.AddrOf a1 ? (a1, add.B) : add.B is Ir.AddrOf a2 ? (a2, add.A) : (null, null);
        if (array is null || other is not Ir.Cell offsetCell || offsetCell.W > 2)
        {
            return false;
        }

        if (shifted is not null)
        {
            if (offsetCell.Sym != shifted.Sym)
            {
                return false;
            }
        }
        else
        {
            index = offsetCell;
        }

        if (!sizes.TryGetValue(array.Sym, out int size) || size > MaxArray || sum.Sym == index.Sym || (shifted is not null && sum.Sym == shifted.Sym))
        {
            return false;
        }

        cursor++;
        var fused = new List<Ir.Ins>();
        int uses = 0;
        while (cursor < body.Count)
        {
            Ir.Ins ins = body[cursor];
            if (ins is Ir.Load { Ptr: Ir.Cell pointer } load && pointer.Sym == sum.Sym)
            {
                if (load.Dst.Sym == sum.Sym)
                {
                    break;
                }

                fused.Add(new Ir.LoadIdx(load.Dst, array.Sym, array.Off + load.Off, index, shift, load.Bytes));
                cursor++;
                uses++;
                if (load.Dst.Sym == index.Sym)
                {
                    break;
                }

                continue;
            }

            if (ins is Ir.Store { Ptr: Ir.Cell storePointer } store && storePointer.Sym == sum.Sym && !(store.Value is Ir.Cell v && v.Sym == sum.Sym))
            {
                fused.Add(new Ir.StoreIdx(array.Sym, array.Off + store.Off, index, shift, store.Value, store.Bytes));
                cursor++;
                uses++;
                continue;
            }

            break;
        }

        if (uses == 0 || !DeadFrom(body, cursor, sum.Sym) || (shifted is not null && !DeadFrom(body, cursor, shifted.Sym)))
        {
            return false;
        }

        replacement = fused;
        consumed = cursor - at;
        return true;
    }
}
