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

    private int _labels;

    private bool _usesIcall;

    public ByteSelector(Ir.Module module, ByteIsa isa)
    {
        _module = module;
        _isa = isa;
    }

    /// <summary>Składa asembler modułu.</summary>
    /// <returns>Tekst dla asemblera CPU.</returns>
    public string Emit()
    {
        foreach (Ir.Function function in _module.Functions)
        {
            EmitFunction(function);
        }

        return Header() + _isa.Text + PrintInit() + PrintData() + PrintBss();
    }

    private static string At(string sym, int offset) =>
        offset == 0 ? sym : $"{sym}{(offset > 0 ? "+" : "-")}{Math.Abs(offset)}";

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

    private void EmitFunction(Ir.Function function)
    {
        int start = 0;
        if (function.Body.Count > 0 && function.Body[0] is Ir.Src leading)
        {
            EmitSource(leading);
            start = 1;
        }

        if (!function.IsStatic)
        {
            _isa.Raw(_isa.Global(function.Name));
        }

        _isa.Raw($"{function.Name}:");
        foreach (Ir.Owned owned in function.Saved)
        {
            foreach (string address in SavedBytes(owned))
            {
                _isa.LoadA(new Octet(false, address));
                _isa.PushA();
            }
        }

        for (int i = 0; i < function.Params.Count; i++)
        {
            Ir.Cell param = function.Params[i];
            for (int part = 0; part < param.W; part++)
            {
                _isa.LoadA(new Octet(false, ArgSym(i, part)));
                _isa.StoreA(Dst(param, part));
            }
        }

        for (int i = start; i < function.Body.Count; i++)
        {
            EmitIns(function, function.Body[i], i == function.Body.Count - 1);
        }

        _isa.Raw($"{Mangle(function, "ret")}:");
        foreach (Ir.Owned owned in function.Saved.Reverse())
        {
            foreach (string address in SavedBytes(owned).Reverse())
            {
                _isa.PopA();
                _isa.StoreA(address);
            }
        }

        _isa.Return();
    }

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
        _isa.Raw(source.File is null ? $";c:{source.Line}" : $";c:{source.File}:{source.Line}");

    private void EmitIns(Ir.Function function, Ir.Ins ins, bool last)
    {
        switch (ins)
        {
            case Ir.Src source:
                EmitSource(source);
                break;
            case Ir.Label label:
                _isa.Raw($"{Mangle(function, label.Name)}:");
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
        if (src is Ir.Cell same && same.Sym == dst.Sym)
        {
            return;
        }

        for (int i = 0; i < dst.W; i++)
        {
            _isa.LoadA(ByteOf(src, i));
            _isa.StoreA(Dst(dst, i));
        }
    }

    private void EmitBin(Ir.Bin bin)
    {
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
        for (int i = 0; i < bin.Dst.W; i++)
        {
            _isa.LoadA(ByteOf(bin.A, i));
            _isa.Alu(alu, ByteOf(bin.B, i), i == 0);
            _isa.StoreA(Dst(bin.Dst, i));
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
                _isa.LoadA(Zero());
                _isa.StoreA(Dst(bin.Dst, i));
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
                    _isa.LoadA(i >= bytes ? new Octet(false, Dst(bin.Dst, i - bytes)) : Zero());
                    _isa.StoreA(Dst(bin.Dst, i));
                }
            }
            else
            {
                for (int i = 0; i < width; i++)
                {
                    _isa.LoadA(i + bytes < width ? new Octet(false, Dst(bin.Dst, i + bytes)) : Zero());
                    _isa.StoreA(Dst(bin.Dst, i));
                }
            }
        }

        for (int bit = 0; bit < n % 8; bit++)
        {
            for (int k = 0; k < width; k++)
            {
                int i = left ? k : width - 1 - k;
                _isa.LoadA(new Octet(false, Dst(bin.Dst, i)));
                if (left)
                {
                    _isa.ShlA(k == 0);
                }
                else
                {
                    _isa.ShrA(k == 0);
                }

                _isa.StoreA(Dst(bin.Dst, i));
            }
        }
    }

    private void EmitUn(Ir.Un un)
    {
        for (int i = 0; i < un.Dst.W; i++)
        {
            if (un.Kind == Ir.UnOp.Neg)
            {
                _isa.LoadA(Zero());
                _isa.Alu(ByteAlu.Sub, ByteOf(un.A, i), i == 0);
            }
            else
            {
                _isa.LoadA(ByteOf(un.A, i));
                _isa.Alu(ByteAlu.Xor, new Octet(true, "255"), true);
            }

            _isa.StoreA(Dst(un.Dst, i));
        }
    }

    private void EmitLoad(Ir.Load load)
    {
        if (load.Ptr is Ir.AddrOf address)
        {
            for (int i = 0; i < load.Dst.W; i++)
            {
                _isa.LoadA(i < load.Bytes ? new Octet(false, At(address.Sym, address.Off + load.Off + MemIndex(i, load.Bytes))) : Zero());
                _isa.StoreA(Dst(load.Dst, i));
            }

            return;
        }

        var pointer = (Ir.Cell)load.Ptr;
        _isa.PtrSetup(pointer.Sym, load.Off);
        for (int i = 0; i < load.Dst.W; i++)
        {
            if (i < load.Bytes)
            {
                _isa.PtrLoad(MemIndex(i, load.Bytes));
            }
            else
            {
                _isa.LoadA(Zero());
            }

            _isa.StoreA(Dst(load.Dst, i));
        }
    }

    private void EmitStore(Ir.Store store)
    {
        if (store.Ptr is Ir.AddrOf address)
        {
            for (int i = 0; i < store.Bytes; i++)
            {
                _isa.LoadA(ByteOf(store.Value, i));
                _isa.StoreA(At(address.Sym, address.Off + store.Off + MemIndex(i, store.Bytes)));
            }

            return;
        }

        var pointer = (Ir.Cell)store.Ptr;
        _isa.PtrSetup(pointer.Sym, store.Off);
        for (int i = 0; i < store.Bytes; i++)
        {
            _isa.LoadA(ByteOf(store.Value, i));
            _isa.PtrStore(MemIndex(i, store.Bytes));
        }
    }

    private void EmitBranch(Ir.Function function, Ir.BrCmp branch)
    {
        string target = Mangle(function, branch.Target);
        int width = Math.Max(WidthOf(branch.A), WidthOf(branch.B));
        Ir.Cond cond = branch.C;
        if (cond is Ir.Cond.Eq or Ir.Cond.Ne)
        {
            string skip = Label("ne");
            for (int i = 0; i < width; i++)
            {
                _isa.LoadA(ByteOf(branch.A, i));
                _isa.Cmp(ByteOf(branch.B, i));
                _isa.JumpIf(ByteFlag.NotZero, cond == Ir.Cond.Eq ? skip : target);
            }

            if (cond == Ir.Cond.Eq)
            {
                _isa.Jump(target);
                _isa.Raw($"{skip}:");
            }

            return;
        }

        bool signed = cond is Ir.Cond.Lt or Ir.Cond.Le or Ir.Cond.Gt or Ir.Cond.Ge;
        bool swap = cond is Ir.Cond.Gt or Ir.Cond.Le or Ir.Cond.Gtu or Ir.Cond.Leu;
        bool onBorrow = cond is Ir.Cond.Lt or Ir.Cond.Gt or Ir.Cond.Ltu or Ir.Cond.Gtu;
        Ir.Op x = swap ? branch.B : branch.A;
        Ir.Op y = swap ? branch.A : branch.B;
        Octet[] xs = Bytes(x, width, signed ? "cc_t0" : null);
        Octet[] ys = Bytes(y, width, signed ? "cc_t1" : null);
        for (int i = 0; i < width; i++)
        {
            _isa.LoadA(xs[i]);
            if (width == 1)
            {
                _isa.Cmp(ys[i]);
            }
            else
            {
                _isa.Alu(ByteAlu.Sub, ys[i], i == 0);
            }
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

        _isa.LoadA(top);
        _isa.Alu(ByteAlu.Xor, new Octet(true, "128"), true);
        _isa.StoreA(biasCell);
        bytes[width - 1] = new Octet(false, biasCell);
        return bytes;
    }

    private void EmitCall(Ir.Call call)
    {
        for (int i = 0; i < call.Args.Count; i++)
        {
            for (int part = 0; part < call.ParamWidths[i]; part++)
            {
                _isa.LoadA(ByteOf(call.Args[i], part));
                _isa.StoreA(ArgSym(i, part));
            }
        }

        if (call.Indirect is not null)
        {
            _usesIcall = true;
            _isa.CallIndirect(call.Indirect.Sym);
        }
        else
        {
            _isa.Call(call.Direct!);
        }

        if (call.Result is not null)
        {
            for (int part = 0; part < call.Result.W; part++)
            {
                _isa.LoadA(new Octet(false, RetSym(part)));
                _isa.StoreA(Dst(call.Result, part));
            }
        }
    }

    private void EmitRet(Ir.Function function, Ir.Ret ret, bool last)
    {
        if (ret.Value is not null)
        {
            for (int part = 0; part < ret.W; part++)
            {
                _isa.LoadA(ByteOf(ret.Value, part));
                _isa.StoreA(RetSym(part));
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
            text.AppendLine(_isa.Extern(function));
        }

        foreach (string external in _module.ExternCells)
        {
            text.AppendLine(_isa.Extern(external));
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
            text.AppendLine(_isa.Extern("__icall"));
            text.AppendLine(_isa.Extern("cc_fp"));
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
        text.AppendLine(_isa.Segment("BSS"));
        foreach (Ir.Data data in _module.Data.Where(static d => d.Segment == "BSS"))
        {
            if (data.Exported)
            {
                text.AppendLine(_isa.Global(data.Sym));
            }

            text.AppendLine($"{data.Sym}: {_isa.Reserve(data.Size)}");
        }

        if (!_module.ObjectMode)
        {
            text.AppendLine("__bss_end:");
        }

        return text.ToString();
    }

    private void AppendData(StringBuilder text, Ir.Data data)
    {
        if (data.Exported)
        {
            text.AppendLine(_isa.Global(data.Sym));
        }

        string label = data.Sym.Length == 0 ? string.Empty : $"{data.Sym}: ";
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
