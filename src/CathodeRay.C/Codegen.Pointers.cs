using System.Text;

namespace CathodeRay.C;

/// <summary>Generator kodu: adresy, wskaźniki i tablice.</summary>
public sealed partial class Codegen
{
    /// <summary>Adres labela do pary (ukryta komórka .word, linker wypełnia).</summary>
    private void EmitAddressOf(string cellLabel, string lo, string hi)
    {
        string addr = $"{_prefix}__addr{++_addrs}";
        _words.Add((addr, cellLabel));
        _code.AppendLine($"LDA {addr}");
        _code.AppendLine($"STA {lo}");
        _code.AppendLine($"LDA {addr}+1");
        _code.AppendLine($"STA {hi}");
    }

    /// <summary>Obniża <c>*p op= v</c> do <c>*h = *h op v</c>, gdzie ukryty wskaźnik <c>h</c> dostaje adres
    /// celu raz (skutki uboczne w celu, np. <c>a[i++] += 1</c>, wykonują się jednokrotnie).</summary>
    private Ast.AssignTo LowerAssignOp(Ast.AssignOpTo node, int depth)
    {
        CType elem = _types.TryGetValue(node.Target, out CType? found) ? found : CType.UChar;
        string name = $"__ao{_assignOps++}";
        string cell = $"{_prefix}__{name}";
        CType pointer = CType.Pointer(elem);
        DataCell(cell, CType.UChar);
        DataCell($"{cell}_h", CType.UChar);
        _extraCells.Add(cell);
        _extraCells.Add($"{cell}_h");
        _cells[name] = new Cell(cell, pointer);
        EvalPtrAddr(node.Target, depth, out string lo, out string hi);
        _code.AppendLine($"LDA {lo}");
        _code.AppendLine($"STA {cell}");
        _code.AppendLine($"LDA {hi}");
        _code.AppendLine($"STA {cell}_h");
        var hidden = new Ast.Var(name);
        _types[hidden] = pointer;
        var store = new Ast.Deref(hidden);
        _types[store] = elem;
        var load = new Ast.Deref(hidden);
        _types[load] = elem;
        var combined = new Ast.Binary(node.Op, load, node.Value);
        _types[combined] = _types.TryGetValue(node.Combined, out CType? result) ? result : elem;
        var assign = new Ast.AssignTo(store, combined);
        _types[assign] = elem;
        return assign;
    }

    /// <summary>Podwaja parę (lo,hi) w miejscu (skala x2 dla int*).</summary>
    private void DoublePair(string lo, string hi)
    {
        _code.AppendLine("LDX 0");
        _code.AppendLine($"LDA {lo}");
        _code.AppendLine($"ADD {lo},X");
        _code.AppendLine($"STA {lo}");
        _code.AppendLine($"LDA {hi}");
        _code.AppendLine($"ADC {hi},X");
        _code.AppendLine($"STA {hi}");
    }

    /// <summary>Liczy adres celu (*p lub p[i]) do pary Temp(depth).</summary>
    private void EvalPtrAddr(Ast.Expr target, int depth, out string lo, out string hi)
    {
        lo = Temp(depth, hi: false);
        hi = Temp(depth, hi: true);
        if (target is Ast.Deref deref)
        {
            EvalInt(deref.Pointer, depth + 1, out string plo, out string phi);
            _code.AppendLine($"LDA {plo}");
            _code.AppendLine($"STA {lo}");
            _code.AppendLine($"LDA {phi}");
            _code.AppendLine($"STA {hi}");
            return;
        }

        if (target is Ast.Index index)
        {
            EvalInt(index.Base, depth + 1, out string blo, out string bhi);
            EvalInt(index.Offset, depth + 2, out string ilo, out string ihi);
            if (ElemSize(TypeOfIndex(index)) == 2)
            {
                DoublePair(ilo, ihi);
            }

            _code.AppendLine("LDX 0");
            _code.AppendLine($"LDA {blo}");
            _code.AppendLine($"ADD {ilo},X");
            _code.AppendLine($"STA {lo}");
            _code.AppendLine($"LDA {bhi}");
            _code.AppendLine($"ADC {ihi},X");
            _code.AppendLine($"STA {hi}");
            return;
        }

        throw new CCodegenException($"pointer target '{target.GetType().Name}' not supported.");
    }

    /// <summary>Czyta spod adresu (łatany operand, wzorzec divmod); nadpisuje parę.</summary>
    private void PatchedLoad(string addrLo, string addrHi, int elemSize, string outLo, string outHi, int depth)
    {
        string tmp = Temp(depth + 3, hi: false);
        string tmph = Temp(depth + 3, hi: true);
        _code.AppendLine($"LDA {addrLo}");
        _code.AppendLine("ADD 1");
        _code.AppendLine($"STA {tmp}");
        _code.AppendLine($"LDA {addrHi}");
        _code.AppendLine("ADC 0");
        _code.AppendLine($"STA {tmph}");
        string site = Label("ld");
        _code.AppendLine($"LDA {addrLo}");
        _code.AppendLine($"STA {site}+1");
        _code.AppendLine($"LDA {addrHi}");
        _code.AppendLine($"STA {site}+2");
        _code.AppendLine($"{site}: LDA 0");
        _code.AppendLine($"STA {outLo}");
        if (elemSize == 1)
        {
            _code.AppendLine("LDX 0");
            _code.AppendLine("TXA");
            _code.AppendLine($"STA {outHi}");
            return;
        }

        string siteHi = Label("ld");
        _code.AppendLine($"LDA {tmp}");
        _code.AppendLine($"STA {siteHi}+1");
        _code.AppendLine($"LDA {tmph}");
        _code.AppendLine($"STA {siteHi}+2");
        _code.AppendLine($"{siteHi}: LDA 0");
        _code.AppendLine($"STA {outHi}");
    }

    /// <summary>Pisze pod adres (łatany operand); wartość z pary/rejestru.</summary>
    private void PatchedStore(string addrLo, string addrHi, int elemSize, string valLo, string valHi, int depth)
    {
        string site = Label("st");
        _code.AppendLine($"LDA {addrLo}");
        _code.AppendLine($"STA {site}+1");
        _code.AppendLine($"LDA {addrHi}");
        _code.AppendLine($"STA {site}+2");
        _code.AppendLine($"LDA {valLo}");
        _code.AppendLine($"{site}: STA 0");
        if (elemSize == 1)
        {
            return;
        }

        string tmp = Temp(depth + 3, hi: false);
        string tmph = Temp(depth + 3, hi: true);
        _code.AppendLine($"LDA {addrLo}");
        _code.AppendLine("ADD 1");
        _code.AppendLine($"STA {tmp}");
        _code.AppendLine($"LDA {addrHi}");
        _code.AppendLine("ADC 0");
        _code.AppendLine($"STA {tmph}");
        string siteHi = Label("st");
        _code.AppendLine($"LDA {tmp}");
        _code.AppendLine($"STA {siteHi}+1");
        _code.AppendLine($"LDA {tmph}");
        _code.AppendLine($"STA {siteHi}+2");
        _code.AppendLine($"LDA {valHi}");
        _code.AppendLine($"{siteHi}: STA 0");
    }

    /// <summary>Zapis przez wskaźnik/indeks (wartość, potem adres).</summary>
    private void StorePtr(Ast.AssignTo assignTo, int depth)
    {
        CType elem = assignTo.Target is Ast.Index index ? TypeOfIndex(index) : TypeOfDeref(assignTo.Target);
        int size = ElemSize(elem);
        string vlo;
        string vhi;
        if (size == 1)
        {
            Eval(assignTo.Value, depth);
            vlo = Temp(depth, hi: false);
            _code.AppendLine($"STA {vlo}");
            vhi = vlo;
        }
        else
        {
            EvalInt(assignTo.Value, depth, out vlo, out vhi);
        }

        EvalPtrAddr(assignTo.Target, depth + 1, out string alo, out string ahi);
        if (size == 1)
        {
            PatchedStore(alo, ahi, size, vlo, vhi, depth + 1);
            _code.AppendLine($"LDA {vlo}");
            return;
        }

        PatchedStore(alo, ahi, size, vlo, vhi, depth + 1);
        _code.AppendLine($"LDA {vlo}");
        _code.AppendLine($"LDA {vhi}");
        _code.AppendLine("TAX");
        _code.AppendLine($"LDA {vlo}");
    }

    private CType TypeOfIndex(Ast.Index index) =>
        _types.TryGetValue(index, out CType? type) ? type : CType.UChar;

    private CType TypeOfDeref(Ast.Expr target) =>
        _types.TryGetValue(target, out CType? type) ? type : CType.UChar;

    /// <summary>Arytmetyka wskaźników (ptr+int, int+ptr, ptr-int; skala z elementu).</summary>
    private void EmitPtrArith(Ast.Binary binary, int depth, string lo, string hi)
    {
        bool leftPtr = KindOf(binary.Left) == "ptr";
        Ast.Expr ptrSide = leftPtr ? binary.Left : binary.Right;
        Ast.Expr intSide = leftPtr ? binary.Right : binary.Left;
        if (binary.Op == "-" && !leftPtr)
        {
            throw new CCodegenException("int - ptr is not supported.");
        }

        CType? @base = _types.TryGetValue(ptrSide, out CType? ptrType) ? ptrType.Base : null;
        int scale = @base is not null && @base.Kind == "uchar" ? 1 : 2;
        EvalInt(ptrSide, depth + 1, out string plo, out string phi);
        EvalInt(intSide, depth + 2, out string ilo, out string ihi);
        if (scale == 2)
        {
            DoublePair(ilo, ihi);
            _code.AppendLine("LDX 0");
            _code.AppendLine($"LDA {plo}");
            if (binary.Op == "+")
            {
                _code.AppendLine($"ADD {ilo},X");
            }
            else
            {
                string site = Label("psub");
                _code.AppendLine($"LDA {ilo}");
                _code.AppendLine($"STA {site}+1");
                _code.AppendLine($"LDA {plo}");
                _code.AppendLine($"{site}: SUB 0");
            }

            _code.AppendLine($"STA {lo}");
            _code.AppendLine($"LDA {phi}");
            if (binary.Op == "+")
            {
                _code.AppendLine($"ADC {ihi},X");
            }
            else
            {
                string siteHi = Label("psub");
                _code.AppendLine($"LDA {ihi}");
                _code.AppendLine($"STA {siteHi}+1");
                _code.AppendLine($"LDA {phi}");
                _code.AppendLine($"{siteHi}: SUB 0");
            }

            _code.AppendLine($"STA {hi}");
            return;
        }

        _code.AppendLine("LDX 0");
        _code.AppendLine($"LDA {plo}");
        if (binary.Op == "+")
        {
            _code.AppendLine($"ADD {ilo},X");
            _code.AppendLine($"STA {lo}");
            _code.AppendLine($"LDA {phi}");
            _code.AppendLine($"ADC {ihi},X");
            _code.AppendLine($"STA {hi}");
            return;
        }

        string patchLo = Label("psub");
        string patchHi = Label("psub");
        string noBorrow = Label("nb");
        _code.AppendLine($"LDA {ilo}");
        _code.AppendLine($"STA {patchLo}+1");
        _code.AppendLine($"LDA {plo}");
        _code.AppendLine($"{patchLo}: SUB 0");
        _code.AppendLine($"STA {lo}");
        _code.AppendLine($"BCS {noBorrow}");
        _code.AppendLine($"LDA {ihi}");
        _code.AppendLine("ADD 1");
        _code.AppendLine($"STA {ihi}");
        _code.AppendLine($"{noBorrow}:");
        _code.AppendLine($"LDA {ihi}");
        _code.AppendLine($"STA {patchHi}+1");
        _code.AppendLine($"LDA {phi}");
        _code.AppendLine($"{patchHi}: SUB 0");
        _code.AppendLine($"STA {hi}");
    }
}
