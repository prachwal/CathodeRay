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

    /// <summary>Funkcje zdefiniowane w module (rozumieją v2); wołania reszty (helpery uruchomieniowe w ręcznym
    /// asemblerze) selektor marszuje jak v1: argumenty do <c>cc_argN</c>, wynik z <c>cc_ret</c>.</summary>
    private readonly HashSet<string> _defined;

    private int _labels;

    private bool _usesIcall;

    /// <summary>Skoki do etykiety powrotu bieżącej funkcji (gdy zero i ciało kończy skokiem — stopka martwa).</summary>
    private int _retJumps;

    public ByteSelector(Ir.Module module, ByteIsa isa)
    {
        _module = module;
        _isa = isa;
        _volatile = module.Volatile is null ? [] : [.. module.Volatile.Select(isa.Sym)];
        _defined = new([.. module.Functions.Select(static f => f.Name)], StringComparer.Ordinal);
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

    private static string RetSym(int part) => part == 0 ? "cc_ret" : "cc_ret_h";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Mangle(Ir.Function function, string label) => $"{function.Name}__{label}";

    private static Octet Zero() => new(true, "0");

    private static bool IsZero(Ir.Op op) => op is Ir.Imm { Value: 0 };

    private static string Key(Octet value) => value.IsImmediate ? "#" + value.Text : value.Text;

    /// <summary>Symbol komórki bez przesunięcia (<c>x+1</c> → <c>x</c>).</summary>
    /// <param name="address">Adres bajtu.</param>
    /// <returns>Baza symbolu.</returns>
    private static string Base(string address)
    {
        int cut = address.IndexOfAny(['+', '-']);
        return cut < 0 ? address : address[..cut];
    }

    /// <summary>Wartość stałej 16-bitowej z bajtów operandu (bez znaku 0..65535).</summary>
    /// <param name="lo">Młodszy bajt.</param>
    /// <param name="hi">Starszy bajt.</param>
    /// <param name="value">Wartość albo 0.</param>
    /// <returns>Czy oba bajty są liczbami.</returns>
    private static bool Const16(Octet lo, Octet hi, out int value)
    {
        value = 0;
        if (!lo.IsImmediate || !hi.IsImmediate)
        {
            return false;
        }

        if (!int.TryParse(lo.Text, CultureInfo.InvariantCulture, out int low)
            || !int.TryParse(hi.Text, CultureInfo.InvariantCulture, out int high))
        {
            return false;
        }

        value = low + (high << 8);
        return true;
    }

    /// <summary>Pomija komentarze źródłowe, zbierając je do późniejszej emisji.</summary>
    /// <param name="function">Funkcja.</param>
    /// <param name="comments">Zebrane komentarze.</param>
    /// <param name="i">Pozycja (przesuwana za komentarze).</param>
    /// <returns>Czy pozycja w zakresie ciała.</returns>
    private static bool SkipComments(Ir.Function function, List<Ir.Src> comments, ref int i)
    {
        while (i < function.Body.Count && function.Body[i] is Ir.Src src)
        {
            comments.Add(src);
            i++;
        }

        return i < function.Body.Count;
    }

    /// <summary>Krok licznika/wskaźnika o 1 (<c>x = x + 1</c> albo <c>x = x - 1</c>).</summary>
    /// <param name="ins">Instrukcja.</param>
    /// <param name="sym">Oczekiwany symbol.</param>
    /// <param name="width">Szerokość.</param>
    /// <param name="increment">Plus 1 albo minus 1.</param>
    /// <returns>Czy instrukcja to taki krok.</returns>
    private static bool IsStep(Ir.Ins ins, string sym, int width, bool increment)
    {
        return ins is Ir.Bin step
            && (increment ? step.Kind == Ir.BinOp.Add : step.Kind == Ir.BinOp.Sub)
            && step.Dst is { W: 2 } dst && dst.Sym == sym && dst.W == width
            && step.A is Ir.Cell a && a.Sym == sym && a.W == width
            && step.B is Ir.Imm { Value: 1 };
    }

    /// <summary>Komórki pętli nie występują nigdzie indziej w funkcji, a do jej etykiet nie skacze nikt obcy.</summary>
    /// <param name="function">Funkcja.</param>
    /// <param name="from">Początek dopasowania.</param>
    /// <param name="to">Koniec dopasowania.</param>
    /// <param name="syms">Symbole pętli.</param>
    /// <param name="top">Etykieta góry.</param>
    /// <param name="end">Etykieta końca.</param>
    /// <returns>Czy pojedyncze użycie.</returns>
    private static bool SingleUse(Ir.Function function, int from, int to, string[] syms, string top, string end)
    {
        for (int k = 0; k < function.Body.Count; k++)
        {
            if (k >= from && k <= to)
            {
                continue;
            }

            foreach (Ir.Op op in IrFacts.Operands(function.Body[k]))
            {
                if ((op is Ir.Cell cell && syms.Contains(cell.Sym)) || (op is Ir.AddrOf addr && syms.Contains(addr.Sym)))
                {
                    return false;
                }
            }

            if ((function.Body[k] is Ir.Jmp jmp && (jmp.Target == top || jmp.Target == end))
                || (function.Body[k] is Ir.BrCmp other && (other.Target == top || other.Target == end)))
            {
                return false;
            }
        }

        return true;
    }

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

    /// <summary>Operand jako słowo 16-bitowe dla <see cref="IPairMoves.TryMoveWord"/> albo <see langword="null"/> (komórka węższa, <c>volatile</c>).</summary>
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
        if (dst is not { } target || src is not { } source || _isa is not IPairMoves pairMoves || !pairMoves.TryMoveWord(target, source))
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
        _retJumps = 0;
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

        // v2: A wejściowe parkuje w EntryParkCell na czas pushy ramki (push czyta komórki przez A i by je
        // zniszczył); intake z rejestrów idzie PO pushach, żeby dziecko zastało zachowane argumenty rodzica
        // (callee-saved: push przed nadpisaniem — jak v1 czyta cc_argN po pushach). X pushe przeżywa.
        string? park = _isa.AbiV2 && function.Saved.Count > 0 && HasRegParams(function) ? _isa.EntryParkCell : null;
        if (park is not null)
        {
            StoreA(park);
        }

        foreach (Ir.Owned owned in function.Saved)
        {
            if (SavedWord(owned) is { } word && _isa is IPairStack pushStack && pushStack.TryPushWord(word))
            {
                continue;
            }

            foreach (string address in SavedBytes(owned))
            {
                LoadA(new Octet(false, address));
                _isa.PushA();
            }
        }

        bool unpark = park is not null;
        for (int i = 0; i < function.Params.Count; i++)
        {
            Ir.Cell param = function.Params[i];
            if (param.W > 2 || !_isa.Model.HasRegister(_isa.ArgCell(i, 0, param.W)))
            {
                continue;
            }

            for (int part = 0; part < param.W; part++)
            {
                string reg = _isa.ArgCell(i, part, param.W);
                if (unpark && reg == "a")
                {
                    _acc.Remove(park!); // StoreA dodał park, LoadA ma go wymusić (nie elidować)
                    LoadA(new Octet(false, park!));
                    unpark = false;
                }
                else
                {
                    _isa.FetchArg(reg);
                    _acc.Clear(); // FetchArg rusza A (txa), a LoadA ufa _acc
                }

                StoreA(Dst(param, part));
            }
        }

        for (int i = 0; i < function.Params.Count; i++)
        {
            Ir.Cell param = function.Params[i];
            if (param.W <= 2 && _isa.Model.HasRegister(_isa.ArgCell(i, 0, param.W)))
            {
                continue; // wzięte z rejestrów powyżej
            }

            if (param.Sym == _isa.ArgCell(i, 0, param.W) || (param.W == 2 && TryMoveWord(WordOf(param), Pair(_isa.ArgCell(i, 0, param.W), _isa.ArgCell(i, 1, param.W)))))
            {
                continue;
            }

            for (int part = 0; part < param.W; part++)
            {
                LoadA(new Octet(false, _isa.ArgCell(i, part, param.W)));
                StoreA(Dst(param, part));
            }
        }

        int bodyIndex = start;
        string? resCell = null;
        bool freshHL = false;
        bool endsWithJump = false;
        while (bodyIndex < function.Body.Count)
        {
            if (TryEmitTailCall(function, bodyIndex, out int next))
            {
                resCell = null;
                freshHL = false;
                endsWithJump = next == function.Body.Count;
                bodyIndex = next;
                continue;
            }

            if (TryEmitCopyLoop(function, bodyIndex, out int after))
            {
                resCell = null;
                freshHL = false;
                endsWithJump = false;
                bodyIndex = after;
                continue;
            }

            Ir.Ins current = function.Body[bodyIndex];
            if (current is Ir.Src)
            {
                // tylko komentarz: rejestr wyniku i pamięć bez zmian
                EmitIns(function, current, false);
                bodyIndex++;
                continue;
            }

            if (current is Ir.Ret ret2 && resCell is not null && ret2.W == 2 && ret2.Value is Ir.Cell cell2
                && cell2.Sym == resCell && cell2.W == 2 && cell2.Sym.StartsWith(function.Name + "__", StringComparison.Ordinal)
                && _isa is IResultReg retHolder && retHolder.ReturnsInResultReg && freshHL)
            {
                // rejestr wyniku trzyma wartość (świeży wynik Bin w HL): pomiń ładowanie
                EmitRet(function, ret2, bodyIndex == function.Body.Count - 1, valueInResultReg: true);
                resCell = null;
                freshHL = false;
                bodyIndex++;
                continue;
            }

            if (current is Ir.Jmp)
            {
                endsWithJump = bodyIndex == function.Body.Count - 1;
            }
            else
            {
                endsWithJump = false;
            }

            WordResult word = EmitIns(function, current, bodyIndex == function.Body.Count - 1);
            resCell = (word.Emitted && current is Ir.Bin bin && bin.Dst.W == 2) ? bin.Dst.Sym : null;
            freshHL = resCell is not null && word.InHL;
            bodyIndex++;
        }

        if (endsWithJump && _retJumps == 0 && function.Saved.Count == 0)
        {
            // nic nie skacze do etykiety powrotu, ramki brak, a ciało kończy skokiem bezwarunkowym:
            // stopka (etykieta + ret) nieosiągalna
            return;
        }

        Raw($"{Mangle(function, "ret")}:");
        bool keepResult = InResultReg(function.RetW);

        // Wynik w A parkuj raz na całą stopkę (nie na każdy bajt): scratch w stopce nie żyje
        // (wpisowy park dawno zdjęty intake), a X (starszy bajt) popów nie rusza.
        string? footerPark = keepResult && function.Saved.Count > 0 ? _isa.EntryParkCell : null;
        bool hoisted = footerPark is not null;
        if (hoisted)
        {
            StoreA(footerPark!);
        }

        foreach (Ir.Owned owned in function.Saved.Reverse())
        {
            if (SavedWord(owned) is { } word && _isa is IPairStack popStack && popStack.TryPopWord(word, keepResult && !hoisted))
            {
                continue;
            }

            foreach (string address in SavedBytes(owned).Reverse())
            {
                _isa.PopByte(address, keepResult && !hoisted);
                _acc.Clear();
            }
        }

        if (hoisted)
        {
            _acc.Remove(footerPark!);
            LoadA(new Octet(false, footerPark!));
        }

        _isa.Return();
    }

    /// <summary>Wynik tej szerokości wraca w rejestrze CPU (<see cref="IResultReg.ReturnsInResultReg"/>), nie w <c>cc_ret</c>.</summary>
    private bool InResultReg(int width) => _isa is IResultReg result && result.ReturnsInResultReg && width is 1 or 2;

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

    /// <summary>Porównanie ze znakiem z zerem bez odejmowania: jedna strona to stała 0,
    /// druga idzie do <see cref="ByteIsa.TryBranchZeroSigned"/> jako <c>wartość cond 0</c>.</summary>
    /// <param name="branch">Skok warunkowy.</param>
    /// <param name="target">Etykieta docelowa (zmanglowana).</param>
    /// <returns><see langword="true"/>, gdy sekwencja została wyemitowana.</returns>
    private bool TryBranchZeroSigned(Ir.BrCmp branch, string target)
    {
        bool zeroLeft = IsZero(branch.A);
        bool zeroRight = IsZero(branch.B);
        if (zeroLeft == zeroRight)
        {
            return false;
        }

        Ir.Op value = zeroLeft ? branch.B : branch.A;
        Ir.Cond cond = (zeroLeft, branch.C) switch
        {
            (true, Ir.Cond.Lt) => Ir.Cond.Gt,
            (true, Ir.Cond.Gt) => Ir.Cond.Lt,
            (true, Ir.Cond.Ge) => Ir.Cond.Le,
            (true, Ir.Cond.Le) => Ir.Cond.Ge,
            _ => branch.C,
        };
        if (WordOf(value) is not { } word || !_isa.TryBranchZeroSigned(word, cond, target))
        {
            return false;
        }

        _acc.Clear();
        return true;
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
        if (signed && width == 2 && TryBranchZeroSigned(branch, target))
        {
            return;
        }

        if (signed && width == 2 && _isa is ISignedBranch signedIsa
            && EmitBranchSignedConst(signedIsa, cond, branch.A, branch.B, width, target))
        {
            return;
        }

        bool swap = cond is Ir.Cond.Gt or Ir.Cond.Le or Ir.Cond.Gtu or Ir.Cond.Leu;
        bool onBorrow = cond is Ir.Cond.Lt or Ir.Cond.Gt or Ir.Cond.Ltu or Ir.Cond.Gtu;
        Ir.Op x = swap ? branch.B : branch.A;
        Ir.Op y = swap ? branch.A : branch.B;
        bool overflow = signed && _isa is ISignedBranch;
        bool bias = signed && !overflow;
        Octet[] xs;
        bool biasX = false;
        if (bias && _isa.XorPreservesCarry)
        {
            xs = BiasedBytes(x, width, true, out biasX);
        }
        else
        {
            xs = Bytes(x, width, bias ? "cc_t0" : null);
        }

        Octet[] ys = Bytes(y, width, bias ? "cc_t1" : null);
        EmitSubBytes(xs, ys, width, overflow, biasX);

        if (overflow)
        {
            ((ISignedBranch)_isa).JumpIfSigned(onBorrow, target);
            return;
        }

        _isa.JumpIf(onBorrow ? ByteFlag.Borrow : ByteFlag.NoBorrow, target);
    }

    /// <summary>Odejmowanie bajt po bajcie do porównania (ładowanie + sub/sbc); z biasX dokleja xor 128
    /// po załadowaniu najstarszego bajtu lewej strony (bias w A zamiast komórki scratch).</summary>
    /// <param name="xs">Bajty lewej strony.</param>
    /// <param name="ys">Bajty prawej strony.</param>
    /// <param name="width">Szerokość.</param>
    /// <param name="overflow">CPU z flagą V (odejmowanie zamiast CMP).</param>
    /// <param name="biasX">Doklej xor 128 do najstarszego bajtu lewej strony.</param>
    private void EmitSubBytes(Octet[] xs, Octet[] ys, int width, bool overflow, bool biasX)
    {
        for (int i = 0; i < width; i++)
        {
            LoadA(xs[i]);
            if (i == width - 1 && biasX)
            {
                Alu(ByteAlu.Xor, new Octet(true, "128"), true);
            }

            if (width == 1 && !overflow)
            {
                Cmp(ys[i]);
            }
            else
            {
                Alu(ByteAlu.Sub, ys[i], i == 0);
            }
        }
    }

    /// <summary>Porównanie ze znakiem ze stałą 16-bitową na CPU z flagą V: normalizacja do <c>Lt</c>/<c>Ge</c>
    /// ze stałą po prawej (<c>Le</c>/<c>Gt</c> przez +1, gdy wynik mieści się w int), odejmowanie wspólną pętlą
    /// i skoki prymitywem bez trampoliny (V rozstrzyga samo). Zwraca <c>false</c> (wołający idzie starą drogą),
    /// gdy brak stałej W2 albo +1 nielegalne.</summary>
    /// <param name="isa">ISA ze zdolnością <see cref="ISignedBranch"/>.</param>
    /// <param name="cond">Warunek.</param>
    /// <param name="a">Lewy operand.</param>
    /// <param name="b">Prawy operand.</param>
    /// <param name="width">Szerokość (zawsze 2).</param>
    /// <param name="target">Etykieta gałęzi prawdy (spadek to fałsz).</param>
    /// <returns>Czy wyemitowano porównanie.</returns>
    private bool EmitBranchSignedConst(ISignedBranch isa, Ir.Cond cond, Ir.Op a, Ir.Op b, int width, string target)
    {
        // Stała jako Imm W1/W2 (węższe ujemne nie docierają tu: promocja typów poszerza je ze znakiem
        // do W2 wcześniej, co widać po sub/sbc z pełnym 0xFFFF dla -1); wartość z bajtów faktycznie
        // odejmowanych (niespójność reprezentacji wykluczona z konstrukcji).
        Ir.Op? constSide = a is Ir.Imm ? a : b is Ir.Imm ? b : null;
        if (constSide is not Ir.Imm imm || (imm.W != 1 && imm.W != 2))
        {
            return false;
        }

        if (!Const16(ByteOf(constSide, 0), ByteOf(constSide, 1), out int raw))
        {
            return false;
        }

        if (imm.W == 1 && raw > 0xFF)
        {
            return false;
        }

        int c = imm.W == 2 ? (short)raw : raw;

        bool constLeft = ReferenceEquals(constSide, a);
        bool less;
        int adjusted;
        switch (cond)
        {
            case Ir.Cond.Lt when !constLeft: less = true; adjusted = c; break;
            case Ir.Cond.Ge when !constLeft: less = false; adjusted = c; break;
            case Ir.Cond.Le when !constLeft && c <= 32766: less = true; adjusted = c + 1; break;
            case Ir.Cond.Gt when !constLeft && c <= 32766: less = false; adjusted = c + 1; break;
            case Ir.Cond.Lt when constLeft && c <= 32766: less = false; adjusted = c + 1; break;
            case Ir.Cond.Ge when constLeft && c <= 32766: less = true; adjusted = c + 1; break;
            case Ir.Cond.Le when constLeft: less = false; adjusted = c; break;
            case Ir.Cond.Gt when constLeft: less = true; adjusted = c; break;
            default: return false;
        }

        Octet[] xs = [ByteOf(constLeft ? b : a, 0), ByteOf(constLeft ? b : a, 1)];
        Octet[] ys = [new Octet(true, Number(adjusted & 0xFF)), new Octet(true, Number((adjusted >> 8) & 0xFF))];
        EmitSubBytes(xs, ys, width, overflow: true, biasX: false);
        if (!isa.TryBranchSignedConst(less, adjusted, target))
        {
            isa.JumpIfSigned(less, target);
        }

        return true;
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

    /// <summary>Bajty operandu do porównania ze znakiem bez komórki scratch: stały najstarszy bajt odwrócony
    /// w miejscu (xor 128 jak w <see cref="Bytes"/>), nie-stały wołający dokończy xorem po LoadA (biasInLoop,
    /// doklejane w <see cref="EmitSubBytes"/>).</summary>
    /// <param name="op">Operand.</param>
    /// <param name="width">Szerokość.</param>
    /// <param name="wantBias">Czy odwracać najstarszy bajt.</param>
    /// <param name="biasInLoop">Nie-stały wierzchołek wymaga xora w pętli.</param>
    /// <returns>Bajty operandu.</returns>
    private Octet[] BiasedBytes(Ir.Op op, int width, bool wantBias, out bool biasInLoop)
    {
        Octet[] bytes = [.. Enumerable.Range(0, width).Select(i => ByteOf(op, i))];
        biasInLoop = false;
        if (!wantBias)
        {
            return bytes;
        }

        Octet top = bytes[width - 1];
        if (top.IsImmediate && int.TryParse(top.Text, CultureInfo.InvariantCulture, out int value))
        {
            bytes[width - 1] = new Octet(true, Number(value ^ 0x80));
            return bytes;
        }

        biasInLoop = true;
        return bytes;
    }

    /// <summary>Pętla kopiująca bajty (<c>while (n) { *d = *s; d++; s++; n--; }</c> albo z <c>n &gt; 0</c>)
    /// jako blok z prymitywu <see cref="ICopyLoop.TryCopyLoop"/> (dziś tylko Z80 z <c>ldir</c>; reszta nie ma
    /// <c>false</c> i pętla idzie starą drogą). Warunki: elementy W1, wskaźniki i licznik W2, ciało dokładnie
    /// [Load, Store, d+1, s+1, n-1], brak innych odwołań do d/s/n/t w funkcji (writeback zbędny) i brak obcych
    /// skoków do etykiet pętli. Licznik <c>Eq</c> ze znakiem ujemnym zawiesiłby oryginał (nieskończona pętla),
    /// więc zamiana jest nieobserwowalna w każdym kończącym się programie.</summary>
    /// <param name="function">Emitowana funkcja.</param>
    /// <param name="index">Pozycja kandydata (etykieta pętli lub wcześniejszy komentarz).</param>
    /// <param name="next">Pozycja za etykietą końca (gdy dopasowano).</param>
    /// <returns>Czy wyemitowano blok kopiujący.</returns>
    private bool TryEmitCopyLoop(Ir.Function function, int index, out int next)
    {
        next = index;
        int i = index;
        var comments = new List<Ir.Src>();
        while (i < function.Body.Count && function.Body[i] is Ir.Src lead)
        {
            comments.Add(lead);
            i++;
        }

        if (i >= function.Body.Count || function.Body[i] is not Ir.Label top)
        {
            return false;
        }

        i++;
        string? end = null;
        Ir.Cell? n = null;
        if (!SkipComments(function, comments, ref i) || function.Body[i] is not Ir.BrCmp branch
            || branch.C is not (Ir.Cond.Eq or Ir.Cond.Le) || branch.A is not Ir.Cell count || count.W != 2
            || branch.B is not Ir.Imm { Value: 0 })
        {
            return false;
        }

        end = branch.Target;
        n = count;
        i++;
        if (!SkipComments(function, comments, ref i) || function.Body[i] is not Ir.Load load
            || load.Dst is not { W: 1 } t || load.Ptr is not Ir.Cell { W: 2 } s || load.Off != 0
            || load.Bytes != 1 || load.Volatile)
        {
            return false;
        }

        i++;
        if (!SkipComments(function, comments, ref i) || function.Body[i] is not Ir.Store store
            || store.Ptr is not Ir.Cell { W: 2 } d || store.Off != 0 || store.Value is not Ir.Cell vt
            || vt.W != 1 || vt.Sym != t.Sym || store.Bytes != 1 || store.Volatile)
        {
            return false;
        }

        string dst = d.Sym;
        string src = s.Sym;
        i++;
        if (!SkipComments(function, comments, ref i) || !IsStep(function.Body[i], dst, 2, true))
        {
            return false;
        }

        i++;
        if (!SkipComments(function, comments, ref i) || !IsStep(function.Body[i], src, 2, true))
        {
            return false;
        }

        i++;
        if (!SkipComments(function, comments, ref i) || !IsStep(function.Body[i], n.Sym, 2, false))
        {
            return false;
        }

        i++;
        if (!SkipComments(function, comments, ref i) || function.Body[i] is not Ir.Jmp jmp || jmp.Target != top.Name)
        {
            return false;
        }

        i++;
        if (!SkipComments(function, comments, ref i) || function.Body[i] is not Ir.Label endLabel || endLabel.Name != end)
        {
            return false;
        }

        if (dst == src || dst == n.Sym || src == n.Sym)
        {
            return false;
        }

        if (!SingleUse(function, index, i, [dst, src, n.Sym, t.Sym], top.Name, end))
        {
            return false;
        }

        Word? dstWord = Pair(_isa.Loc(dst, 2, 0), _isa.Loc(dst, 2, 1));
        Word? srcWord = Pair(_isa.Loc(src, 2, 0), _isa.Loc(src, 2, 1));
        Word? countWord = Pair(_isa.Loc(n.Sym, 2, 0), _isa.Loc(n.Sym, 2, 1));
        if (dstWord is not { } dstW || srcWord is not { } srcW || countWord is not { } countW)
        {
            return false;
        }

        foreach (Ir.Src comment in comments)
        {
            EmitSource(comment);
        }

        if (_isa is not ICopyLoop copy || !copy.TryCopyLoop(dstW, srcW, countW))
        {
            return false;
        }

        _acc.Clear();
        Raw($"{Mangle(function, end)}:");
        next = i + 1;
        return true;
    }

    /// <summary>Wywołanie ogonowe: <c>Call</c> z wynikiem i zaraz <c>Ret</c> tej samej wartości
    /// zamienia w skok (bez call/ret i bez przenoszenia wyniku). Bezpieczne tylko bez ramki
    /// (<see cref="Ir.Function.Saved"/> puste), bez zapisów rejestrów wokół wołania i dla wyniku
    /// void albo 1-2 bajtów (już w miejscu docelowym).</summary>
    /// <param name="function">Emitowana funkcja.</param>
    /// <param name="index">Pozycja kandydata na <c>Call</c>.</param>
    /// <param name="next">Pozycja za zużytym <c>Ret</c> (gdy dopasowano).</param>
    /// <returns>Czy wyemitowano skok ogonowy.</returns>
    private bool TryEmitTailCall(Ir.Function function, int index, out int next)
    {
        next = index;
        if (!_isa.SupportsTailCall || function.Body[index] is not Ir.Call call)
        {
            return false;
        }

        int retIndex = index + 1;
        var skipped = new List<Ir.Src>();
        while (retIndex < function.Body.Count && function.Body[retIndex] is Ir.Src comment)
        {
            skipped.Add(comment);
            retIndex++;
        }

        if (retIndex >= function.Body.Count || function.Body[retIndex] is not Ir.Ret ret)
        {
            return false;
        }

        bool voidTail = call.Result is null && ret.Value is null && ret.W == 0;
        bool valueTail = call.Result is { } result && ret.Value is Ir.Cell value
            && value.Sym == result.Sym && value.W == result.W && ret.W == result.W && result.W is 1 or 2;
        if (!voidTail && !valueTail)
        {
            return false;
        }

        if (function.Saved.Count != 0 || _isa.SavedAround(call).Count != 0)
        {
            return false;
        }

        // Wynik helpera v1 wraca przez cc_ret, więc skok ogonowy do niego gubiłby wynik w rejestrach
        // (void przechodzi: bez wyniku wystarczy jmp z argumentami w cc_argN).
        bool regCall = RegCall(call);
        if (!regCall && !voidTail)
        {
            return false;
        }

        foreach (Ir.Src comment in skipped)
        {
            EmitSource(comment);
        }

        bool earlyFp = false;
        if (call.Indirect is { } indirect && HasRegArgs(call))
        {
            earlyFp = true;
            _usesIcall = true;
            _isa.SetupFp(_isa.Sym(indirect.Sym));
            _acc.Clear(); // SetupFp ładuje A, a EmitCallArgs zaczyna od LoadA
        }

        EmitCallArgs(call);
        if (call.Indirect is not null)
        {
            if (earlyFp)
            {
                _isa.Jump("__icall");
            }
            else
            {
                _usesIcall = true;
                _isa.TailCallIndirect(_isa.Sym(call.Indirect.Sym));
            }
        }
        else
        {
            _isa.TailCall(_isa.Sym(call.Direct!));
        }

        next = retIndex + 1;
        return true;
    }

    /// <summary>Ustawia argumenty wołania w komórkach <c>cc_argN</c> (bez zapisów rejestrów i bez samego skoku).</summary>
    private void EmitCallArgs(Ir.Call call)
    {
        if (RegSourcesClobberedByMemArgs(call))
        {
            // ParamAlias położył źródło argumentu rejestrowego na komórce docelowej argumentu pamięciowego
            // (np. apply__a to cc_arg2, a b ląduje w cc_arg2): rejestry najpierw, młodszy bajt (A) na stos
            // przez fazę pamięci (X faza pamięci nie rusza — tylko A).
            EmitRegArgs(call);
            _isa.PushA();
            EmitMemArgs(call);
            _isa.PopA();
            _acc.Clear();
            return;
        }

        EmitMemArgs(call);
        EmitRegArgs(call);
    }

    /// <summary>Czy faza pamięci nadpisałaby źródła argumentów rejestrowych (kolizja przez ParamAlias).</summary>
    /// <param name="call">Wołanie.</param>
    /// <returns>Czy któreś źródło rejestrowe leży na komórce docelowej argumentu pamięciowego.</returns>
    private bool RegSourcesClobberedByMemArgs(Ir.Call call)
    {
        var memDests = new HashSet<string>(StringComparer.Ordinal);
        for (int j = 0; j < call.Args.Count; j++)
        {
            if (IsRegArg(call, j))
            {
                continue;
            }

            for (int part = 0; part < call.ParamWidths[j]; part++)
            {
                memDests.Add(Base(CallArgCell(call, j, part, call.ParamWidths[j])));
            }
        }

        if (memDests.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < call.Args.Count; i++)
        {
            if (!IsRegArg(call, i))
            {
                continue;
            }

            for (int part = 0; part < call.ParamWidths[i]; part++)
            {
                Octet src = ByteOf(call.Args[i], part);
                if (!src.IsImmediate && memDests.Contains(Base(src.Text)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Argumenty pamięciowe wołania (kolejno, jak v1 — bezpieczne między sobą).</summary>
    /// <param name="call">Wołanie.</param>
    private void EmitMemArgs(Ir.Call call)
    {
        for (int i = 0; i < call.Args.Count; i++)
        {
            if (IsRegArg(call, i))
            {
                continue; // rejestry osobną fazą (liczenie kolejnych argumentów niszczy A/X)
            }

            if (call.Args[i] is Ir.Cell same && same.W == call.ParamWidths[i] && same.Sym == CallArgCell(call, i, 0, call.ParamWidths[i]))
            {
                // parametr funkcji zaaliasowany na cc_argN jest już w komórce argumentu na tej samej pozycji
                continue;
            }

            if (call.ParamWidths[i] == 2 && TryMoveWord(Pair(CallArgCell(call, i, 0, call.ParamWidths[i]), CallArgCell(call, i, 1, call.ParamWidths[i])), WordOf(call.Args[i])))
            {
                continue;
            }

            for (int part = 0; part < call.ParamWidths[i]; part++)
            {
                LoadA(ByteOf(call.Args[i], part));
                StoreA(CallArgCell(call, i, part, call.ParamWidths[i]));
            }
        }
    }

    /// <summary>Argumenty rejestrowe wołania (po fazie pamięci; A kończy z młodszym bajtem).</summary>
    /// <param name="call">Wołanie.</param>
    private void EmitRegArgs(Ir.Call call)
    {
        for (int i = 0; i < call.Args.Count; i++)
        {
            if (!IsRegArg(call, i))
            {
                continue;
            }

            // wartość do A, potem do rejestru; części od starszej (A kończy z młodszym)
            for (int part = call.ParamWidths[i] - 1; part >= 0; part--)
            {
                LoadA(ByteOf(call.Args[i], part));
                _isa.StoreArg(CallArgCell(call, i, part, call.ParamWidths[i]));
                _acc.Clear(); // StoreArg rusza A (tax), a LoadA ufa _acc
            }
        }
    }

    /// <summary>Lokalizacja argumentu wołania: rejestry dla wołań modułowych v2, komórki pamięci
    /// dla helperów v1 (i zawsze na v1) — czysta decyzja lokalna zamiast mutowanej flagi w ISA.</summary>
    /// <param name="call">Wołanie.</param>
    /// <param name="index">Numer argumentu.</param>
    /// <param name="part">Numer bajtu.</param>
    /// <param name="width">Szerokość argumentu.</param>
    /// <returns>Rejestr albo symbol komórki.</returns>
    private string CallArgCell(Ir.Call call, int index, int part, int width) =>
        RegCall(call) ? _isa.ArgCell(index, part, width) : _isa.MemArgCell(index, part);

    /// <summary>Argument wołania jedzie rejestrem (v2): wołanie modułowe, szerokość pasuje i model zna rejestr.</summary>
    /// <param name="call">Wołanie.</param>
    /// <param name="index">Numer argumentu.</param>
    /// <returns>Czy argument idzie rejestrem.</returns>
    private bool IsRegArg(Ir.Call call, int index) =>
        RegCall(call) && call.ParamWidths[index] <= 2 && _isa.Model.HasRegister(_isa.ArgCell(index, 0, call.ParamWidths[index]));

    /// <summary>Wołanie niesie argumenty w rejestrach (v2): co najmniej jeden pasuje.</summary>
    /// <param name="call">Wołanie.</param>
    /// <returns>Czy któryś argument jedzie rejestrem.</returns>
    private bool HasRegArgs(Ir.Call call)
    {
        for (int i = 0; i < call.Args.Count; i++)
        {
            if (IsRegArg(call, i))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Wołanie rozumie rejestry v2: cel zdefiniowany w module albo wskaźnik (funkcje mini-C);
    /// helpery uruchomieniowe w ręcznym asemblerze (v1) marszowane są przez <c>cc_argN</c>/<c>cc_ret</c>.
    /// Na v1 zawsze prawda (brak zmiany zachowania Z80/8080, których helpery mówią rejestrami).</summary>
    /// <param name="call">Wołanie.</param>
    /// <returns>Czy argumenty i wynik mogą iść rejestrami.</returns>
    private bool RegCall(Ir.Call call) =>
        !_isa.AbiV2 || call.Indirect is not null || (call.Direct is { } direct && _defined.Contains(direct));

    /// <summary>Funkcja ma parametry niesione rejestrami (v2): co najmniej jeden pasuje.</summary>
    /// <param name="function">Funkcja.</param>
    /// <returns>Czy któryś parametr jedzie rejestrem.</returns>
    private bool HasRegParams(Ir.Function function)
    {
        for (int i = 0; i < function.Params.Count; i++)
        {
            Ir.Cell param = function.Params[i];
            if (param.W <= 2 && _isa.Model.HasRegister(_isa.ArgCell(i, 0, param.W)))
            {
                return true;
            }
        }

        return false;
    }

    private void EmitCall(Ir.Call call)
    {
        // v2: wskaźnik pośredni do cc_fp PRZED argumentami (ich ustawianie niszczy A)
        bool earlyFp = false;
        if (call.Indirect is { } indirect && HasRegArgs(call))
        {
            earlyFp = true;
            _usesIcall = true;
            _isa.SetupFp(_isa.Sym(indirect.Sym));
            _acc.Clear(); // SetupFp ładuje A, a EmitCallArgs zaczyna od LoadA
        }

        EmitCallArgs(call);

        // rejestry komórek żywych za wołaniem: na stos po argumentach, ze stosu przed zapisem wyniku (wynik może leżeć w tej parze)
        IReadOnlyList<string> saved = _isa.SavedAround(call);
        if (_isa is IPairStack pairPush)
        {
            foreach (string pair in saved)
            {
                pairPush.PushPair(pair);
            }
        }

        if (call.Indirect is not null)
        {
            if (earlyFp)
            {
                CallDirect("__icall");
            }
            else
            {
                _usesIcall = true;
                CallIndirect(_isa.Sym(call.Indirect.Sym));
            }
        }
        else
        {
            CallDirect(_isa.Sym(call.Direct!));
        }

        if (_isa is IPairStack pairPop)
        {
            foreach (string pair in saved.Reverse())
            {
                pairPop.PopPair(pair);
            }
        }

        if (call.Result is not null && RegCall(call) && InResultReg(call.Result.W))
        {
            if (call.Result.W == 2 && WordOf(call.Result) is { } dst && _isa is IResultPairs resultPairs && resultPairs.TryMoveFromResultReg(dst))
            {
                _acc.Remove(dst.Lo);
                _acc.Remove(dst.Hi);
                return;
            }

            for (int part = 0; part < call.Result.W; part++)
            {
                ((IResultReg)_isa).ResultByteToA(part);
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

    private void EmitRet(Ir.Function function, Ir.Ret ret, bool last, bool valueInResultReg = false)
    {
        if (!valueInResultReg && ret.Value is not null && InResultReg(ret.W))
        {
            if (!(ret.W == 2 && WordOf(ret.Value) is { } word && _isa is IResultPairs resultPairs && resultPairs.TryMoveToResultReg(word)))
            {
                // v2/6502: od starszego, bo A niesie po jednym bajcie (A kończy z młodszym, X ze starszym,
                // oba przeżywają restore w stopce); v1: bez zmian (Z80 ładuje L i H niezależnie).
                int from = _isa.AbiV2 ? ret.W - 1 : 0;
                int end = _isa.AbiV2 ? -1 : ret.W;
                int step = _isa.AbiV2 ? -1 : 1;
                for (int part = from; part != end; part += step)
                {
                    LoadA(ByteOf(ret.Value, part));
                    ((IResultReg)_isa).ResultByteFromA(part);
                    if (_isa.AbiV2)
                    {
                        _acc.Clear(); // tax rusza A, a następne LoadA ufa _acc
                    }
                }
            }
        }
        else if (!valueInResultReg && ret.Value is not null && !(ret.W == 2 && TryMoveWord(Pair(RetSym(0), RetSym(1)), WordOf(ret.Value))))
        {
            for (int part = 0; part < ret.W; part++)
            {
                LoadA(ByteOf(ret.Value, part));
                StoreA(RetSym(part));
            }
        }

        if (!last)
        {
            _retJumps++;
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
