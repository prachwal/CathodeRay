using System.Globalization;
using System.Text;

namespace CathodeRay.C;

/// <summary>Dobór instrukcji dla procesorów akumulatorowych: składa kod z prymitywów <see cref="ByteIsa"/>. Komórki leżą
/// w pamięci, argumenty przechodzą przez komórki <c>cc_argN</c>, wynik przez <c>cc_ret</c>. Operacje, których CPU nie ma
/// (mnożenie, dzielenie, przesunięcia o zmienną liczbę, bloki), zamienia wcześniej <see cref="Legalizer"/>.</summary>
internal sealed class ByteSelector
{
    private readonly Ir.Module _module;

    private readonly ByteIsa _isa;

    private readonly Dictionary<string, string> _addressCells = new(StringComparer.Ordinal);

    private readonly List<(string Label, string Expression)> _addressList = [];

    /// <summary>Co wiadomo o akumulatorze: adresy pamięci i stałe (z prefiksem <c>#</c>), które mają teraz taką samą wartość jak A.
    /// Każdy zapis do pamięci to zapis A, więc zapisy zachowują tę wiedzę; zmienia ją zmiana A, wołanie i etykieta (wejście z innego miejsca).</summary>
    private readonly HashSet<string> _acc = new(StringComparer.Ordinal);

    private readonly HashSet<string> _volatile;

    private int _labels;

    private bool _usesIcall;

    public ByteSelector(Ir.Module module, ByteIsa isa)
    {
        _module = module;
        _isa = isa;
        _volatile = module.Volatile is null ? [] : [.. module.Volatile.Select(isa.Sym)];
    }

    /// <summary>Składa asembler modułu.</summary>
    /// <returns>Tekst dla asemblera CPU.</returns>
    public string Emit()
    {
        // komórka w rejestrze nie może być zewnętrzna ani zapisywana w ramce (push/pop jej bajtów)
        string? misplaced = _module.ExternCells.Concat(_module.Functions.SelectMany(static f => f.Saved).Select(static o => o.Sym))
            .FirstOrDefault(sym => _isa.IsRegister(_isa.Sym(sym)));
        if (misplaced is not null)
        {
            throw new InvalidOperationException($"cell {misplaced} is assigned to a register but is extern or saved in a frame.");
        }

        foreach (Ir.Function function in _module.Functions)
        {
            EmitFunction(function);
        }

        return Header() + _isa.Text + PrintInit() + PrintData() + PrintBss();
    }

    private static int WidthOf(Ir.Op op) => op switch
    {
        Ir.Cell cell => cell.W,
        Ir.Imm imm => imm.W,
        _ => 2,
    };

    private static string ArgSym(int index, int part) => part == 0 ? $"cc_arg{index + 1}" : $"cc_arg{index + 1}_h";

    private static string RetSym(int part) => part == 0 ? "cc_ret" : "cc_ret_h";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Mangle(Ir.Function function, string label) => $"{function.Name}__{label}";

    private static Octet Zero() => new(true, "0");

    private static bool IsZero(Ir.Op op) => op is Ir.Imm { Value: 0 };

    private static string Key(Octet value) => value.IsImmediate ? "#" + value.Text : value.Text;

    private string At(string sym, int offset) => _isa.At(sym, offset);

    private bool IsVolatile(string address)
    {
        if (_volatile.Count == 0)
        {
            return false;
        }

        int cut = address.IndexOfAny(['+', '-']);
        return _volatile.Contains(cut < 0 ? address : address[..cut]);
    }

    private void LoadA(Octet value)
    {
        string key = Key(value);
        bool cacheable = value.IsImmediate || !IsVolatile(value.Text);
        if (cacheable && _acc.Contains(key))
        {
            return;
        }

        _isa.LoadA(value);
        _acc.Clear();
        if (cacheable)
        {
            _acc.Add(key);
        }
    }

    private void StoreA(string address)
    {
        _isa.StoreA(address);
        if (!IsVolatile(address))
        {
            _acc.Add(address);
        }
    }

    private void Alu(ByteAlu op, Octet value, bool first)
    {
        _isa.Alu(op, value, first);
        _acc.Clear();
    }

    private void Cmp(Octet value)
    {
        _isa.Cmp(value);
        _acc.Clear();
    }

    private void ShlA(bool first)
    {
        _isa.ShlA(first);
        _acc.Clear();
    }

    private void ShrA(bool first)
    {
        _isa.ShrA(first);
        _acc.Clear();
    }

    private void PopA()
    {
        _isa.PopA();
        _acc.Clear();
    }

    private void CallDirect(string symbol)
    {
        _isa.Call(symbol);
        _acc.Clear();
    }

    private void CallIndirect(string cell)
    {
        _isa.CallIndirect(cell);
        _acc.Clear();
    }

    private void PtrSetup(string cell, int offset, bool mustCopy = false)
    {
        _isa.PtrSetup(cell, offset, mustCopy);
        _acc.Clear();
    }

    private void PtrLoad(int index)
    {
        _isa.PtrLoad(index);
        _acc.Clear();
    }

    private void Raw(string line)
    {
        _isa.Raw(line);
        if (line.EndsWith(':'))
        {
            _acc.Clear();
        }
    }

    private string Label(string hint) => $"S{++_labels}_{hint}";

    private int MemIndex(int significance, int bytes) => _isa.BigEndian ? bytes - 1 - significance : significance;

    private Octet ByteOf(Ir.Op op, int index)
    {
        switch (op)
        {
            case Ir.Cell cell:
                return index < cell.W ? new Octet(false, _isa.Loc(cell.Sym, cell.W, index)) : Zero();
            case Ir.Imm imm:
                return new Octet(true, Number(index < imm.W ? (imm.Value >> (8 * index)) & 0xFF : 0));
            case Ir.AddrOf address:
                if (index >= 2)
                {
                    return Zero();
                }

                string? direct = _isa.AddressByte(At(address.Sym, address.Off), index);
                return direct is not null ? new Octet(true, direct) : new Octet(false, _isa.Loc(AddressCell(address), 2, index));
            default:
                throw new InvalidOperationException($"unsupported operand {op.GetType().Name}.");
        }
    }

    private string AddressCell(Ir.AddrOf address)
    {
        string key = $"{address.Sym}|{address.Off}";
        if (!_addressCells.TryGetValue(key, out string? label))
        {
            label = $"__a{_addressList.Count}";
            _addressCells[key] = label;
            _addressList.Add((label, At(address.Sym, address.Off)));
        }

        return label;
    }

    private string Dst(Ir.Cell cell, int index) => _isa.Loc(cell.Sym, cell.W, index);

    /// <summary>Operand jako słowo 16-bitowe dla <see cref="ByteIsa.TryMoveWord"/> albo <see langword="null"/> (komórka węższa, <c>volatile</c>).</summary>
    private Word? WordOf(Ir.Op op) => op switch
    {
        Ir.Cell { W: 2 } cell => Pair(Dst(cell, 0), Dst(cell, 1)),
        Ir.Imm imm => new Word(true, Number(imm.Value & (imm.W == 1 ? 0xFF : 0xFFFF)), string.Empty),
        Ir.AddrOf address => new Word(true, At(address.Sym, address.Off), string.Empty),
        _ => null,
    };

    /// <summary>Połowa (0 = młodsza, 1 = starsza) operandu 32-bitowego jako słowo albo <see langword="null"/>; węższy operand
    /// ma starszą połowę równą zeru.</summary>
    private Word? HalfOf(Ir.Op op, int half) => op switch
    {
        Ir.Cell { W: 4 } cell => Pair(Dst(cell, 2 * half), Dst(cell, (2 * half) + 1)),
        Ir.Imm { W: 4 } imm => new Word(true, Number((imm.Value >> (16 * half)) & 0xFFFF), string.Empty),
        Ir.Cell { W: 1 } when half == 0 => null,
        _ => half == 0 ? WordOf(op) : new Word(true, "0", string.Empty),
    };

    private Word? Pair(string lo, string hi) => (IsVolatile(lo) || IsVolatile(hi)) ? null : new Word(false, lo, hi);

    /// <summary>Kopia słowa przez ISA; A się nie zmienia, ale bajty celu już nie są równe A.</summary>
    private bool TryMoveWord(Word? dst, Word? src)
    {
        if (dst is not { } target || src is not { } source || !_isa.TryMoveWord(target, source))
        {
            return false;
        }

        _acc.Remove(target.Lo);
        _acc.Remove(target.Hi);
        return true;
    }

    private void EmitFunction(Ir.Function function)
    {
        int mark = _isa.Mark;
        _isa.BeginFunction(function);
        EmitFunctionBody(function);
        _isa.RelaxFrom(mark);
    }

    private void EmitFunctionBody(Ir.Function function)
    {
        int start = 0;
        if (function.Body.Count > 0 && function.Body[0] is Ir.Src leading)
        {
            EmitSource(leading);
            start = 1;
        }

        if (!function.IsStatic)
        {
            Raw(_isa.Global(_isa.Sym(function.Name)));
        }

        Raw($"{_isa.Sym(function.Name)}:");
        foreach (Ir.Owned owned in function.Saved)
        {
            if (SavedWord(owned) is { } word && _isa.TryPushWord(word))
            {
                continue;
            }

            foreach (string address in SavedBytes(owned))
            {
                LoadA(new Octet(false, address));
                _isa.PushA();
            }
        }

        for (int i = 0; i < function.Params.Count; i++)
        {
            Ir.Cell param = function.Params[i];
            if (param.Sym == ArgSym(i, 0) || (param.W == 2 && TryMoveWord(WordOf(param), Pair(ArgSym(i, 0), ArgSym(i, 1)))))
            {
                continue;
            }

            for (int part = 0; part < param.W; part++)
            {
                LoadA(new Octet(false, ArgSym(i, part)));
                StoreA(Dst(param, part));
            }
        }

        for (int i = start; i < function.Body.Count; i++)
        {
            EmitIns(function, function.Body[i], i == function.Body.Count - 1);
        }

        Raw($"{Mangle(function, "ret")}:");
        foreach (Ir.Owned owned in function.Saved.Reverse())
        {
            if (SavedWord(owned) is { } word && _isa.TryPopWord(word, InResultReg(function.RetW)))
            {
                continue;
            }

            foreach (string address in SavedBytes(owned).Reverse())
            {
                PopA();
                StoreA(address);
            }
        }

        _isa.Return();
    }

    /// <summary>Wynik tej szerokości wraca w rejestrze CPU (<see cref="ByteIsa.ReturnsInResultReg"/>), nie w <c>cc_ret</c>.</summary>
    private bool InResultReg(int width) => _isa.ReturnsInResultReg && width is 1 or 2;

    /// <summary>Komórka ramki jako słowo (skalar 2-bajtowy) do odłożenia parą albo <see langword="null"/>.</summary>
    private Word? SavedWord(Ir.Owned owned) =>
        (owned.Size == 2 && !owned.Aggregate) ? Pair(_isa.Loc(owned.Sym, 2, 0), _isa.Loc(owned.Sym, 2, 1)) : null;

    private IEnumerable<string> SavedBytes(Ir.Owned owned)
    {
        if (owned.Aggregate)
        {
            for (int i = 0; i < owned.Size; i++)
            {
                yield return At(owned.Sym, i);
            }

            yield break;
        }

        for (int i = 0; i < owned.Size; i++)
        {
            yield return _isa.Loc(owned.Sym, owned.Size, i);
        }
    }

    private void EmitSource(Ir.Src source) =>
        Raw(source.File is null ? $";c:{source.Line}" : $";c:{source.File}:{source.Line}");

    private void EmitIns(Ir.Function function, Ir.Ins ins, bool last)
    {
        switch (ins)
        {
            case Ir.Src source:
                EmitSource(source);
                break;
            case Ir.Label label:
                Raw($"{Mangle(function, label.Name)}:");
                break;
            case Ir.Jmp jump:
                _isa.Jump(Mangle(function, jump.Target));
                break;
            case Ir.Mov mov:
                EmitMov(mov.Dst, mov.Src);
                break;
            case Ir.Bin bin:
                EmitBin(bin);
                break;
            case Ir.Un un:
                EmitUn(un);
                break;
            case Ir.Load load:
                EmitLoad(load);
                break;
            case Ir.Store store:
                EmitStore(store);
                break;
            case Ir.LoadIdx loadIdx:
                EmitLoadIdx(loadIdx);
                break;
            case Ir.StoreIdx storeIdx:
                EmitStoreIdx(storeIdx);
                break;
            case Ir.BrCmp branch:
                EmitBranch(function, branch);
                break;
            case Ir.Call call:
                EmitCall(call);
                break;
            case Ir.Ret ret:
                EmitRet(function, ret, last);
                break;
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

    private void EmitBin(Ir.Bin bin)
    {
        if (bin.Kind is Ir.BinOp.Add or Ir.BinOp.Sub && bin.Dst.W <= 2 && bin.A is Ir.Cell same && same.Sym == bin.Dst.Sym && same.W == bin.Dst.W && bin.B is Ir.Imm { Value: 1 })
        {
            string[] cells = [.. Enumerable.Range(0, bin.Dst.W).Select(i => Dst(bin.Dst, i))];
            if (_isa.TryStep(cells, bin.Kind == Ir.BinOp.Add))
            {
                _acc.Clear();
                return;
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
            return;
        }

        switch (bin.Kind)
        {
            case Ir.BinOp.Add:
                EmitChain(ByteAlu.Add, bin);
                break;
            case Ir.BinOp.Sub:
                EmitChain(ByteAlu.Sub, bin);
                break;
            case Ir.BinOp.And:
                EmitChain(ByteAlu.And, bin);
                break;
            case Ir.BinOp.Or:
                EmitChain(ByteAlu.Or, bin);
                break;
            case Ir.BinOp.Xor:
                EmitChain(ByteAlu.Xor, bin);
                break;
            case Ir.BinOp.Shl:
            case Ir.BinOp.Shr:
                EmitShift(bin);
                break;
            default:
                throw new InvalidOperationException($"ByteSelector cannot select {bin.Kind} (Legalizer should have removed it).");
        }
    }

    private void EmitChain(ByteAlu alu, Ir.Bin bin)
    {
        if (alu is ByteAlu.Add or ByteAlu.Sub && bin.Dst.W == 2 && WordOf(bin.Dst) is { } dst && WordOf(bin.A) is { } a && WordOf(bin.B) is { } b
            && _isa.TryAddWord(dst, a, b, alu == ByteAlu.Sub))
        {
            // A bez zmian, ale bajty celu już nie są mu równe
            _acc.Remove(dst.Lo);
            _acc.Remove(dst.Hi);
            return;
        }

        if (alu is ByteAlu.Add or ByteAlu.Sub && bin.Dst.W == 4 && HalfOf(bin.Dst, 0) is { } dl && HalfOf(bin.Dst, 1) is { } dh
            && HalfOf(bin.A, 0) is { } al && HalfOf(bin.A, 1) is { } ah && HalfOf(bin.B, 0) is { } bl && HalfOf(bin.B, 1) is { } bh
            && _isa.TryAddLong((dl, dh), (al, ah), (bl, bh), alu == ByteAlu.Sub))
        {
            foreach (string address in (string[])[dl.Lo, dl.Hi, dh.Lo, dh.Hi])
            {
                _acc.Remove(address);
            }

            return;
        }

        for (int i = 0; i < bin.Dst.W; i++)
        {
            LoadA(ByteOf(bin.A, i));
            Alu(alu, ByteOf(bin.B, i), i == 0);
            StoreA(Dst(bin.Dst, i));
        }
    }

    private void EmitShift(Ir.Bin bin)
    {
        if (bin.B is not Ir.Imm count)
        {
            throw new InvalidOperationException("variable shift count must be legalized.");
        }

        int width = bin.Dst.W;
        int n = count.Value & 0xFF;
        bool left = bin.Kind == Ir.BinOp.Shl;
        if (n >= 8 * width)
        {
            for (int i = 0; i < width; i++)
            {
                LoadA(Zero());
                StoreA(Dst(bin.Dst, i));
            }

            return;
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
        _isa.IndexSetup(_isa.Loc(load.Index.Sym, load.Index.W, 0), load.Shift);
        _acc.Clear();
        for (int i = 0; i < load.Dst.W; i++)
        {
            if (i < load.Bytes)
            {
                _isa.IndexLoad(At(load.Sym, load.Off + MemIndex(i, load.Bytes)));
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
        _isa.IndexSetup(_isa.Loc(store.Index.Sym, store.Index.W, 0), store.Shift);
        _acc.Clear();
        for (int i = 0; i < store.Bytes; i++)
        {
            LoadA(ByteOf(store.Value, i));
            _isa.IndexStore(At(store.Sym, store.Off + MemIndex(i, store.Bytes)));
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

    private void EmitBranch(Ir.Function function, Ir.BrCmp branch)
    {
        string target = Mangle(function, branch.Target);
        int width = Math.Max(WidthOf(branch.A), WidthOf(branch.B));
        Ir.Cond cond = branch.C;
        if (cond is Ir.Cond.Eq or Ir.Cond.Ne && width > 1 && (IsZero(branch.B) || IsZero(branch.A)))
        {
            // porównanie z zerem: A = suma bitowa (OR) wszystkich bajtów, flaga Z z ostatniej operacji
            Ir.Op value = IsZero(branch.B) ? branch.A : branch.B;
            LoadA(ByteOf(value, 0));
            for (int i = 1; i < width; i++)
            {
                Alu(ByteAlu.Or, ByteOf(value, i), i == 1);
            }

            _isa.JumpIf(cond == Ir.Cond.Eq ? ByteFlag.Zero : ByteFlag.NotZero, target);
            return;
        }

        if (cond is Ir.Cond.Eq or Ir.Cond.Ne)
        {
            string skip = Label("ne");
            for (int i = 0; i < width; i++)
            {
                LoadA(ByteOf(branch.A, i));
                Cmp(ByteOf(branch.B, i));
                _isa.JumpIf(ByteFlag.NotZero, cond == Ir.Cond.Eq ? skip : target);
            }

            if (cond == Ir.Cond.Eq)
            {
                _isa.Jump(target);
                Raw($"{skip}:");
            }

            return;
        }

        bool signed = cond is Ir.Cond.Lt or Ir.Cond.Le or Ir.Cond.Gt or Ir.Cond.Ge;
        bool swap = cond is Ir.Cond.Gt or Ir.Cond.Le or Ir.Cond.Gtu or Ir.Cond.Leu;
        bool onBorrow = cond is Ir.Cond.Lt or Ir.Cond.Gt or Ir.Cond.Ltu or Ir.Cond.Gtu;
        Ir.Op x = swap ? branch.B : branch.A;
        Ir.Op y = swap ? branch.A : branch.B;
        bool overflow = signed && _isa.HasOverflowFlag;
        bool bias = signed && !overflow;
        Octet[] xs = Bytes(x, width, bias ? "cc_t0" : null);
        Octet[] ys = Bytes(y, width, bias ? "cc_t1" : null);
        for (int i = 0; i < width; i++)
        {
            LoadA(xs[i]);
            if (width == 1 && !overflow)
            {
                Cmp(ys[i]);
            }
            else
            {
                Alu(ByteAlu.Sub, ys[i], i == 0);
            }
        }

        if (overflow)
        {
            _isa.JumpIfSigned(onBorrow, target);
            return;
        }

        _isa.JumpIf(onBorrow ? ByteFlag.Borrow : ByteFlag.NoBorrow, target);
    }

    /// <summary>Bajty operandu; dla porównania ze znakiem najstarszy bajt jest odwrócony (xor 128), więc porównanie
    /// bez znaku daje wynik ze znakiem.</summary>
    private Octet[] Bytes(Ir.Op op, int width, string? biasCell)
    {
        Octet[] bytes = [.. Enumerable.Range(0, width).Select(i => ByteOf(op, i))];
        if (biasCell is null)
        {
            return bytes;
        }

        Octet top = bytes[width - 1];
        if (top.IsImmediate && int.TryParse(top.Text, CultureInfo.InvariantCulture, out int value))
        {
            bytes[width - 1] = new Octet(true, Number(value ^ 0x80));
            return bytes;
        }

        LoadA(top);
        Alu(ByteAlu.Xor, new Octet(true, "128"), true);
        StoreA(biasCell);
        bytes[width - 1] = new Octet(false, biasCell);
        return bytes;
    }

    private void EmitCall(Ir.Call call)
    {
        for (int i = 0; i < call.Args.Count; i++)
        {
            if (call.Args[i] is Ir.Cell same && same.W == call.ParamWidths[i] && same.Sym == ArgSym(i, 0))
            {
                // parametr funkcji zaaliasowany na cc_argN jest już w komórce argumentu na tej samej pozycji
                continue;
            }

            if (call.ParamWidths[i] == 2 && TryMoveWord(Pair(ArgSym(i, 0), ArgSym(i, 1)), WordOf(call.Args[i])))
            {
                continue;
            }

            for (int part = 0; part < call.ParamWidths[i]; part++)
            {
                LoadA(ByteOf(call.Args[i], part));
                StoreA(ArgSym(i, part));
            }
        }

        // rejestry komórek żywych za wołaniem: na stos po argumentach, ze stosu przed zapisem wyniku (wynik może leżeć w tej parze)
        IReadOnlyList<string> saved = _isa.SavedAround(call);
        foreach (string pair in saved)
        {
            _isa.PushPair(pair);
        }

        if (call.Indirect is not null)
        {
            _usesIcall = true;
            CallIndirect(_isa.Sym(call.Indirect.Sym));
        }
        else
        {
            CallDirect(_isa.Sym(call.Direct!));
        }

        foreach (string pair in saved.Reverse())
        {
            _isa.PopPair(pair);
        }

        if (call.Result is not null && InResultReg(call.Result.W))
        {
            if (call.Result.W == 2 && WordOf(call.Result) is { } dst && _isa.TryMoveFromResultReg(dst))
            {
                _acc.Remove(dst.Lo);
                _acc.Remove(dst.Hi);
                return;
            }

            for (int part = 0; part < call.Result.W; part++)
            {
                _isa.ResultByteToA(part);
                _acc.Clear();
                StoreA(Dst(call.Result, part));
            }
        }
        else if (call.Result is not null && !(call.Result.W == 2 && TryMoveWord(WordOf(call.Result), Pair(RetSym(0), RetSym(1)))))
        {
            for (int part = 0; part < call.Result.W; part++)
            {
                LoadA(new Octet(false, RetSym(part)));
                StoreA(Dst(call.Result, part));
            }
        }
    }

    private void EmitRet(Ir.Function function, Ir.Ret ret, bool last)
    {
        if (ret.Value is not null && InResultReg(ret.W))
        {
            if (!(ret.W == 2 && WordOf(ret.Value) is { } word && _isa.TryMoveToResultReg(word)))
            {
                for (int part = 0; part < ret.W; part++)
                {
                    LoadA(ByteOf(ret.Value, part));
                    _isa.ResultByteFromA(part);
                }
            }
        }
        else if (ret.Value is not null && !(ret.W == 2 && TryMoveWord(Pair(RetSym(0), RetSym(1)), WordOf(ret.Value))))
        {
            for (int part = 0; part < ret.W; part++)
            {
                LoadA(ByteOf(ret.Value, part));
                StoreA(RetSym(part));
            }
        }

        if (!last)
        {
            _isa.Jump(Mangle(function, "ret"));
        }
    }

    private string Header()
    {
        var text = new StringBuilder();
        text.AppendLine(_isa.Segment("CODE"));
        if (!_module.ObjectMode)
        {
            return text.ToString();
        }

        text.Append(_isa.Preamble());
        foreach (string function in _module.ExternFunctions)
        {
            text.AppendLine(_isa.Extern(_isa.Sym(function)));
        }

        foreach (string external in _module.ExternCells)
        {
            text.AppendLine(_isa.Extern(_isa.Sym(external)));
        }

        for (int arg = 1; arg <= TypeChecker.MaxArgs; arg++)
        {
            text.AppendLine(_isa.Extern($"cc_arg{arg}"));
            text.AppendLine(_isa.Extern($"cc_arg{arg}_h"));
        }

        foreach (string symbol in new[] { "cc_ret", "cc_ret_h", "cc_t0", "cc_t1" })
        {
            text.AppendLine(_isa.Extern(symbol));
        }

        if (_usesIcall)
        {
            foreach (string symbol in _isa.IndirectSymbols)
            {
                text.AppendLine(_isa.Extern(symbol));
            }
        }

        return text.ToString();
    }

    private string PrintInit()
    {
        var text = new StringBuilder();
        Ir.Data[] table = [.. _module.Data.Where(static d => d.Segment == "INIT")];
        if (table.Length == 0 && _module.ObjectMode)
        {
            return string.Empty;
        }

        text.AppendLine(_isa.Segment("INIT"));
        if (!_module.ObjectMode)
        {
            text.AppendLine("__init_start:");
        }

        foreach (Ir.Data data in table)
        {
            AppendData(text, data);
        }

        if (!_module.ObjectMode)
        {
            text.AppendLine("__init_end:");
        }

        return text.ToString();
    }

    private string PrintData()
    {
        var text = new StringBuilder();
        text.AppendLine(_isa.Segment("DATA"));
        foreach (Ir.Data data in _module.Data.Where(static d => d.Segment == "DATA"))
        {
            AppendData(text, data);
        }

        foreach ((string label, string expression) in _addressList)
        {
            text.AppendLine($"{label}: {_isa.Word(expression)}");
        }

        return text.ToString();
    }

    private string PrintBss()
    {
        var text = new StringBuilder();
        foreach (string segment in new[] { "ZP", "BSS" })
        {
            AppendReserved(text, segment);
        }

        return text.ToString();
    }

    private void AppendReserved(StringBuilder text, string segment)
    {
        if (segment == "ZP" && !_module.Data.Any(static d => d.Segment == "ZP") && _module.ObjectMode)
        {
            return;
        }

        text.AppendLine(_isa.Segment(segment));
        foreach (Ir.Data data in _module.Data.Where(d => d.Segment == segment && !_isa.IsRegister(_isa.Sym(d.Sym))))
        {
            if (data.Exported)
            {
                text.AppendLine(_isa.Global(_isa.Sym(data.Sym)));
            }

            text.AppendLine($"{_isa.Sym(data.Sym)}: {_isa.Reserve(data.Size)}");
        }

        if (!_module.ObjectMode && segment == "BSS")
        {
            text.AppendLine("__bss_end:");
        }
        else if (!_module.ObjectMode && segment == "ZP")
        {
            text.AppendLine("__zp_end:");
        }
    }

    private void AppendData(StringBuilder text, Ir.Data data)
    {
        if (data.Exported)
        {
            text.AppendLine(_isa.Global(_isa.Sym(data.Sym)));
        }

        string label = data.Sym.Length == 0 ? string.Empty : $"{_isa.Sym(data.Sym)}: ";
        foreach (Ir.Piece piece in data.Init!)
        {
            switch (piece)
            {
                case Ir.Bytes bytes:
                    text.AppendLine($"{label}{_isa.Bytes(bytes.Value.Select(static b => (int)b))}");
                    break;
                case Ir.SymWord word:
                    text.AppendLine($"{label}{_isa.Word(At(word.Sym, word.Off))}");
                    break;
            }

            label = string.Empty;
        }
    }
}
