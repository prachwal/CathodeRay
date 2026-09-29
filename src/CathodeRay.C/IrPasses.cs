namespace CathodeRay.C;

/// <summary>Przebiegi optymalizujące na kodzie pośrednim (niezależne od celu).</summary>
internal static class IrPasses
{
    /// <summary>Uruchamia wszystkie przebiegi na ciele funkcji.</summary>
    /// <param name="body">Instrukcje.</param>
    /// <param name="function">Nazwa funkcji: jej komórki lokalne mają przedrostek <c>nazwa__</c> i tylko one podlegają propagacji i usuwaniu.</param>
    /// <returns>Instrukcje po optymalizacji.</returns>
    /// <param name="inlined">Nazwy funkcji wstawionych do tego ciała: ich komórki też są lokalne (używane tylko w wstawionym kodzie).</param>
    /// <param name="volatiles">Symbole <c>volatile</c>: nie są propagowane ani usuwane.</param>
    public static List<Ir.Ins> Optimize(List<Ir.Ins> body, string function, IReadOnlySet<string>? inlined = null, IReadOnlySet<string>? volatiles = null) =>
        RemoveDead(Propagate(ForwardTemporaries(body), function, inlined, volatiles), function, inlined, volatiles);

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

    private static bool IsLocal(string symbol, string function, IReadOnlySet<string>? inlined = null) =>
        symbol.StartsWith(function + "__", StringComparison.Ordinal) || (inlined?.Any(name => symbol.StartsWith(name + "__", StringComparison.Ordinal)) ?? false);

    private static long Mask(int width) => width == 1 ? 0xFF : width == 2 ? 0xFFFF : 0xFFFFFFFFL;

    /// <summary>Symbol obiektu bez przesunięcia połówki (<c>x+4</c> to część obiektu <c>x</c>).</summary>
    private static string BaseSymbol(string symbol)
    {
        int plus = symbol.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? symbol : symbol[..plus];
    }

    private static HashSet<string> AddressTaken(List<Ir.Ins> body)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (Ir.Ins ins in body)
        {
            foreach (Ir.Op op in ReadOperands(ins))
            {
                if (op is Ir.AddrOf address)
                {
                    taken.Add(address.Sym);
                }
            }
        }

        return taken;
    }

    /// <summary>Propagacja stałych, adresów i kopii w bloku podstawowym oraz składanie działań na stałych. Dotyczy tylko komórek lokalnych funkcji,
    /// których adres nie jest brany (nie mogą być zmienione przez wskaźnik ani wołanie).</summary>
    private static List<Ir.Ins> Propagate(List<Ir.Ins> body, string function, IReadOnlySet<string>? inlined, IReadOnlySet<string>? volatiles)
    {
        HashSet<string> taken = AddressTaken(body);
        bool Tracked(string sym) => IsLocal(sym, function, inlined) && !taken.Contains(BaseSymbol(sym)) && volatiles?.Contains(BaseSymbol(sym)) != true;
        var known = new Dictionary<string, Ir.Op>(StringComparer.Ordinal);
        var result = new List<Ir.Ins>(body.Count);

        Ir.Op Substitute(Ir.Op op, bool pointerPosition, string? written = null)
        {
            if (op is Ir.Cell cell && Tracked(cell.Sym) && known.TryGetValue(cell.Sym, out Ir.Op? value))
            {
                switch (value)
                {
                    case Ir.Imm imm when !pointerPosition:
                        return new Ir.Imm((int)(imm.Value & Mask(cell.W)), cell.W);
                    case Ir.AddrOf address when cell.W == 2:
                        return address;
                    case Ir.Cell other when other.W == cell.W && other.Sym != written:
                        return other;
                }
            }

            return op;
        }

        void Kill(string sym)
        {
            known.Remove(sym);
            foreach (string key in known.Where(pair => pair.Value is Ir.Cell c && c.Sym == sym).Select(static pair => pair.Key).ToList())
            {
                known.Remove(key);
            }
        }

        foreach (Ir.Ins original in body)
        {
            Ir.Ins ins = original switch
            {
                Ir.Mov mov => mov with { Src = Substitute(mov.Src, false, mov.Dst.Sym) },
                Ir.Bin bin => bin with { A = Substitute(bin.A, false, bin.Dst.Sym), B = Substitute(bin.B, false, bin.Dst.Sym) },
                Ir.Un un => un with { A = Substitute(un.A, false, un.Dst.Sym) },
                Ir.Load load => load with { Ptr = Substitute(load.Ptr, true, load.Dst.Sym) },
                Ir.Store store => store with { Ptr = Substitute(store.Ptr, true), Value = Substitute(store.Value, false) },
                Ir.BrCmp branch => branch with { A = Substitute(branch.A, false), B = Substitute(branch.B, false) },
                Ir.Call call => call with { Args = [.. call.Args.Select(a => Substitute(a, false))] },
                Ir.Ret { Value: not null } ret => ret with { Value = Substitute(ret.Value, false) },
                _ => original,
            };
            ins = Fold(ins);
            if (ins is Ir.Jmp or Ir.Label)
            {
                // wejście z innego miejsca albo skok: nic nie wiadomo
                if (ins is Ir.Label)
                {
                    known.Clear();
                }

                result.Add(ins);
                continue;
            }

            if (ins is Ir.BrCmp { A: Ir.Imm ca, B: Ir.Imm cb } decided)
            {
                // wynik znany w czasie kompilacji: skok bezwarunkowy albo nic
                if (Compare(decided.C, ca, cb))
                {
                    result.Add(new Ir.Jmp(decided.Target));
                }

                continue;
            }

            if (Defined(ins) is { } defined)
            {
                Kill(defined.Sym);
                if (ins is Ir.Mov { Src: var source } && Tracked(defined.Sym))
                {
                    switch (source)
                    {
                        case Ir.Imm imm:
                            known[defined.Sym] = imm;
                            break;
                        case Ir.AddrOf when defined.W == 2:
                            known[defined.Sym] = source;
                            break;
                        case Ir.Cell copy when copy.W == defined.W && copy.Sym != defined.Sym && Tracked(copy.Sym):
                            known[defined.Sym] = copy;
                            break;
                    }
                }
            }

            if (ins is Ir.Call)
            {
                known.Clear();
            }

            if (ins is Ir.Mov { Src: Ir.Cell same } self && Defined(ins) is { } target && same.Sym == target.Sym && same.W == target.W)
            {
                continue;
            }

            result.Add(ins);
        }

        return result;
    }

    /// <summary>Składa działania na dwóch stałych, dodawanie stałej do adresu i skok warunkowy o znanym wyniku.</summary>
    private static Ir.Ins Fold(Ir.Ins ins)
    {
        switch (ins)
        {
            case Ir.Bin { A: Ir.Imm a, B: Ir.Imm b } bin when Evaluate(bin.Kind, a, b, bin.Dst.W) is { } value:
                return new Ir.Mov(bin.Dst, new Ir.Imm((int)value, bin.Dst.W));
            case Ir.Bin { Kind: Ir.BinOp.Add, A: Ir.AddrOf address, B: Ir.Imm offset, Dst.W: 2 } bin:
                return new Ir.Mov(bin.Dst, new Ir.AddrOf(address.Sym, address.Off + (int)(offset.Value & 0xFFFF)));
            case Ir.Bin { Kind: Ir.BinOp.Sub, A: Ir.AddrOf address, B: Ir.Imm offset, Dst.W: 2 } bin:
                return new Ir.Mov(bin.Dst, new Ir.AddrOf(address.Sym, address.Off - (int)(offset.Value & 0xFFFF)));
            case Ir.Un { A: Ir.Imm a } un:
                long v = a.Value & Mask(a.W);
                return new Ir.Mov(un.Dst, new Ir.Imm((int)((un.Kind == Ir.UnOp.Neg ? -v : ~v) & Mask(un.Dst.W)), un.Dst.W));
            default:
                return ins;
        }
    }

    private static bool Compare(Ir.Cond cond, Ir.Imm a, Ir.Imm b)
    {
        int width = Math.Max(a.W, b.W);
        long x = (uint)a.Value & Mask(a.W);
        long y = (uint)b.Value & Mask(b.W);
        if (cond is Ir.Cond.Lt or Ir.Cond.Le or Ir.Cond.Gt or Ir.Cond.Ge)
        {
            x = width == 1 ? (sbyte)x : width == 2 ? (short)x : (int)x;
            y = width == 1 ? (sbyte)y : width == 2 ? (short)y : (int)y;
        }

        return cond switch
        {
            Ir.Cond.Eq => x == y,
            Ir.Cond.Ne => x != y,
            Ir.Cond.Lt or Ir.Cond.Ltu => x < y,
            Ir.Cond.Le or Ir.Cond.Leu => x <= y,
            Ir.Cond.Gt or Ir.Cond.Gtu => x > y,
            _ => x >= y,
        };
    }

    private static long? Evaluate(Ir.BinOp kind, Ir.Imm a, Ir.Imm b, int width)
    {
        long x = (uint)a.Value & Mask(a.W);
        long y = (uint)b.Value & Mask(b.W);
        long? result = kind switch
        {
            Ir.BinOp.Add => x + y,
            Ir.BinOp.Sub => x - y,
            Ir.BinOp.And => x & y,
            Ir.BinOp.Or => x | y,
            Ir.BinOp.Xor => x ^ y,
            Ir.BinOp.Mul => x * y,
            Ir.BinOp.Shl => (y & 0xFF) >= width * 8 ? 0 : x << (int)(y & 0xFF),
            Ir.BinOp.Shr => (y & 0xFF) >= width * 8 ? 0 : x >> (int)(y & 0xFF),
            Ir.BinOp.Div => y == 0 ? 0 : x / y,
            Ir.BinOp.Mod => y == 0 ? 0 : x % y,
            _ => null,
        };
        return result is null ? null : result & Mask(width);
    }

    /// <summary>Usuwa zapisy do komórek lokalnych, których nikt nie czyta (adres nie jest brany), do braku zmian.</summary>
    private static List<Ir.Ins> RemoveDead(List<Ir.Ins> body, string function, IReadOnlySet<string>? inlined, IReadOnlySet<string>? volatiles)
    {
        while (true)
        {
            HashSet<string> taken = AddressTaken(body);
            var reads = new HashSet<string>(StringComparer.Ordinal);
            foreach (Ir.Ins ins in body)
            {
                foreach (Ir.Op op in ReadOperands(ins))
                {
                    if (op is Ir.Cell cell)
                    {
                        reads.Add(cell.Sym);
                    }
                }
            }

            int before = body.Count;
            body = [.. body.Where(ins => ins is Ir.Call or Ir.Load { Volatile: true } || Defined(ins) is not { } d || !IsLocal(d.Sym, function, inlined) || taken.Contains(BaseSymbol(d.Sym)) || reads.Contains(d.Sym) || volatiles?.Contains(BaseSymbol(d.Sym)) == true)];
            if (body.Count == before)
            {
                return body;
            }
        }
    }
}
