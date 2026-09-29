namespace CathodeRay.C;

/// <summary>Lowering: instrukcje.</summary>
internal sealed partial class Lowering
{
    private void LowerStmt(Ast.Stmt stmt)
    {
        try
        {
            LowerStmtCore(stmt);
        }
        catch (CCodegenException e) when (e.Line == 0 && _lines.TryGetValue(stmt, out int line))
        {
            e.Line = line;
            throw;
        }
    }

    private void LowerStmtCore(Ast.Stmt stmt)
    {
        if (stmt is not Ast.Block and not Ast.Nop)
        {
            Comment(stmt);
        }

        switch (stmt)
        {
            case Ast.Nop:
                break;
            case Ast.Block block:
                foreach (Ast.Stmt item in block.Items)
                {
                    LowerStmt(item);
                }

                break;
            case Ast.Decl decl when decl.Flags.HasFlag(DeclFlags.Static):
                break;
            case Ast.Decl decl:
                if ((decl.Init is Ast.InitList or Ast.Str) && _cells[decl.Name].Type.Kind is "array" or "struct")
                {
                    LowerAggregateInit(decl);
                }
                else if (decl.Init is not null)
                {
                    AssignVariable(decl.Name, decl.Init, 0);
                }

                break;
            case Ast.Label label:
                Emit(new Ir.Label(UserLabel(label.Name)));
                break;
            case Ast.Goto jump:
                Emit(new Ir.Jmp(UserLabel(jump.Name)));
                break;
            case Ast.DoWhile doStmt:
                LowerDoWhile(doStmt);
                break;
            case Ast.Switch switchStmt:
                LowerSwitch(switchStmt);
                break;
            case Ast.If ifStmt:
                LowerIf(ifStmt);
                break;
            case Ast.While whileStmt:
                LowerWhile(whileStmt);
                break;
            case Ast.For forStmt:
                LowerFor(forStmt);
                break;
            case Ast.Return ret:
                LowerReturn(ret);
                break;
            case Ast.Break:
                Emit(new Ir.Jmp(_loopLabels.Peek().Break));
                break;
            case Ast.Continue:
                Emit(new Ir.Jmp(_loopLabels.Peek().Continue));
                break;
            case Ast.ExprStmt exprStmt:
                Effect(exprStmt.Value);
                break;
            default:
                throw new CCodegenException($"unsupported statement {stmt.GetType().Name}.");
        }
    }

    private string UserLabel(string name) => $"{_prefix}__L_{name}";

    /// <summary>Wyrażenie dla skutków ubocznych (wynik wywołania jest odrzucany).</summary>
    private void Effect(Ast.Expr expr)
    {
        if (expr is Ast.Call or Ast.CallExpr)
        {
            LowerCall(expr, 0, null);
            return;
        }

        Value(expr, 0);
    }

    private void LowerIf(Ast.If ifStmt)
    {
        string els = Label("else");
        string done = Label("endif");
        Branch(ifStmt.Cond, els, whenTrue: false, 0);
        LowerStmt(ifStmt.Then);
        Emit(new Ir.Jmp(done));
        Emit(new Ir.Label(els));
        if (ifStmt.Else is not null)
        {
            LowerStmt(ifStmt.Else);
        }

        Emit(new Ir.Label(done));
    }

    private void LowerWhile(Ast.While whileStmt)
    {
        string loop = Label("while");
        string done = Label("wend");
        Emit(new Ir.Label(loop));
        Branch(whileStmt.Cond, done, whenTrue: false, 0);
        _loopLabels.Push((done, loop));
        LowerStmt(whileStmt.Body);
        _loopLabels.Pop();
        Emit(new Ir.Jmp(loop));
        Emit(new Ir.Label(done));
    }

    private void LowerDoWhile(Ast.DoWhile doStmt)
    {
        string top = Label("do");
        string cont = Label("dcont");
        string done = Label("dend");
        Emit(new Ir.Label(top));
        _loopLabels.Push((done, cont));
        LowerStmt(doStmt.Body);
        _loopLabels.Pop();
        Emit(new Ir.Label(cont));
        Branch(doStmt.Cond, top, whenTrue: true, 0);
        Emit(new Ir.Label(done));
    }

    private void LowerFor(Ast.For forStmt)
    {
        if (forStmt.Init is not null)
        {
            LowerStmt(forStmt.Init);
        }

        string loop = Label("for");
        string done = Label("fend");
        string step = Label("fstep");
        Emit(new Ir.Label(loop));
        if (forStmt.Cond is not null)
        {
            Branch(forStmt.Cond, done, whenTrue: false, 0);
        }

        _loopLabels.Push((done, step));
        LowerStmt(forStmt.Body);
        _loopLabels.Pop();
        Emit(new Ir.Label(step));
        if (forStmt.Step is not null)
        {
            Effect(forStmt.Step);
        }

        Emit(new Ir.Jmp(loop));
        Emit(new Ir.Label(done));
    }

    private void LowerReturn(Ast.Return ret)
    {
        Ast.Function def = _current!.Def;
        int retW = def.ReturnType == "void" && def.ReturnStars == 0 ? 0 : def.ReturnType == "uchar" && def.ReturnStars == 0 ? 1 : 2;
        if (ret.Value is null)
        {
            Emit(new Ir.Ret(null, retW));
            return;
        }

        if (retW == 0)
        {
            throw new CCodegenException("return with a value needs a function.");
        }

        Emit(new Ir.Ret(Value(ret.Value, 0), retW));
    }

    /// <summary>switch: wartość do własnej komórki (chronionej ramką), łańcuch porównań ze stałymi,
    /// potem ciała po kolei (przechodzą dalej jak w C).</summary>
    private void LowerSwitch(Ast.Switch stmt)
    {
        string sym = $"{_prefix}__sw@{_switches++}";
        AddBss(sym, 2);
        _extraOwned.Add(new Ir.Owned(sym, 2, false));
        var cell = new Ir.Cell(sym, 2);
        Emit(new Ir.Mov(cell, Value(stmt.Value, 0)));
        string done = Label("swend");
        var labels = new List<string>();
        string? defaultLabel = null;
        foreach (Ast.SwitchCase item in stmt.Cases)
        {
            string label = Label("case");
            labels.Add(label);
            if (item.Value is null)
            {
                defaultLabel = label;
                continue;
            }

            TryConstValue(item.Value, out int value);
            Emit(new Ir.BrCmp(Ir.Cond.Eq, cell, new Ir.Imm(value & 0xFFFF, 2), label));
        }

        Emit(new Ir.Jmp(defaultLabel ?? done));
        _loopLabels.Push((done, _loopLabels.Count > 0 ? _loopLabels.Peek().Continue : done));
        for (int i = 0; i < stmt.Cases.Count; i++)
        {
            Emit(new Ir.Label(labels[i]));
            foreach (Ast.Stmt body in stmt.Cases[i].Body)
            {
                LowerStmt(body);
            }
        }

        _loopLabels.Pop();
        Emit(new Ir.Label(done));
    }

    /// <summary>Lokalna tablica/struktura z <c>{…}</c> lub napisem: zerowanie (gdy inicjalizator nie pokrywa całości),
    /// potem zapisy skalarów w miejscach (zagnieżdżone pola i elementy).</summary>
    private void LowerAggregateInit(Ast.Decl decl)
    {
        VarCell cell = _cells[decl.Name];
        var entries = new List<(int Offset, CType Type, Ast.Expr Value)>();
        CollectInit(cell.Type, decl.Init!, 0, entries);
        var target = new Ir.AddrOf(cell.Sym, 0);
        if (entries.Sum(static e => Width(e.Type)) < cell.Type.Size)
        {
            Emit(new Ir.Fill(target, 0, cell.Type.Size));
        }

        foreach ((int offset, CType entryType, Ast.Expr value) in entries)
        {
            Emit(new Ir.Store(target, offset, Value(value, 0), Width(entryType)));
        }
    }
}
