namespace CathodeRay.C;

/// <summary>Przebieg IR → IR rozbijający komórki i stałe 32-bitowe (<c>long</c>/<c>ulong</c>) na dwie połówki 16-bitowe, żeby
/// selektory znały tylko szerokości 1 i 2. Połówki to komórki <c>sym</c> i <c>sym+2</c> (kolejność zależy od bajtów celu);
/// przeniesienie i pożyczka wychodzą z porównania połówek. Mnożenie, dzielenie i przesunięcia o zmienną liczbę zamienia wcześniej
/// <see cref="Legalizer"/> na wołania <c>__cc_*32</c>. Argument 32-bitowy to dwa argumenty 16-bitowe (młodszy, starszy), a wynik
/// wraca młodszą połową w <c>cc_ret</c> i starszą w <c>cc_rethi</c>.</summary>
internal sealed class WideLegalizer
{
    /// <summary>Komórka z starszą połową wyniku 32-bitowego (definiuje crt0).</summary>
    public const string ReturnHigh = "cc_rethi";

    private readonly Ir.Module _module;

    private readonly bool _bigEndian;

    /// <summary>Zostawia 32-bitowe Mov/Add/Sub/And/Or/Xor/Neg/Cpl (selektor bajtowy liczy je łańcuchem 4 bajtów z przeniesieniem).</summary>
    private readonly bool _keepArithmetic;

    private readonly List<Ir.Data> _temps = [];

    private readonly HashSet<string> _tempNames = new(StringComparer.Ordinal);

    private bool _usesReturnHigh;

    private int _labels;

    private WideLegalizer(Ir.Module module, bool bigEndian, bool keepArithmetic)
    {
        _module = module;
        _bigEndian = bigEndian;
        _keepArithmetic = keepArithmetic;
    }

    /// <summary>Przesunięcie połówki względem adresu obiektu 32-bitowego: młodsza połowa leży pod niższym adresem w LE, pod wyższym w BE.</summary>
    private (int Low, int High) HalfOffsets => _bigEndian ? (2, 0) : (0, 2);

    /// <summary>Rozbija operacje 32-bitowe modułu na 16-bitowe.</summary>
    /// <param name="module">Moduł po <see cref="Legalizer"/>.</param>
    /// <param name="byteOrder">Kolejność bajtów celu (układ połówek w pamięci).</param>
    /// <param name="keepArithmetic"><see langword="true"/>: 32-bitowe Mov/Add/Sub/And/Or/Xor/Neg/Cpl zostają (cele bajtowe liczą je
    /// łańcuchem bajtów); rozbijane są dalej wołania, powroty, dostępy przez wskaźnik, porównania i przesunięcia.</param>
    /// <returns>Moduł bez komórek i stałych 32-bitowych (poza zostawioną arytmetyką).</returns>
    public static Ir.Module Run(Ir.Module module, TargetByteOrder byteOrder, bool keepArithmetic = false)
    {
        ArgumentNullException.ThrowIfNull(module);
        return new WideLegalizer(module, byteOrder == TargetByteOrder.Big, keepArithmetic).Apply();
    }

    private static int WidthOf(Ir.Op op) => op switch
    {
        Ir.Cell cell => cell.W,
        Ir.Imm imm => imm.W,
        _ => 2,
    };

    private static bool IsWide(Ir.Op? op) => op is not null && WidthOf(op) == 4;

    private static Ir.Cond Unsigned(Ir.Cond cond) => cond switch
    {
        Ir.Cond.Lt => Ir.Cond.Ltu,
        Ir.Cond.Le => Ir.Cond.Leu,
        Ir.Cond.Gt => Ir.Cond.Gtu,
        Ir.Cond.Ge => Ir.Cond.Geu,
        _ => cond,
    };

    private static Ir.Cond Strict(Ir.Cond cond) => cond switch
    {
        Ir.Cond.Le => Ir.Cond.Lt,
        Ir.Cond.Ge => Ir.Cond.Gt,
        Ir.Cond.Leu => Ir.Cond.Ltu,
        Ir.Cond.Geu => Ir.Cond.Gtu,
        _ => cond,
    };

    private static Ir.Imm Zero() => new(0, 2);

    private Ir.Module Apply()
    {
        var functions = new List<Ir.Function>();
        foreach (Ir.Function function in _module.Functions)
        {
            var body = new List<Ir.Ins>();
            foreach (Ir.Ins ins in function.Body)
            {
                Rewrite(ins, body);
            }

            var parameters = new List<Ir.Cell>();
            foreach (Ir.Cell param in function.Params)
            {
                if (param.W == 4)
                {
                    (Ir.Cell low, Ir.Cell high) = Halves(param);
                    parameters.Add(low);
                    parameters.Add(high);
                }
                else
                {
                    parameters.Add(param);
                }
            }

            functions.Add(function with { Params = parameters, RetW = function.RetW == 4 ? 2 : function.RetW, Body = body });
        }

        IReadOnlyList<string> externCells = _usesReturnHigh && !_module.ExternCells.Contains(ReturnHigh) ? [.. _module.ExternCells, ReturnHigh] : _module.ExternCells;
        return _module with { Functions = functions, Data = [.. _module.Data, .. _temps], ExternCells = externCells };
    }

    /// <summary>Młodsza i starsza połowa komórki 32-bitowej.</summary>
    private (Ir.Cell Low, Ir.Cell High) Halves(Ir.Cell cell)
    {
        var first = new Ir.Cell(cell.Sym, 2);
        var second = new Ir.Cell(cell.Sym + "+2", 2);
        return _bigEndian ? (second, first) : (first, second);
    }

    /// <summary>Połówki operandu: 32-bitowy dzieli się na dwie 16-bitowe, węższy ma młodszą połowę równą sobie, a starszą zerem.</summary>
    private (Ir.Op Low, Ir.Op High) Halves(Ir.Op op)
    {
        switch (op)
        {
            case Ir.Cell { W: 4 } cell:
                (Ir.Cell low, Ir.Cell high) = Halves(cell);
                return (low, high);
            case Ir.Imm { W: 4 } imm:
                return (new Ir.Imm(imm.Value & 0xFFFF, 2), new Ir.Imm((imm.Value >> 16) & 0xFFFF, 2));
            default:
                return (op, Zero());
        }
    }

    private Ir.Cell Temp(int slot, int width = 2)
    {
        string name = width == 1 ? $"__wc{slot}" : $"__wl{slot}";
        if (_tempNames.Add(name))
        {
            _temps.Add(new Ir.Data(name, "BSS", width, null, false));
        }

        return new Ir.Cell(name, width);
    }

    private string NewLabel() => $"__wl_{++_labels}";

    private void Rewrite(Ir.Ins ins, List<Ir.Ins> output)
    {
        switch (ins)
        {
            case Ir.Mov { Dst.W: 4 } or Ir.Un { Dst.W: 4 } or Ir.Bin { Dst.W: 4, Kind: Ir.BinOp.Add or Ir.BinOp.Sub or Ir.BinOp.And or Ir.BinOp.Or or Ir.BinOp.Xor } when _keepArithmetic:
                output.Add(ins);
                break;
            case Ir.Mov { Dst.W: 4 } mov:
                RewriteMov(mov, output);
                break;
            case Ir.Mov { Src: Ir.Cell { W: 4 } or Ir.Imm { W: 4 } } mov:
                output.Add(new Ir.Mov(mov.Dst, Halves(mov.Src).Low));
                break;
            case Ir.Bin { Dst.W: 4 } bin:
                RewriteBin(bin, output);
                break;
            case Ir.Bin bin when IsWide(bin.A) || IsWide(bin.B):
                output.Add(bin with { A = Halves(bin.A).Low, B = Halves(bin.B).Low });
                break;
            case Ir.Un { Dst.W: 4 } un:
                RewriteUn(un, output);
                break;
            case Ir.Load { Dst.W: 4 } load:
                RewriteLoad(load, output);
                break;
            case Ir.Store { Bytes: 4 } store:
                RewriteStore(store, output);
                break;
            case Ir.BrCmp branch when IsWide(branch.A) || IsWide(branch.B):
                RewriteBranch(branch, output);
                break;
            case Ir.Call call when call.Args.Zip(call.ParamWidths).Any(static p => p.Second == 4) || call.Result is { W: 4 }:
                RewriteCall(call, output);
                break;
            case Ir.Ret { W: 4 } ret:
                RewriteReturn(ret, output);
                break;
            default:
                output.Add(ins);
                break;
        }
    }

    private void RewriteMov(Ir.Mov mov, List<Ir.Ins> output)
    {
        (Ir.Cell dstLow, Ir.Cell dstHigh) = Halves(mov.Dst);
        (Ir.Op low, Ir.Op high) = Halves(mov.Src);
        if (mov.Src is Ir.Cell { W: 4 } same && same.Sym == mov.Dst.Sym)
        {
            return;
        }

        output.Add(new Ir.Mov(dstLow, low));
        output.Add(new Ir.Mov(dstHigh, high));
    }

    private void RewriteBin(Ir.Bin bin, List<Ir.Ins> output)
    {
        (Ir.Cell dl, Ir.Cell dh) = Halves(bin.Dst);
        (Ir.Op al, Ir.Op ah) = Halves(bin.A);
        (Ir.Op bl, Ir.Op bh) = Halves(bin.B);
        switch (bin.Kind)
        {
            case Ir.BinOp.And or Ir.BinOp.Or or Ir.BinOp.Xor:
                output.Add(new Ir.Bin(bin.Kind, dl, al, bl));
                output.Add(new Ir.Bin(bin.Kind, dh, ah, bh));
                break;
            case Ir.BinOp.Add:
            {
                Ir.Cell sum = Temp(0);
                Ir.Cell carry = Temp(0, 1);
                string done = NewLabel();
                output.Add(new Ir.Bin(Ir.BinOp.Add, sum, al, bl));
                output.Add(new Ir.Mov(carry, new Ir.Imm(0, 1)));
                output.Add(new Ir.BrCmp(Ir.Cond.Geu, sum, al, done));
                output.Add(new Ir.Mov(carry, new Ir.Imm(1, 1)));
                output.Add(new Ir.Label(done));
                output.Add(new Ir.Bin(Ir.BinOp.Add, dh, ah, bh));
                output.Add(new Ir.Bin(Ir.BinOp.Add, dh, dh, carry));
                output.Add(new Ir.Mov(dl, sum));
                break;
            }

            case Ir.BinOp.Sub:
            {
                Ir.Cell diff = Temp(0);
                Ir.Cell borrow = Temp(0, 1);
                string done = NewLabel();
                output.Add(new Ir.Mov(borrow, new Ir.Imm(0, 1)));
                output.Add(new Ir.BrCmp(Ir.Cond.Geu, al, bl, done));
                output.Add(new Ir.Mov(borrow, new Ir.Imm(1, 1)));
                output.Add(new Ir.Label(done));
                output.Add(new Ir.Bin(Ir.BinOp.Sub, diff, al, bl));
                output.Add(new Ir.Bin(Ir.BinOp.Sub, dh, ah, bh));
                output.Add(new Ir.Bin(Ir.BinOp.Sub, dh, dh, borrow));
                output.Add(new Ir.Mov(dl, diff));
                break;
            }

            case Ir.BinOp.Shl or Ir.BinOp.Shr or Ir.BinOp.Sar when bin.B is Ir.Imm count:
                RewriteShift(bin.Kind, dl, dh, al, ah, count.Value & 0xFF, output);
                break;
            default:
                throw new InvalidOperationException($"WideLegalizer cannot split {bin.Kind} (Legalizer should have replaced it).");
        }
    }

    private void RewriteShift(Ir.BinOp kind, Ir.Cell dl, Ir.Cell dh, Ir.Op al, Ir.Op ah, int n, List<Ir.Ins> output)
    {
        if (n == 0)
        {
            output.Add(new Ir.Mov(dl, al));
            output.Add(new Ir.Mov(dh, ah));
            return;
        }

        switch (kind)
        {
            case Ir.BinOp.Shl:
                if (n >= 32)
                {
                    output.Add(new Ir.Mov(dl, Zero()));
                    output.Add(new Ir.Mov(dh, Zero()));
                }
                else if (n >= 16)
                {
                    output.Add(new Ir.Bin(Ir.BinOp.Shl, dh, al, new Ir.Imm(n - 16, 1)));
                    output.Add(new Ir.Mov(dl, Zero()));
                }
                else
                {
                    Ir.Cell carried = Temp(0);
                    output.Add(new Ir.Bin(Ir.BinOp.Shr, carried, al, new Ir.Imm(16 - n, 1)));
                    output.Add(new Ir.Bin(Ir.BinOp.Shl, dh, ah, new Ir.Imm(n, 1)));
                    output.Add(new Ir.Bin(Ir.BinOp.Or, dh, dh, carried));
                    output.Add(new Ir.Bin(Ir.BinOp.Shl, dl, al, new Ir.Imm(n, 1)));
                }

                break;
            case Ir.BinOp.Shr:
                if (n >= 32)
                {
                    output.Add(new Ir.Mov(dl, Zero()));
                    output.Add(new Ir.Mov(dh, Zero()));
                }
                else if (n >= 16)
                {
                    output.Add(new Ir.Bin(Ir.BinOp.Shr, dl, ah, new Ir.Imm(n - 16, 1)));
                    output.Add(new Ir.Mov(dh, Zero()));
                }
                else
                {
                    Ir.Cell carried = Temp(0);
                    output.Add(new Ir.Bin(Ir.BinOp.Shl, carried, ah, new Ir.Imm(16 - n, 1)));
                    output.Add(new Ir.Bin(Ir.BinOp.Shr, dl, al, new Ir.Imm(n, 1)));
                    output.Add(new Ir.Bin(Ir.BinOp.Or, dl, dl, carried));
                    output.Add(new Ir.Bin(Ir.BinOp.Shr, dh, ah, new Ir.Imm(n, 1)));
                }

                break;
            default:
                if (n >= 32)
                {
                    output.Add(new Ir.Bin(Ir.BinOp.Sar, dl, ah, new Ir.Imm(15, 1)));
                    output.Add(new Ir.Mov(dh, dl));
                }
                else if (n >= 16)
                {
                    output.Add(new Ir.Bin(Ir.BinOp.Sar, dl, ah, new Ir.Imm(n - 16, 1)));
                    output.Add(new Ir.Bin(Ir.BinOp.Sar, dh, ah, new Ir.Imm(15, 1)));
                }
                else
                {
                    Ir.Cell carried = Temp(0);
                    output.Add(new Ir.Bin(Ir.BinOp.Shl, carried, ah, new Ir.Imm(16 - n, 1)));
                    output.Add(new Ir.Bin(Ir.BinOp.Shr, dl, al, new Ir.Imm(n, 1)));
                    output.Add(new Ir.Bin(Ir.BinOp.Or, dl, dl, carried));
                    output.Add(new Ir.Bin(Ir.BinOp.Sar, dh, ah, new Ir.Imm(n, 1)));
                }

                break;
        }
    }

    private void RewriteUn(Ir.Un un, List<Ir.Ins> output)
    {
        if (un.Kind == Ir.UnOp.Neg)
        {
            RewriteBin(new Ir.Bin(Ir.BinOp.Sub, un.Dst, new Ir.Imm(0, 4), un.A), output);
            return;
        }

        (Ir.Cell dl, Ir.Cell dh) = Halves(un.Dst);
        (Ir.Op al, Ir.Op ah) = Halves(un.A);
        output.Add(new Ir.Un(Ir.UnOp.Cpl, dl, al));
        output.Add(new Ir.Un(Ir.UnOp.Cpl, dh, ah));
    }

    private void RewriteLoad(Ir.Load load, List<Ir.Ins> output)
    {
        (Ir.Cell dl, Ir.Cell dh) = Halves(load.Dst);
        (int lowAt, int highAt) = HalfOffsets;
        Ir.Op pointer = load.Ptr;
        if (pointer is Ir.Cell { Sym: var pointerSym } && pointerSym == load.Dst.Sym)
        {
            // wskaźnik leży w tej samej komórce co wynik: pierwsza połowa zniszczyłaby adres drugiej
            Ir.Cell copy = Temp(1);
            output.Add(new Ir.Mov(copy, pointer));
            pointer = copy;
        }

        if (load.Bytes == 4)
        {
            output.Add(new Ir.Load(dl, pointer, load.Off + lowAt, 2));
            output.Add(new Ir.Load(dh, pointer, load.Off + highAt, 2));
            return;
        }

        output.Add(new Ir.Load(dl, pointer, load.Off, load.Bytes));
        output.Add(new Ir.Mov(dh, Zero()));
    }

    private void RewriteStore(Ir.Store store, List<Ir.Ins> output)
    {
        (Ir.Op low, Ir.Op high) = Halves(store.Value);
        (int lowAt, int highAt) = HalfOffsets;
        output.Add(new Ir.Store(store.Ptr, store.Off + lowAt, low, 2));
        output.Add(new Ir.Store(store.Ptr, store.Off + highAt, high, 2));
    }

    private void RewriteBranch(Ir.BrCmp branch, List<Ir.Ins> output)
    {
        (Ir.Op al, Ir.Op ah) = Halves(branch.A);
        (Ir.Op bl, Ir.Op bh) = Halves(branch.B);
        switch (branch.C)
        {
            case Ir.Cond.Eq:
            {
                string skip = NewLabel();
                output.Add(new Ir.BrCmp(Ir.Cond.Ne, ah, bh, skip));
                output.Add(new Ir.BrCmp(Ir.Cond.Eq, al, bl, branch.Target));
                output.Add(new Ir.Label(skip));
                break;
            }

            case Ir.Cond.Ne:
                output.Add(new Ir.BrCmp(Ir.Cond.Ne, ah, bh, branch.Target));
                output.Add(new Ir.BrCmp(Ir.Cond.Ne, al, bl, branch.Target));
                break;
            default:
            {
                string skip = NewLabel();
                output.Add(new Ir.BrCmp(Strict(branch.C), ah, bh, branch.Target));
                output.Add(new Ir.BrCmp(Ir.Cond.Ne, ah, bh, skip));
                output.Add(new Ir.BrCmp(Unsigned(branch.C), al, bl, branch.Target));
                output.Add(new Ir.Label(skip));
                break;
            }
        }
    }

    private void RewriteCall(Ir.Call call, List<Ir.Ins> output)
    {
        var args = new List<Ir.Op>();
        var widths = new List<int>();
        for (int i = 0; i < call.Args.Count; i++)
        {
            if (call.ParamWidths[i] == 4)
            {
                (Ir.Op low, Ir.Op high) = Halves(call.Args[i]);
                args.Add(low);
                args.Add(high);
                widths.Add(2);
                widths.Add(2);
            }
            else
            {
                args.Add(call.Args[i] is Ir.Cell { W: 4 } or Ir.Imm { W: 4 } ? Halves(call.Args[i]).Low : call.Args[i]);
                widths.Add(call.ParamWidths[i]);
            }
        }

        if (call.Result is { W: 4 } result)
        {
            _usesReturnHigh = true;
            (Ir.Cell low, Ir.Cell high) = Halves(result);
            output.Add(call with { Args = args, ParamWidths = widths, Result = low });
            output.Add(new Ir.Mov(high, new Ir.Cell(ReturnHigh, 2)));
            return;
        }

        output.Add(call with { Args = args, ParamWidths = widths });
    }

    private void RewriteReturn(Ir.Ret ret, List<Ir.Ins> output)
    {
        if (ret.Value is null)
        {
            output.Add(ret with { W = 2 });
            return;
        }

        _usesReturnHigh = true;
        (Ir.Op low, Ir.Op high) = Halves(ret.Value);
        output.Add(new Ir.Mov(new Ir.Cell(ReturnHigh, 2), high));
        output.Add(new Ir.Ret(low, 2));
    }
}
