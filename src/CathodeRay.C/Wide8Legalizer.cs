namespace CathodeRay.C;

/// <summary>Przebieg IR → IR rozbijający komórki i stałe 64-bitowe (<c>long long</c>) na dwie połówki 32-bitowe (<c>sym</c> i
/// <c>sym+4</c>, kolejność zależy od bajtów celu). Dodawanie i odejmowanie liczy przeniesienie porównaniem połówek, przesunięcia o stałą
/// składa z przesunięć połówek, mnożenie, dzielenie i przesunięcia o zmienną liczbę zamienia na wołania <c>__cc_*64</c> z adresami
/// obiektów (procedury w <c>rt_ll64.c</c>). Działa zaraz po obniżeniu funkcji, więc reszta potoku nie zna szerokości 8. Wartość
/// 64-bitowa nie jest argumentem ani wynikiem w rejestrach: wołający podaje adres kopii, wynik wraca przez bufor <c>cc_retbuf</c>.</summary>
internal sealed class Wide8Legalizer
{
    private readonly bool _bigEndian;

    private readonly Func<string> _label;

    private readonly List<Ir.Ins> _output = [];

    private Wide8Legalizer(bool bigEndian, Func<string> label)
    {
        _bigEndian = bigEndian;
        _label = label;
    }

    /// <summary>Nazwy i rozmiary komórek pomocniczych (wspólne, statyczne, używane wyłącznie w obrębie jednej rozpiski).</summary>
    public static IReadOnlyDictionary<string, int> Scratch { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["__w8a"] = 4,
        ["__w8b"] = 4,
        ["__w8c"] = 1,
        ["__w8d"] = 4,
        ["__w8x0"] = 8,
        ["__w8x1"] = 8,
    };

    private (int Low, int High) Offsets => _bigEndian ? (4, 0) : (0, 4);

    /// <summary>Czy instrukcja używa operandu 64-bitowego.</summary>
    /// <param name="ins">Instrukcja.</param>
    /// <returns>Prawda, gdy wymaga rozbicia.</returns>
    public static bool Touches(Ir.Ins ins) => ins switch
    {
        Ir.Mov mov => WidthOf(mov.Dst) == 8 || WidthOf(mov.Src) == 8,
        Ir.Bin bin => WidthOf(bin.Dst) == 8 || WidthOf(bin.A) == 8 || WidthOf(bin.B) == 8,
        Ir.Un un => WidthOf(un.Dst) == 8 || WidthOf(un.A) == 8,
        Ir.Load load => WidthOf(load.Dst) == 8,
        Ir.Store store => store.Bytes == 8,
        Ir.BrCmp branch => WidthOf(branch.A) == 8 || WidthOf(branch.B) == 8,
        _ => false,
    };

    /// <summary>Rozbija operacje 64-bitowe ciała funkcji.</summary>
    /// <param name="body">Instrukcje.</param>
    /// <param name="bigEndian">Czy cel ma bajty od najstarszego.</param>
    /// <param name="label">Fabryka unikalnych etykiet.</param>
    /// <returns>Instrukcje bez szerokości 8.</returns>
    public static List<Ir.Ins> Run(List<Ir.Ins> body, bool bigEndian, Func<string> label)
    {
        if (!body.Any(Touches))
        {
            return body;
        }

        var pass = new Wide8Legalizer(bigEndian, label);
        foreach (Ir.Ins ins in body)
        {
            pass.Rewrite(ins);
        }

        return pass._output;
    }

    private static int WidthOf(Ir.Op op) => op switch
    {
        Ir.Cell cell => cell.W,
        Ir.Imm imm => imm.W,
        _ => 2,
    };

    private static Ir.Imm Zero() => new(0, 4);

    private static Ir.Cell Scr(string name, int width) => new(name, width);

    private (Ir.Op Low, Ir.Op High) Halves(Ir.Op op)
    {
        switch (op)
        {
            case Ir.Cell { W: 8 } cell:
                var first = new Ir.Cell(cell.Sym, 4);
                var second = new Ir.Cell(cell.Sym + "+4", 4);
                return _bigEndian ? (second, first) : (first, second);
            case Ir.Imm { W: 8 } imm:
                return (new Ir.Imm(imm.Value, 4), new Ir.Imm(imm.High, 4));
            default:
                return (op, Zero());
        }
    }

    private (Ir.Cell Low, Ir.Cell High) CellHalves(Ir.Cell cell) => ((Ir.Cell)Halves(cell).Low, (Ir.Cell)Halves(cell).High);

    private void Emit(Ir.Ins ins) => _output.Add(ins);

    private void Rewrite(Ir.Ins ins)
    {
        switch (ins)
        {
            case Ir.Mov { Dst.W: 8 } mov:
                (Ir.Cell dl, Ir.Cell dh) = CellHalves(mov.Dst);
                (Ir.Op sl, Ir.Op sh) = Halves(mov.Src);
                if (mov.Src is Ir.Cell { W: 8 } same && same.Sym == mov.Dst.Sym)
                {
                    return;
                }

                Emit(new Ir.Mov(dl, sl));
                Emit(new Ir.Mov(dh, sh));
                break;
            case Ir.Mov { Src: Ir.Cell { W: 8 } or Ir.Imm { W: 8 } } mov:
                Emit(new Ir.Mov(mov.Dst, Halves(mov.Src).Low));
                break;
            case Ir.Bin { Dst.W: 8 } bin:
                RewriteBin(bin);
                break;
            case Ir.Bin bin when WidthOf(bin.A) == 8 || WidthOf(bin.B) == 8:
                Emit(bin with { A = Halves(bin.A).Low, B = Halves(bin.B).Low });
                break;
            case Ir.Un { Dst.W: 8 } un:
                RewriteUn(un);
                break;
            case Ir.Un un when WidthOf(un.A) == 8:
                Emit(un with { A = Halves(un.A).Low });
                break;
            case Ir.Load { Dst.W: 8 } load:
                (Ir.Cell ll, Ir.Cell lh) = CellHalves(load.Dst);
                var lowLoad = new Ir.Load(ll, load.Ptr, load.Off + Offsets.Low, 4, load.Volatile);
                var highLoad = new Ir.Load(lh, load.Ptr, load.Off + Offsets.High, 4, load.Volatile);
                if (load.Ptr is Ir.Cell pointer && pointer.Sym == ll.Sym)
                {
                    // wskaźnik leży w młodszej połówce celu: czytamy najpierw drugą połówkę
                    Emit(highLoad);
                    Emit(lowLoad);
                }
                else
                {
                    Emit(lowLoad);
                    Emit(highLoad);
                }

                break;
            case Ir.Store { Bytes: 8 } store:
                (Ir.Op vl, Ir.Op vh) = Halves(store.Value);
                Emit(new Ir.Store(store.Ptr, store.Off + Offsets.Low, vl, 4, store.Volatile));
                Emit(new Ir.Store(store.Ptr, store.Off + Offsets.High, vh, 4, store.Volatile));
                break;
            case Ir.BrCmp branch:
                RewriteBranch(branch);
                break;
            default:
                Emit(ins);
                break;
        }
    }

    private void RewriteBin(Ir.Bin bin)
    {
        (Ir.Cell dl, Ir.Cell dh) = CellHalves(bin.Dst);
        (Ir.Op al, Ir.Op ah) = Halves(bin.A);
        (Ir.Op bl, Ir.Op bh) = Halves(bin.B);
        Ir.Cell t = Scr("__w8a", 4);
        Ir.Cell u = Scr("__w8b", 4);
        Ir.Cell carry = Scr("__w8c", 1);
        switch (bin.Kind)
        {
            case Ir.BinOp.And or Ir.BinOp.Or or Ir.BinOp.Xor:
                Emit(new Ir.Bin(bin.Kind, t, al, bl));
                Emit(new Ir.Bin(bin.Kind, u, ah, bh));
                Emit(new Ir.Mov(dl, t));
                Emit(new Ir.Mov(dh, u));
                break;
            case Ir.BinOp.Add:
            {
                string done = _label();
                Emit(new Ir.Bin(Ir.BinOp.Add, t, al, bl));
                Emit(new Ir.Mov(carry, new Ir.Imm(0, 1)));
                Emit(new Ir.BrCmp(Ir.Cond.Geu, t, al, done));
                Emit(new Ir.Mov(carry, new Ir.Imm(1, 1)));
                Emit(new Ir.Label(done));
                Emit(new Ir.Bin(Ir.BinOp.Add, u, ah, bh));
                Emit(new Ir.Bin(Ir.BinOp.Add, u, u, carry));
                Emit(new Ir.Mov(dl, t));
                Emit(new Ir.Mov(dh, u));
                break;
            }

            case Ir.BinOp.Sub:
            {
                string done = _label();
                Emit(new Ir.Bin(Ir.BinOp.Sub, t, al, bl));
                Emit(new Ir.Mov(carry, new Ir.Imm(0, 1)));
                Emit(new Ir.BrCmp(Ir.Cond.Geu, al, bl, done));
                Emit(new Ir.Mov(carry, new Ir.Imm(1, 1)));
                Emit(new Ir.Label(done));
                Emit(new Ir.Bin(Ir.BinOp.Sub, u, ah, bh));
                Emit(new Ir.Bin(Ir.BinOp.Sub, u, u, carry));
                Emit(new Ir.Mov(dl, t));
                Emit(new Ir.Mov(dh, u));
                break;
            }

            case Ir.BinOp.Shl or Ir.BinOp.Shr or Ir.BinOp.Sar when bin.B is Ir.Imm count:
                ConstantShift(bin.Kind, dl, dh, al, ah, count.Value & 63);
                break;
            case Ir.BinOp.Shl or Ir.BinOp.Shr or Ir.BinOp.Sar:
                string shift = bin.Kind switch { Ir.BinOp.Shl => "__cc_shl64", Ir.BinOp.Shr => "__cc_shr64", _ => "__cc_sar64" };
                Ir.Cell operand = Materialize(bin.A, 0);
                Emit(new Ir.Call(shift, null, [new Ir.AddrOf(bin.Dst.Sym, 0), new Ir.AddrOf(operand.Sym, 0), Halves(bin.B).Low], [2, 2, 1], null));
                break;
            default:
                string name = bin.Kind switch
                {
                    Ir.BinOp.Mul => "__cc_mul64",
                    Ir.BinOp.Div => "__cc_divu64",
                    Ir.BinOp.Mod => "__cc_modu64",
                    Ir.BinOp.DivS => "__cc_divs64",
                    Ir.BinOp.ModS => "__cc_mods64",
                    _ => throw new CCodegenException($"operator {bin.Kind} is not supported for long long."),
                };
                Ir.Cell left = Materialize(bin.A, 0);
                Ir.Cell right = Materialize(bin.B, 1);
                Emit(new Ir.Call(name, null, [new Ir.AddrOf(bin.Dst.Sym, 0), new Ir.AddrOf(left.Sym, 0), new Ir.AddrOf(right.Sym, 0)], [2, 2, 2], null));
                break;
        }
    }

    /// <summary>Operand jako komórka 64-bitowa w pamięci (stałe i węższe wartości trafiają do komórki pomocniczej).</summary>
    private Ir.Cell Materialize(Ir.Op op, int slot)
    {
        if (op is Ir.Cell { W: 8 } cell)
        {
            return cell;
        }

        Ir.Cell scratch = Scr($"__w8x{slot}", 8);
        Rewrite(new Ir.Mov(scratch, op));
        return scratch;
    }

    private void ConstantShift(Ir.BinOp kind, Ir.Cell dl, Ir.Cell dh, Ir.Op al, Ir.Op ah, int n)
    {
        Ir.Cell t = Scr("__w8a", 4);
        Ir.Cell u = Scr("__w8b", 4);
        if (n == 0)
        {
            Emit(new Ir.Mov(t, al));
            Emit(new Ir.Mov(u, ah));
        }
        else if (n >= 32)
        {
            int k = n - 32;
            switch (kind)
            {
                case Ir.BinOp.Shl:
                    Emit(new Ir.Bin(Ir.BinOp.Shl, u, al, new Ir.Imm(k, 1)));
                    Emit(new Ir.Mov(t, Zero()));
                    break;
                case Ir.BinOp.Shr:
                    Emit(new Ir.Bin(Ir.BinOp.Shr, t, ah, new Ir.Imm(k, 1)));
                    Emit(new Ir.Mov(u, Zero()));
                    break;
                default:
                    Emit(new Ir.Bin(Ir.BinOp.Sar, t, ah, new Ir.Imm(k, 1)));
                    Emit(new Ir.Bin(Ir.BinOp.Sar, u, ah, new Ir.Imm(31, 1)));
                    break;
            }
        }
        else
        {
            Ir.Cell mid = Scr("__w8d", 4);
            switch (kind)
            {
                case Ir.BinOp.Shl:
                    Emit(new Ir.Bin(Ir.BinOp.Shl, u, ah, new Ir.Imm(n, 1)));
                    Emit(new Ir.Bin(Ir.BinOp.Shr, mid, al, new Ir.Imm(32 - n, 1)));
                    Emit(new Ir.Bin(Ir.BinOp.Or, u, u, mid));
                    Emit(new Ir.Bin(Ir.BinOp.Shl, t, al, new Ir.Imm(n, 1)));
                    break;
                default:
                    Emit(new Ir.Bin(Ir.BinOp.Shr, t, al, new Ir.Imm(n, 1)));
                    Emit(new Ir.Bin(Ir.BinOp.Shl, mid, ah, new Ir.Imm(32 - n, 1)));
                    Emit(new Ir.Bin(Ir.BinOp.Or, t, t, mid));
                    Emit(new Ir.Bin(kind == Ir.BinOp.Shr ? Ir.BinOp.Shr : Ir.BinOp.Sar, u, ah, new Ir.Imm(n, 1)));
                    break;
            }
        }

        Emit(new Ir.Mov(dl, t));
        Emit(new Ir.Mov(dh, u));
    }

    private void RewriteUn(Ir.Un un)
    {
        (Ir.Cell dl, Ir.Cell dh) = CellHalves(un.Dst);
        (Ir.Op al, Ir.Op ah) = Halves(un.A);
        Ir.Cell t = Scr("__w8a", 4);
        Ir.Cell u = Scr("__w8b", 4);
        if (un.Kind == Ir.UnOp.Cpl)
        {
            Emit(new Ir.Un(Ir.UnOp.Cpl, t, al));
            Emit(new Ir.Un(Ir.UnOp.Cpl, u, ah));
        }
        else
        {
            // -x = ~x + 1: młodsza połowa 0 - al, starsza 0 - ah - (al != 0)
            string done = _label();
            Ir.Cell borrow = Scr("__w8c", 1);
            Emit(new Ir.Bin(Ir.BinOp.Sub, t, Zero(), al));
            Emit(new Ir.Mov(borrow, new Ir.Imm(0, 1)));
            Emit(new Ir.BrCmp(Ir.Cond.Eq, al, Zero(), done));
            Emit(new Ir.Mov(borrow, new Ir.Imm(1, 1)));
            Emit(new Ir.Label(done));
            Emit(new Ir.Bin(Ir.BinOp.Sub, u, Zero(), ah));
            Emit(new Ir.Bin(Ir.BinOp.Sub, u, u, borrow));
        }

        Emit(new Ir.Mov(dl, t));
        Emit(new Ir.Mov(dh, u));
    }

    private void RewriteBranch(Ir.BrCmp branch)
    {
        if (WidthOf(branch.A) != 8 && WidthOf(branch.B) != 8)
        {
            Emit(branch);
            return;
        }

        (Ir.Op al, Ir.Op ah) = Halves(branch.A);
        (Ir.Op bl, Ir.Op bh) = Halves(branch.B);
        string skip = _label();
        switch (branch.C)
        {
            case Ir.Cond.Eq:
                Emit(new Ir.BrCmp(Ir.Cond.Ne, al, bl, skip));
                Emit(new Ir.BrCmp(Ir.Cond.Eq, ah, bh, branch.Target));
                Emit(new Ir.Label(skip));
                break;
            case Ir.Cond.Ne:
                Emit(new Ir.BrCmp(Ir.Cond.Ne, al, bl, branch.Target));
                Emit(new Ir.BrCmp(Ir.Cond.Ne, ah, bh, branch.Target));
                break;
            default:
                // decyduje starsza połowa (z tym samym znakiem porównania), a przy równych połowach młodsza bez znaku
                Ir.Cond strict = branch.C switch
                {
                    Ir.Cond.Le => Ir.Cond.Lt,
                    Ir.Cond.Ge => Ir.Cond.Gt,
                    Ir.Cond.Leu => Ir.Cond.Ltu,
                    Ir.Cond.Geu => Ir.Cond.Gtu,
                    _ => branch.C,
                };
                Ir.Cond low = branch.C switch
                {
                    Ir.Cond.Lt or Ir.Cond.Ltu => Ir.Cond.Ltu,
                    Ir.Cond.Le or Ir.Cond.Leu => Ir.Cond.Leu,
                    Ir.Cond.Gt or Ir.Cond.Gtu => Ir.Cond.Gtu,
                    _ => Ir.Cond.Geu,
                };
                Emit(new Ir.BrCmp(strict, ah, bh, branch.Target));
                Emit(new Ir.BrCmp(Ir.Cond.Ne, ah, bh, skip));
                Emit(new Ir.BrCmp(low, al, bl, branch.Target));
                Emit(new Ir.Label(skip));
                break;
        }
    }
}
