namespace CathodeRay.C;

/// <summary>Selektor stuba: operacje na komórkach (kopie, arytmetyka, dostęp przez wskaźnik).</summary>
internal sealed partial class StubSelector
{
    private static string ByteName(Ir.Cell cell, int index) => index == 0 ? cell.Sym : CellHi(cell.Sym);

    private void EmitMov(Ir.Mov mov)
    {
        if (mov.Src is Ir.Cell same && same.Sym == mov.Dst.Sym && same.W >= mov.Dst.W)
        {
            return;
        }

        for (int i = 0; i < mov.Dst.W; i++)
        {
            LoadA(mov.Src, i);
            Line($"STA {ByteName(mov.Dst, i)}");
        }
    }

    private void EmitBin(Ir.Bin bin)
    {
        switch (bin.Kind)
        {
            case Ir.BinOp.Add:
                EmitAdd(bin);
                break;
            case Ir.BinOp.Sub:
                EmitSub(bin);
                break;
            case Ir.BinOp.And:
                EmitLogic(bin, "AND");
                break;
            case Ir.BinOp.Or:
                EmitLogic(bin, "ORA");
                break;
            case Ir.BinOp.Xor:
                EmitLogic(bin, "EOR");
                break;
            case Ir.BinOp.Shl or Ir.BinOp.Shr or Ir.BinOp.Sar:
                EmitShift(bin);
                break;
            default:
                EmitMulDiv(bin);
                break;
        }
    }

    private void EmitAdd(Ir.Bin bin)
    {
        int n = bin.Dst.W;
        Octet b0 = ByteOf(bin.B, 0);
        Octet b1 = ByteOf(bin.B, 1);
        if (NeedsIndex(b0) || (n == 2 && NeedsIndex(b1)))
        {
            Line("LDX 0");
        }

        LoadA(bin.A, 0);
        Alu("ADD", b0);
        Line($"STA {bin.Dst.Sym}");
        if (n == 2)
        {
            LoadA(bin.A, 1);
            Alu("ADC", b1);
            Line($"STA {CellHi(bin.Dst.Sym)}");
        }
    }

    private void EmitLogic(Ir.Bin bin, string op)
    {
        int n = bin.Dst.W;
        if (NeedsIndex(ByteOf(bin.B, 0)) || (n == 2 && NeedsIndex(ByteOf(bin.B, 1))))
        {
            Line("LDX 0");
        }

        for (int i = 0; i < n; i++)
        {
            LoadA(bin.A, i);
            Alu(op, ByteOf(bin.B, i));
            Line($"STA {ByteName(bin.Dst, i)}");
        }
    }

    private void EmitSub(Ir.Bin bin)
    {
        Octet b0 = ByteOf(bin.B, 0);
        SubOrCompare("SUB", b0, () => LoadA(bin.A, 0));
        Line($"STA {bin.Dst.Sym}");
        if (bin.Dst.W == 1)
        {
            return;
        }

        Octet b1 = ByteOf(bin.B, 1);
        string noBorrow = Label("nb");
        string end = Label("sube");
        Line($"BCS {noBorrow}");
        SubOrCompare("SUB", b1, () => LoadA(bin.A, 1));
        Line("SUB 1");
        Line($"STA {CellHi(bin.Dst.Sym)}");
        Line($"JMP {end}");
        Line($"{noBorrow}:");
        SubOrCompare("SUB", b1, () => LoadA(bin.A, 1));
        Line($"STA {CellHi(bin.Dst.Sym)}");
        Line($"{end}:");
    }

    private void EmitShift(Ir.Bin bin)
    {
        Ir.Cell dst = bin.Dst;
        if (bin.B is Ir.Imm { Value: 0 })
        {
            EmitMov(new Ir.Mov(dst, bin.A));
            return;
        }

        if (bin.B is not Ir.Imm)
        {
            LoadA(bin.B, 0);
            Line("STA __x@0");
        }

        EmitMov(new Ir.Mov(dst, bin.A));
        if (bin.B is Ir.Imm { Value: <= 3 } small)
        {
            for (int i = 0; i < small.Value; i++)
            {
                ShiftStep(bin.Kind, dst);
            }

            return;
        }

        if (bin.B is Ir.Imm count)
        {
            Line($"LDI {count.Value & 0xFF}");
            Line("STA __x@0");
        }

        string loop = Label("sh");
        string done = Label("shd");
        Line($"{loop}:");
        Line("LDA __x@0");
        Line("CPA 0");
        Line($"BEQ {done}");
        ShiftStep(bin.Kind, dst);
        Line("LDA __x@0");
        Line("SUB 1");
        Line("STA __x@0");
        Line($"JMP {loop}");
        Line($"{done}:");
    }

    /// <summary>Jedno przesunięcie o bit komórki w miejscu (16-bit: dodawanie do siebie / SHR z przeniesieniem).</summary>
    private void ShiftStep(Ir.BinOp kind, Ir.Cell dst)
    {
        string lo = dst.Sym;
        string hi = CellHi(dst.Sym);
        if (dst.W == 1)
        {
            Line($"LDA {lo}");
            Line(kind == Ir.BinOp.Shl ? "SHL" : "SHR");
            Line($"STA {lo}");
            return;
        }

        if (kind == Ir.BinOp.Shl)
        {
            Line("LDX 0");
            Line($"LDA {lo}");
            Line($"ADD {lo},X");
            Line($"STA {lo}");
            Line($"LDA {hi}");
            Line($"ADC {hi},X");
            Line($"STA {hi}");
            return;
        }

        string carry = Label("shc");
        string next = Label("shn");
        if (kind == Ir.BinOp.Sar)
        {
            Line($"LDA {hi}");
            Line("AND 128");
            Line("STA __x@1");
        }

        Line($"LDA {hi}");
        Line("SHR");
        Line($"STA {hi}");
        Line($"BCS {carry}");
        Line($"LDA {lo}");
        Line("SHR");
        Line($"STA {lo}");
        Line($"JMP {next}");
        Line($"{carry}:");
        Line($"LDA {lo}");
        Line("SHR");
        Line("ADD 128");
        Line($"STA {lo}");
        Line($"{next}:");
        if (kind == Ir.BinOp.Sar)
        {
            Line("LDX 0");
            Line($"LDA {hi}");
            Line("ADD __x@1,X");
            Line($"STA {hi}");
        }
    }

    private void EmitMulDiv(Ir.Bin bin)
    {
        Ir.Cell dst = bin.Dst;
        bool isMul = bin.Kind == Ir.BinOp.Mul;
        bool isMod = bin.Kind is Ir.BinOp.Mod or Ir.BinOp.ModS;
        if (dst.W == 1)
        {
            if (isMul)
            {
                _needMul = true;
            }
            else
            {
                _needDiv = true;
            }

            LoadA(bin.A, 0);
            Line("STA __x@0");
            Octet divisor = ByteOf(bin.B, 0);
            if (divisor.IsImmediate)
            {
                Line($"LDX {divisor.Text}");
            }
            else
            {
                LoadA(divisor);
                Line("TAX");
            }

            Line("LDA __x@0");
            Line(isMul ? "CALL cc_mul8" : "CALL cc_divmod");
            if (isMod)
            {
                Line("TXA");
            }

            Line($"STA {dst.Sym}");
            return;
        }

        LoadA(bin.A, 0);
        Line("STA cc_w_a");
        LoadA(bin.A, 1);
        Line("STA cc_w_a_h");
        LoadA(bin.B, 0);
        Line("STA cc_w_b");
        LoadA(bin.B, 1);
        Line("STA cc_w_b_h");
        if (isMul)
        {
            _needMul16 = true;
            Line("CALL cc_mul16");
        }
        else
        {
            _needDiv16 = true;
            Line(bin.Kind is Ir.BinOp.DivS or Ir.BinOp.ModS ? "CALL cc_sdiv16" : "CALL cc_div16");
        }

        if (isMod)
        {
            Line("LDA cc_w_r");
            Line($"STA {dst.Sym}");
            Line("LDA cc_w_r_h");
            Line($"STA {CellHi(dst.Sym)}");
            return;
        }

        Line($"STA {dst.Sym}");
        Line("TXA");
        Line($"STA {CellHi(dst.Sym)}");
    }

    private void EmitUn(Ir.Un un)
    {
        Ir.Cell dst = un.Dst;
        if (un.Kind == Ir.UnOp.Cpl)
        {
            for (int i = 0; i < dst.W; i++)
            {
                LoadA(un.A, i);
                Line("NOT");
                Line($"STA {ByteName(dst, i)}");
            }

            return;
        }

        if (dst.W == 1)
        {
            LoadA(un.A, 0);
            Line("NOT");
            Line("INC");
            Line($"STA {dst.Sym}");
            return;
        }

        string done = Label("neg");
        LoadA(un.A, 1);
        Line("NOT");
        Line($"STA {CellHi(dst.Sym)}");
        LoadA(un.A, 0);
        Line("NOT");
        Line("INC");
        Line($"STA {dst.Sym}");
        Line($"BNE {done}");
        Line($"LDA {CellHi(dst.Sym)}");
        Line("INC");
        Line($"STA {CellHi(dst.Sym)}");
        Line($"{done}:");
    }

    /// <summary>Adres bazowy <c>ptr + off</c> jako dwa bajty (przy przesunięciu liczony do komórki <paramref name="scratch"/>).</summary>
    private (Octet Lo, Octet Hi) BaseAddress(Ir.Op pointer, int offset, string scratch)
    {
        if (offset == 0)
        {
            return (ByteOf(pointer, 0), ByteOf(pointer, 1));
        }

        LoadA(pointer, 0);
        Line($"ADD {offset & 0xFF}");
        Line($"STA {scratch}");
        LoadA(pointer, 1);
        Line($"ADC {(offset >> 8) & 0xFF}");
        Line($"STA {scratch}+1");
        return (new Octet(false, scratch), new Octet(false, $"{scratch}+1"));
    }

    private void EmitLoad(Ir.Load load)
    {
        Ir.Cell dst = load.Dst;
        if (load.Ptr is Ir.AddrOf direct)
        {
            for (int i = 0; i < load.Bytes; i++)
            {
                Line($"LDA {At(direct.Sym, direct.Off + load.Off + i)}");
                Line($"STA {ByteName(dst, i)}");
            }

            ZeroExtend(dst, load.Bytes);
            return;
        }

        (Octet lo, Octet hi) = BaseAddress(load.Ptr, load.Off, "__p@0");
        if (load.Bytes == 2)
        {
            LoadA(lo);
            Line("ADD 1");
            Line("STA __p@1");
            LoadA(hi);
            Line("ADC 0");
            Line("STA __p@1+1");
        }

        string site = Label("ld");
        LoadA(lo);
        Line($"STA {site}+1");
        LoadA(hi);
        Line($"STA {site}+2");
        Line($"{site}: LDA 0");
        Line($"STA {dst.Sym}");
        if (load.Bytes == 2)
        {
            string siteHi = Label("ld");
            Line("LDA __p@1");
            Line($"STA {siteHi}+1");
            Line("LDA __p@1+1");
            Line($"STA {siteHi}+2");
            Line($"{siteHi}: LDA 0");
            Line($"STA {CellHi(dst.Sym)}");
            return;
        }

        ZeroExtend(dst, 1);
    }

    private void ZeroExtend(Ir.Cell dst, int bytes)
    {
        for (int i = bytes; i < dst.W; i++)
        {
            Line("LDI 0");
            Line($"STA {ByteName(dst, i)}");
        }
    }

    private void EmitStore(Ir.Store store)
    {
        if (store.Ptr is Ir.AddrOf direct)
        {
            for (int i = 0; i < store.Bytes; i++)
            {
                LoadA(store.Value, i);
                Line($"STA {At(direct.Sym, direct.Off + store.Off + i)}");
            }

            return;
        }

        (Octet lo, Octet hi) = BaseAddress(store.Ptr, store.Off, "__p@0");
        if (store.Bytes == 2)
        {
            LoadA(lo);
            Line("ADD 1");
            Line("STA __p@1");
            LoadA(hi);
            Line("ADC 0");
            Line("STA __p@1+1");
        }

        string site = Label("st");
        LoadA(lo);
        Line($"STA {site}+1");
        LoadA(hi);
        Line($"STA {site}+2");
        LoadA(store.Value, 0);
        Line($"{site}: STA 0");
        if (store.Bytes == 2)
        {
            string siteHi = Label("st");
            Line("LDA __p@1");
            Line($"STA {siteHi}+1");
            Line("LDA __p@1+1");
            Line($"STA {siteHi}+2");
            LoadA(store.Value, 1);
            Line($"{siteHi}: STA 0");
        }
    }

    private void EmitCopy(Ir.CopyBlock copy)
    {
        if (copy.Size == 0)
        {
            return;
        }

        LoadA(copy.Dst, 0);
        Line("STA __p@2");
        LoadA(copy.Dst, 1);
        Line("STA __p@2+1");
        LoadA(copy.Src, 0);
        Line("STA __p@3");
        LoadA(copy.Src, 1);
        Line("STA __p@3+1");
        Line($"LDI {copy.Size & 0xFF}");
        Line("STA __c@0");
        Line($"LDI {(copy.Size >> 8) & 0xFF}");
        Line("STA __c@0+1");
        string loop = Label("copy");
        string load = Label("cld");
        string store = Label("cst");
        string noBorrow = Label("cnb");
        Line($"{loop}:");
        Line("LDA __p@3");
        Line($"STA {load}+1");
        Line("LDA __p@3+1");
        Line($"STA {load}+2");
        Line($"{load}: LDA 0");
        Line("STA __x@0");
        Line("LDA __p@2");
        Line($"STA {store}+1");
        Line("LDA __p@2+1");
        Line($"STA {store}+2");
        Line("LDA __x@0");
        Line($"{store}: STA 0");
        foreach (string pointer in new[] { "__p@3", "__p@2" })
        {
            Line($"LDA {pointer}");
            Line("ADD 1");
            Line($"STA {pointer}");
            Line($"LDA {pointer}+1");
            Line("ADC 0");
            Line($"STA {pointer}+1");
        }

        Line("LDA __c@0");
        Line("SUB 1");
        Line("STA __c@0");
        Line($"BCS {noBorrow}");
        Line("LDA __c@0+1");
        Line("SUB 1");
        Line("STA __c@0+1");
        Line($"{noBorrow}: LDX 0");
        Line("LDA __c@0");
        Line("ORA __c@0+1,X");
        Line($"BNE {loop}");
    }

    private void EmitFill(Ir.Fill fill)
    {
        if (fill.Dst is not Ir.AddrOf target)
        {
            throw new InvalidOperationException("Fill needs a constant address.");
        }

        Line($"LDI {fill.Value & 0xFF}");
        for (int page = 0; page * 256 < fill.Size; page++)
        {
            int length = Math.Min(256, fill.Size - (page * 256));
            string loop = Label("fill");
            Line("LDX 0");
            Line($"{loop}: STA {At(target.Sym, target.Off + (page * 256))},X");
            Line("INX");
            Line($"CPX {length & 0xFF}");
            Line($"BNE {loop}");
        }
    }

    private void EmitCall(Ir.Call call)
    {
        int count = call.Args.Count;
        for (int i = 1; i < count; i++)
        {
            (string lo, string? hi) = ArgCells(call.ParamWidths, i);
            if (lo == "cc_arg1_h")
            {
                continue;
            }

            LoadA(call.Args[i], 0);
            Line($"STA {lo}");
            if (hi is not null)
            {
                LoadA(call.Args[i], 1);
                Line($"STA {hi}");
            }
        }

        string site = Label("icall");
        if (call.Indirect is not null)
        {
            LoadA(call.Indirect, 0);
            Line($"STA {site}+1");
            LoadA(call.Indirect, 1);
            Line($"STA {site}+2");
        }

        if (count >= 1 && call.ParamWidths[0] == 2)
        {
            LoadA(call.Args[0], 1);
            Line("TAX");
        }
        else if (count >= 2 && ArgCells(call.ParamWidths, 1).Lo == "cc_arg1_h")
        {
            LoadA(call.Args[1], 0);
            Line("TAX");
        }

        if (count >= 1)
        {
            LoadA(call.Args[0], 0);
        }

        Line(call.Direct is not null ? $"CALL {call.Direct}" : $"{site}: CALL 0");
        if (call.Result is not null)
        {
            Line($"STA {call.Result.Sym}");
            if (call.Result.W == 2)
            {
                Line("TXA");
                Line($"STA {CellHi(call.Result.Sym)}");
            }
        }
    }

    private void EmitRet(Ir.Function function, Ir.Ret ret)
    {
        if (ret.Value is not null)
        {
            if (ret.W == 2)
            {
                LoadA(ret.Value, 1);
                Line("TAX");
            }

            LoadA(ret.Value, 0);
        }

        Line($"JMP {function.Name}__ret");
    }
}
