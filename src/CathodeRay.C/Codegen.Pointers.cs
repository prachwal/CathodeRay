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
            ScaleIndex(ilo, ihi, TypeOfIndex(index).Size);

            _code.AppendLine("LDX 0");
            _code.AppendLine($"LDA {blo}");
            _code.AppendLine($"ADD {ilo},X");
            _code.AppendLine($"STA {lo}");
            _code.AppendLine($"LDA {bhi}");
            _code.AppendLine($"ADC {ihi},X");
            _code.AppendLine($"STA {hi}");
            return;
        }

        if (target is Ast.Member member)
        {
            StructField field = FieldOf(member);
            string blo;
            string bhi;
            if (member.Arrow)
            {
                EvalInt(member.Base, depth + 1, out blo, out bhi);
            }
            else
            {
                EvalLvalueAddr(member.Base, depth + 1, out blo, out bhi);
            }

            if (field.Offset == 0)
            {
                _code.AppendLine($"LDA {blo}");
                _code.AppendLine($"STA {lo}");
                _code.AppendLine($"LDA {bhi}");
                _code.AppendLine($"STA {hi}");
                return;
            }

            _code.AppendLine($"LDA {blo}");
            _code.AppendLine($"ADD {field.Offset & 0xFF}");
            _code.AppendLine($"STA {lo}");
            _code.AppendLine($"LDA {bhi}");
            _code.AppendLine($"ADC {(field.Offset >> 8) & 0xFF}");
            _code.AppendLine($"STA {hi}");
            return;
        }

        throw new CCodegenException($"pointer target '{target.GetType().Name}' not supported.");
    }

    /// <summary>Adres lwartości (zmienna, <c>*p</c>, <c>a[i]</c>, <c>s.f</c>) w parze Temp(depth).</summary>
    private void EvalLvalueAddr(Ast.Expr expr, int depth, out string lo, out string hi)
    {
        if (expr is Ast.Var variable)
        {
            lo = Temp(depth, hi: false);
            hi = Temp(depth, hi: true);
            EmitAddressOf(CellOf(variable.Name).Lo, lo, hi);
            return;
        }

        if (expr is Ast.Deref or Ast.Index or Ast.Member)
        {
            EvalPtrAddr(expr, depth, out lo, out hi);
            return;
        }

        throw new CCodegenException($"'{expr.GetType().Name}' is not an lvalue.");
    }

    private StructField FieldOf(Ast.Member member)
    {
        CType baseType = _types[member.Base];
        StructInfo info = member.Arrow ? baseType.Base!.Info! : baseType.Info!;
        return info.Find(member.Name)!;
    }

    /// <summary>Mnoży indeks (para lo,hi) w miejscu przez rozmiar elementu: 1 nic, 2 podwojenie,
    /// reszta przez <c>cc_mul16</c>.</summary>
    private void ScaleIndex(string ilo, string ihi, int size)
    {
        if (size == 1)
        {
            return;
        }

        if (size == 2)
        {
            DoublePair(ilo, ihi);
            return;
        }

        _needMul16 = true;
        _code.AppendLine($"LDA {ilo}");
        _code.AppendLine("STA cc_w_a");
        _code.AppendLine($"LDA {ihi}");
        _code.AppendLine("STA cc_w_a_h");
        _code.AppendLine($"LDI {size & 0xFF}");
        _code.AppendLine("STA cc_w_b");
        _code.AppendLine($"LDI {(size >> 8) & 0xFF}");
        _code.AppendLine("STA cc_w_b_h");
        _code.AppendLine("CALL cc_mul16");
        _code.AppendLine($"STA {ilo}");
        _code.AppendLine("TXA");
        _code.AppendLine($"STA {ihi}");
    }

    /// <summary>Kopiuje strukturę (adres celu w Temp(depth), źródło liczone z lwartości) bajt po bajcie;
    /// licznik 16-bit, więc bez limitu rozmiaru.</summary>
    private void EmitStructCopy(int depth, string dstLo, string dstHi, Ast.Expr source, int size)
    {
        if (size == 0)
        {
            return;
        }

        EvalLvalueAddr(source, depth + 1, out string srcLo, out string srcHi);
        string countLo = Temp(depth + 2, hi: false);
        string countHi = Temp(depth + 2, hi: true);
        string bytes = Temp(depth + 3, hi: false);
        string loop = Label("copy");
        string load = Label("cld");
        string store = Label("cst");
        string noBorrow = Label("cnb");
        _code.AppendLine($"LDI {size & 0xFF}");
        _code.AppendLine($"STA {countLo}");
        _code.AppendLine($"LDI {(size >> 8) & 0xFF}");
        _code.AppendLine($"STA {countHi}");
        _code.AppendLine($"{loop}:");
        _code.AppendLine($"LDA {srcLo}");
        _code.AppendLine($"STA {load}+1");
        _code.AppendLine($"LDA {srcHi}");
        _code.AppendLine($"STA {load}+2");
        _code.AppendLine($"{load}: LDA 0");
        _code.AppendLine($"STA {bytes}");
        _code.AppendLine($"LDA {dstLo}");
        _code.AppendLine($"STA {store}+1");
        _code.AppendLine($"LDA {dstHi}");
        _code.AppendLine($"STA {store}+2");
        _code.AppendLine($"LDA {bytes}");
        _code.AppendLine($"{store}: STA 0");
        foreach ((string pl, string ph) in new[] { (srcLo, srcHi), (dstLo, dstHi) })
        {
            _code.AppendLine($"LDA {pl}");
            _code.AppendLine("ADD 1");
            _code.AppendLine($"STA {pl}");
            _code.AppendLine($"LDA {ph}");
            _code.AppendLine("ADC 0");
            _code.AppendLine($"STA {ph}");
        }

        _code.AppendLine($"LDA {countLo}");
        _code.AppendLine("SUB 1");
        _code.AppendLine($"STA {countLo}");
        _code.AppendLine($"BCS {noBorrow}");
        _code.AppendLine($"LDA {countHi}");
        _code.AppendLine("SUB 1");
        _code.AppendLine($"STA {countHi}");
        _code.AppendLine($"{noBorrow}: LDX 0");
        _code.AppendLine($"LDA {countLo}");
        _code.AppendLine($"ORA {countHi},X");
        _code.AppendLine($"BNE {loop}");
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
        if (elem.Kind == "struct")
        {
            EvalPtrAddr(assignTo.Target, depth, out string dlo, out string dhi);
            EmitStructCopy(depth, dlo, dhi, assignTo.Value, elem.Size);
            return;
        }

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

    /// <summary>Różnica wskaźników w elementach: (p - q) / rozmiar (dzielenie ze znakiem).</summary>
    private void EmitPtrDiff(Ast.Binary binary, int depth, string lo, string hi)
    {
        EvalInt(binary.Left, depth + 1, out string leftLo, out string leftHi);
        EvalInt(binary.Right, depth + 2, out string rightLo, out string rightHi);
        _code.AppendLine($"LDA {leftLo}");
        _code.AppendLine($"STA {lo}");
        _code.AppendLine($"LDA {leftHi}");
        _code.AppendLine($"STA {hi}");
        EmitSub16(lo, hi, rightLo, rightHi);
        int size = _types.TryGetValue(binary.Left, out CType? type) && type.Base is not null ? type.Base.Size : 1;
        if (size <= 1)
        {
            return;
        }

        _needDiv16 = true;
        _code.AppendLine($"LDA {lo}");
        _code.AppendLine("STA cc_w_a");
        _code.AppendLine($"LDA {hi}");
        _code.AppendLine("STA cc_w_a_h");
        _code.AppendLine($"LDI {size & 0xFF}");
        _code.AppendLine("STA cc_w_b");
        _code.AppendLine($"LDI {(size >> 8) & 0xFF}");
        _code.AppendLine("STA cc_w_b_h");
        _code.AppendLine("CALL cc_sdiv16");
        _code.AppendLine($"STA {lo}");
        _code.AppendLine("TXA");
        _code.AppendLine($"STA {hi}");
    }

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
        int scale = @base?.Size ?? 1;
        EvalInt(ptrSide, depth + 1, out string plo, out string phi);
        EvalInt(intSide, depth + 2, out string ilo, out string ihi);
        ScaleIndex(ilo, ihi, scale);
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
