using System.Globalization;
using System.Text;

namespace CathodeRay.C;

internal sealed partial class ByteSelector
{
    /// <summary>Emuluje instrukcję; zwraca <see langword="true"/>, gdy wynik słowa został w rejestrze wyniku
    /// (ścieżka <see cref="IWordArithmetic.TryAddWord"/>: pętla może pominąć ładowanie do <c>Ret</c>).</summary>
    private WordResult EmitIns(Ir.Function function, Ir.Ins ins, bool last)
    {
        switch (ins)
        {
            case Ir.Src source:
                EmitSource(source);
                return new(false, false);
            case Ir.Label label:
                Raw($"{Mangle(function, label.Name)}:");
                return new(false, false);
            case Ir.Jmp jump:
                _isa.Jump(Mangle(function, jump.Target));
                return new(false, false);
            case Ir.Mov mov:
                EmitMov(mov.Dst, mov.Src);
                return new(false, false);
            case Ir.Bin bin:
                return EmitBin(bin);
            case Ir.Un un:
                EmitUn(un);
                return new(false, false);
            case Ir.Load load:
                EmitLoad(load);
                return new(false, false);
            case Ir.Store store:
                EmitStore(store);
                return new(false, false);
            case Ir.LoadIdx loadIdx:
                EmitLoadIdx(loadIdx);
                return new(false, false);
            case Ir.StoreIdx storeIdx:
                EmitStoreIdx(storeIdx);
                return new(false, false);
            case Ir.BrCmp branch:
                EmitBranch(function, branch);
                return new(false, false);
            case Ir.Call call:
                EmitCall(call);
                return new(false, false);
            case Ir.Ret ret:
                EmitRet(function, ret, last);
                return new(false, false);
            default:
                throw new InvalidOperationException($"ByteSelector cannot select {ins.GetType().Name} (Legalizer should have removed it).");
        }
    }

    private void EmitMov(Ir.Cell dst, Ir.Op src)
    {
        if (src is Ir.Cell same && same.Sym == dst.Sym && same.W == dst.W)
        {
            return;
        }

        if (dst.W == 2 && TryMoveWord(WordOf(dst), WordOf(src)))
        {
            return;
        }

        for (int i = 0; i < dst.W; i++)
        {
            if (dst.W == 4 && i % 2 == 0 && TryMoveWord(HalfOf(dst, i / 2), HalfOf(src, i / 2)))
            {
                i++;
                continue;
            }

            LoadA(ByteOf(src, i));
            StoreA(Dst(dst, i));
        }
    }

    private WordResult EmitBin(Ir.Bin bin)
    {
        if (bin.Kind is Ir.BinOp.Add or Ir.BinOp.Sub && bin.Dst.W <= 2 && bin.A is Ir.Cell same && same.Sym == bin.Dst.Sym && same.W == bin.Dst.W && bin.B is Ir.Imm { Value: 1 })
        {
            string[] cells = [.. Enumerable.Range(0, bin.Dst.W).Select(i => Dst(bin.Dst, i))];
            if (_isa.TryStep(cells, bin.Kind == Ir.BinOp.Add))
            {
                _acc.Clear();
                return new(false, false);
            }
        }

        // Optymalizacja: dodawanie 16-bitowe stałej z zerowym bajtem starszym (tylko 6502): zamiast adc #0 użyj bcc skip; inc hi
        if (bin.Kind == Ir.BinOp.Add && bin.Dst.W == 2 && bin.A is Ir.Cell sameCell && sameCell.Sym == bin.Dst.Sym && sameCell.W == 2 && bin.B is Ir.Imm imm && imm.W == 2 && (imm.Value >> 8) == 0 && _isa is Mos6502Isa)
        {
            // Dodaj młodszy bajt (carry zostanie ustawiony jeśli overflow)
            LoadA(ByteOf(bin.A, 0));
            Alu(ByteAlu.Add, ByteOf(bin.B, 0), true);
            StoreA(Dst(bin.Dst, 0));

            // Zamiast lda hi; adc #0; sta hi, użyj bcc skip; inc hi; skip:
            // Carry flag jest ustawiony jeśli był overflow (dodanie spowodowało >= 256)
            // bcc = branch if carry clear (brak overflow)
            string skip = _isa.LocalLabel();
            Raw($"bcc {skip}");
            _isa.TryStep([Dst(bin.Dst, 1)], true);
            Raw($"{skip}:");

            _acc.Clear();
            return new(false, false);
        }

        switch (bin.Kind)
        {
            case Ir.BinOp.Add:
                return EmitChain(ByteAlu.Add, bin);
            case Ir.BinOp.Sub:
                return EmitChain(ByteAlu.Sub, bin);
            case Ir.BinOp.And:
                return EmitChain(ByteAlu.And, bin);
            case Ir.BinOp.Or:
                return EmitChain(ByteAlu.Or, bin);
            case Ir.BinOp.Xor:
                return EmitChain(ByteAlu.Xor, bin);
            case Ir.BinOp.Shl:
            case Ir.BinOp.Shr:
                return EmitShift(bin);
            default:
                throw new InvalidOperationException($"ByteSelector cannot select {bin.Kind} (Legalizer should have removed it).");
        }
    }

    private WordResult EmitChain(ByteAlu alu, Ir.Bin bin)
    {
        // Podwojenie słowa (x + x): prymityw shiftu, nie dwa ładowania do add (tylko CPU z TryShlWord1;
        // reszta idzie starą drogą bez zmian w IR).
        if (alu is ByteAlu.Add && bin.Dst.W == 2 && bin.A.Equals(bin.B)
            && WordOf(bin.Dst) is { } doubled && WordOf(bin.A) is { } doubledSrc
            && _isa is IWordShift shiftedByOne && shiftedByOne.TryShlWord1(doubled, doubledSrc) is { Emitted: true } shl)
        {
            _acc.Remove(doubled.Lo);
            _acc.Remove(doubled.Hi);
            return shl;
        }

        if (alu is ByteAlu.Add or ByteAlu.Sub && bin.Dst.W == 2 && WordOf(bin.Dst) is { } dst && WordOf(bin.A) is { } a && WordOf(bin.B) is { } b
            && _isa is IWordArithmetic word && word.TryAddWord(dst, a, b, alu == ByteAlu.Sub) is { Emitted: true } add)
        {
            // A bez zmian, ale bajty celu już nie są mu równe
            _acc.Remove(dst.Lo);
            _acc.Remove(dst.Hi);
            return add;
        }

        if (alu is ByteAlu.Add or ByteAlu.Sub && bin.Dst.W == 4 && HalfOf(bin.Dst, 0) is { } dl && HalfOf(bin.Dst, 1) is { } dh
            && HalfOf(bin.A, 0) is { } al && HalfOf(bin.A, 1) is { } ah && HalfOf(bin.B, 0) is { } bl && HalfOf(bin.B, 1) is { } bh
            && _isa is IWordArithmetic longArith && longArith.TryAddLong((dl, dh), (al, ah), (bl, bh), alu == ByteAlu.Sub))
        {
            foreach (string address in (string[])[dl.Lo, dl.Hi, dh.Lo, dh.Hi])
            {
                _acc.Remove(address);
            }

            return new(false, false);
        }

        for (int i = 0; i < bin.Dst.W; i++)
        {
            LoadA(ByteOf(bin.A, i));
            Alu(alu, ByteOf(bin.B, i), i == 0);
            StoreA(Dst(bin.Dst, i));
        }

        return new(false, false);
    }

    private WordResult EmitShift(Ir.Bin bin)
    {
        if (bin.B is not Ir.Imm count)
        {
            throw new InvalidOperationException("variable shift count must be legalized.");
        }

        int width = bin.Dst.W;
        int n = count.Value & 0xFF;
        bool left = bin.Kind == Ir.BinOp.Shl;
        if (left && n == 1 && width == 2 && WordOf(bin.Dst) is { } dst && WordOf(bin.A) is { } src
            && _isa is IWordShift shiftedByOne && shiftedByOne.TryShlWord1(dst, src) is { Emitted: true } shl)
        {
            // A bez zmian, ale bajty celu już nie są mu równe (jak w EmitChain po TryAddWord)
            _acc.Remove(dst.Lo);
            _acc.Remove(dst.Hi);
            return shl;
        }

        if (n >= 8 * width)
        {
            for (int i = 0; i < width; i++)
            {
                LoadA(Zero());
                StoreA(Dst(bin.Dst, i));
            }

            return new(false, false);
        }

        EmitMov(bin.Dst, bin.A);
        int bytes = n / 8;
        if (bytes > 0)
        {
            if (left)
            {
                for (int i = width - 1; i >= 0; i--)
                {
                    LoadA(i >= bytes ? new Octet(false, Dst(bin.Dst, i - bytes)) : Zero());
                    StoreA(Dst(bin.Dst, i));
                }
            }
            else
            {
                for (int i = 0; i < width; i++)
                {
                    LoadA(i + bytes < width ? new Octet(false, Dst(bin.Dst, i + bytes)) : Zero());
                    StoreA(Dst(bin.Dst, i));
                }
            }
        }

        for (int bit = 0; bit < n % 8; bit++)
        {
            for (int k = 0; k < width; k++)
            {
                int i = left ? k : width - 1 - k;
                LoadA(new Octet(false, Dst(bin.Dst, i)));
                if (left)
                {
                    ShlA(k == 0);
                }
                else
                {
                    ShrA(k == 0);
                }

                StoreA(Dst(bin.Dst, i));
            }
        }

        return new(false, false);
    }

    private void EmitUn(Ir.Un un)
    {
        for (int i = 0; i < un.Dst.W; i++)
        {
            if (un.Kind == Ir.UnOp.Neg)
            {
                LoadA(Zero());
                Alu(ByteAlu.Sub, ByteOf(un.A, i), i == 0);
            }
            else
            {
                LoadA(ByteOf(un.A, i));
                Alu(ByteAlu.Xor, new Octet(true, "255"), true);
            }

            StoreA(Dst(un.Dst, i));
        }
    }

    private void EmitLoad(Ir.Load load)
    {
        if (load.Ptr is Ir.AddrOf address)
        {
            for (int i = 0; i < load.Dst.W; i++)
            {
                LoadA(i < load.Bytes ? new Octet(false, At(address.Sym, address.Off + load.Off + MemIndex(i, load.Bytes))) : Zero());
                StoreA(Dst(load.Dst, i));
            }

            return;
        }

        var pointer = (Ir.Cell)load.Ptr;
        PtrSetup(_isa.Sym(pointer.Sym), load.Off, pointer.Sym == load.Dst.Sym);
        for (int i = 0; i < load.Dst.W; i++)
        {
            if (i < load.Bytes)
            {
                PtrLoad(MemIndex(i, load.Bytes));
            }
            else
            {
                LoadA(Zero());
            }

            StoreA(Dst(load.Dst, i));
        }
    }

    private void EmitLoadIdx(Ir.LoadIdx load)
    {
        var indexed = (IIndexed)_isa;
        indexed.IndexSetup(_isa.Loc(load.Index.Sym, load.Index.W, 0), load.Shift);
        _acc.Clear();
        for (int i = 0; i < load.Dst.W; i++)
        {
            if (i < load.Bytes)
            {
                indexed.IndexLoad(At(load.Sym, load.Off + MemIndex(i, load.Bytes)));
                _acc.Clear();
            }
            else
            {
                LoadA(Zero());
            }

            StoreA(Dst(load.Dst, i));
        }
    }

    private void EmitStoreIdx(Ir.StoreIdx store)
    {
        var indexed = (IIndexed)_isa;
        indexed.IndexSetup(_isa.Loc(store.Index.Sym, store.Index.W, 0), store.Shift);
        _acc.Clear();
        for (int i = 0; i < store.Bytes; i++)
        {
            LoadA(ByteOf(store.Value, i));
            indexed.IndexStore(At(store.Sym, store.Off + MemIndex(i, store.Bytes)));
        }
    }

    private void EmitStore(Ir.Store store)
    {
        if (store.Ptr is Ir.AddrOf address)
        {
            for (int i = 0; i < store.Bytes; i++)
            {
                LoadA(ByteOf(store.Value, i));
                StoreA(At(address.Sym, address.Off + store.Off + MemIndex(i, store.Bytes)));
            }

            return;
        }

        var pointer = (Ir.Cell)store.Ptr;
        PtrSetup(_isa.Sym(pointer.Sym), store.Off);
        for (int i = 0; i < store.Bytes; i++)
        {
            LoadA(ByteOf(store.Value, i));
            _isa.PtrStore(MemIndex(i, store.Bytes));
        }
    }
}
