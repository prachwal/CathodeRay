using System.Text;

namespace CathodeRay.C;

/// <summary>Generator kodu: instrukcje (if/pętle/switch/return, inicjalizatory tablic).</summary>
public sealed partial class Codegen
{
    private void EmitStmt(Ast.Stmt stmt)
    {
        try
        {
            EmitStmtCore(stmt);
        }
        catch (CCodegenException e) when (e.Line == 0 && _lines.TryGetValue(stmt, out int line))
        {
            e.Line = line;
            throw;
        }
    }

    private void EmitStmtCore(Ast.Stmt stmt)
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
                    EmitStmt(item);
                }

                break;
            case Ast.Decl decl:
                if (decl.ArrayLength > 0 && decl.Init is not null)
                {
                    EmitArrayInit(decl);
                }
                else if (decl.Init is not null)
                {
                    Store(decl.Name, decl.Init, 0);
                }

                break;
            case Ast.DoWhile doStmt:
                EmitDoWhile(doStmt);
                break;
            case Ast.Switch switchStmt:
                EmitSwitch(switchStmt);
                break;
            case Ast.If ifStmt:
                EmitIf(ifStmt);
                break;
            case Ast.While whileStmt:
                EmitWhile(whileStmt);
                break;
            case Ast.For forStmt:
                EmitFor(forStmt);
                break;
            case Ast.Return ret:
                EmitReturn(ret);
                break;
            case Ast.Break:
                _code.AppendLine($"JMP {_loopLabels.Peek().Break}");
                break;
            case Ast.Continue:
                _code.AppendLine($"JMP {_loopLabels.Peek().Continue}");
                break;
            case Ast.ExprStmt exprStmt:
                Eval(exprStmt.Value, 0);
                break;
            default:
                throw new CCodegenException($"unsupported statement {stmt.GetType().Name}.");
        }
    }

    private void Store(string name, Ast.Expr value, int depth)
    {
        (string cell, CType type) = CellOf(name);
        if (IsWide(type))
        {
            EvalInt(value, depth, out string lo, out string hi);
            _code.AppendLine($"LDA {lo}");
            _code.AppendLine($"STA {cell}");
            _code.AppendLine($"LDA {hi}");
            _code.AppendLine($"STA {Hi(cell)}");
        }
        else
        {
            Eval(value, depth);
            _code.AppendLine($"STA {cell}");
        }
    }

    private void EmitIf(Ast.If ifStmt)
    {
        string els = Label("else");
        string done = Label("endif");
        JumpIfFalse(ifStmt.Cond, els, 0);
        EmitStmt(ifStmt.Then);
        _code.AppendLine($"JMP {done}");
        _code.AppendLine($"{els}:");
        if (ifStmt.Else is not null)
        {
            EmitStmt(ifStmt.Else);
        }

        _code.AppendLine($"{done}:");
    }

    private void EmitWhile(Ast.While whileStmt)
    {
        string loop = Label("while");
        string done = Label("wend");
        _code.AppendLine($"{loop}:");
        JumpIfFalse(whileStmt.Cond, done, 0);
        _loopLabels.Push((done, loop));
        EmitStmt(whileStmt.Body);
        _loopLabels.Pop();
        _code.AppendLine($"JMP {loop}");
        _code.AppendLine($"{done}:");
    }

    private void EmitDoWhile(Ast.DoWhile doStmt)
    {
        string top = Label("do");
        string cont = Label("dcont");
        string done = Label("dend");
        _code.AppendLine($"{top}:");
        _loopLabels.Push((done, cont));
        EmitStmt(doStmt.Body);
        _loopLabels.Pop();
        _code.AppendLine($"{cont}:");
        JumpIfTrue(doStmt.Cond, top, 0);
        _code.AppendLine($"{done}:");
    }

    /// <summary>switch: wartość do własnej pary komórek (chronionej ramką), łańcuch porównań
    /// ze stałymi, potem ciała po kolei (przechodzą dalej jak w C).</summary>
    private void EmitSwitch(Ast.Switch stmt)
    {
        string cell = $"{_prefix}__sw{_switches++}";
        DataCell(cell, CType.UChar);
        DataCell($"{cell}_h", CType.UChar);
        _switchCells.Add(cell);
        _switchCells.Add($"{cell}_h");
        EvalInt(stmt.Value, 0, out string lo, out string hi);
        _code.AppendLine($"LDA {lo}");
        _code.AppendLine($"STA {cell}");
        _code.AppendLine($"LDA {hi}");
        _code.AppendLine($"STA {cell}_h");
        string done = Label("swend");
        var labels = new List<string>();
        string? defaultLabel = null;
        foreach (Ast.SwitchCase item in stmt.Cases)
        {
            string label = Label("case");
            labels.Add(label);
            if (item.Value is not Ast.Number number)
            {
                defaultLabel = label;
                continue;
            }

            TryNumber(number.Text, out int value);
            string next = Label("swnext");
            _code.AppendLine($"LDA {cell}");
            _code.AppendLine($"CPA {value & 0xFF}");
            _code.AppendLine($"BNE {next}");
            _code.AppendLine($"LDA {cell}_h");
            _code.AppendLine($"CPA {(value >> 8) & 0xFF}");
            _code.AppendLine($"BNE {next}");
            _code.AppendLine($"JMP {label}");
            _code.AppendLine($"{next}:");
        }

        _code.AppendLine($"JMP {defaultLabel ?? done}");
        _loopLabels.Push((done, _loopLabels.Count > 0 ? _loopLabels.Peek().Continue : done));
        for (int i = 0; i < stmt.Cases.Count; i++)
        {
            _code.AppendLine($"{labels[i]}:");
            foreach (Ast.Stmt body in stmt.Cases[i].Body)
            {
                EmitStmt(body);
            }
        }

        _loopLabels.Pop();
        _code.AppendLine($"{done}:");
    }

    /// <summary>Lokalna tablica z <c>{…}</c>/napisem: elementy po kolei, reszta zerowana pętlą.</summary>
    private void EmitArrayInit(Ast.Decl decl)
    {
        (string cell, CType type) = CellOf(decl.Name);
        CType elem = type.Base!;
        int size = elem.Size;
        IReadOnlyList<Ast.Expr> items = decl.Init is Ast.InitList list
            ? list.Items
            : [.. ((Ast.Str)decl.Init!).Value.Append('\0').Select(static ch => (Ast.Expr)new Ast.Number(((int)ch).ToString(System.Globalization.CultureInfo.InvariantCulture)))];
        for (int i = 0; i < items.Count; i++)
        {
            string at = i * size == 0 ? cell : $"{cell}+{i * size}";
            if (size == 1)
            {
                Eval(items[i], 0);
                _code.AppendLine($"STA {at}");
                continue;
            }

            EvalInt(items[i], 0, out string lo, out string hi);
            _code.AppendLine($"LDA {lo}");
            _code.AppendLine($"STA {at}");
            _code.AppendLine($"LDA {hi}");
            _code.AppendLine($"STA {cell}+{(i * size) + 1}");
        }

        int start = items.Count * size;
        if (start < type.Size)
        {
            string loop = Label("zero");
            _code.AppendLine("LDI 0");
            _code.AppendLine($"LDX {start}");
            _code.AppendLine($"{loop}: STA {cell},X");
            _code.AppendLine("INX");
            _code.AppendLine($"CPX {type.Size & 0xFF}");
            _code.AppendLine($"BNE {loop}");
        }
    }

    private void EmitFor(Ast.For forStmt)
    {
        if (forStmt.Init is not null)
        {
            EmitStmt(forStmt.Init);
        }

        string loop = Label("for");
        string done = Label("fend");
        _code.AppendLine($"{loop}:");
        if (forStmt.Cond is not null)
        {
            JumpIfFalse(forStmt.Cond, done, 0);
        }

        string step = Label("fstep");
        _loopLabels.Push((done, step));
        EmitStmt(forStmt.Body);
        _loopLabels.Pop();
        _code.AppendLine($"{step}:");
        if (forStmt.Step is not null)
        {
            Eval(forStmt.Step, 0);
        }

        _code.AppendLine($"JMP {loop}");
        _code.AppendLine($"{done}:");
    }

    private void EmitReturn(Ast.Return ret)
    {
        if (ret.Value is not null)
        {
            if (!_functions.TryGetValue(_prefix, out CheckedFunction? current) || current.Def.ReturnType == "void")
            {
                throw new CCodegenException("return with a value needs a function.");
            }

            if (current.Def.ReturnType == "int")
            {
                EvalInt(ret.Value, 0, out string lo, out string hi);
                _code.AppendLine($"LDA {lo}");
                _code.AppendLine($"LDA {hi}");
                _code.AppendLine("TAX");
                _code.AppendLine($"LDA {lo}");
            }
            else
            {
                Eval(ret.Value, 0);
            }
        }

        _code.AppendLine($"JMP {_prefix}__ret");
    }
}
