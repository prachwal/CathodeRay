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
            case Ast.Decl decl when decl.Flags.HasFlag(DeclFlags.Static):
                break;
            case Ast.Decl decl:
                if ((decl.Init is Ast.InitList or Ast.Str) && CellOf(decl.Name).Type.Kind is "array" or "struct")
                {
                    EmitAggregateInit(decl);
                }
                else if (decl.Init is not null)
                {
                    Store(decl.Name, decl.Init, 0);
                }

                break;
            case Ast.Label label:
                _code.AppendLine($"{_prefix}__L_{label.Name}:");
                break;
            case Ast.Goto jump:
                _code.AppendLine($"JMP {_prefix}__L_{jump.Name}");
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
        if (type.Kind == "struct")
        {
            string dstLo = Temp(depth, hi: false);
            string dstHi = Temp(depth, hi: true);
            EmitAddressOf(cell, dstLo, dstHi);
            EmitStructCopy(depth, dstLo, dstHi, value, type.Size);
            return;
        }

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
        _extraCells.Add(cell);
        _extraCells.Add($"{cell}_h");
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
            if (item.Value is null)
            {
                defaultLabel = label;
                continue;
            }

            TryConstValue(item.Value, out int value);
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

    /// <summary>Lokalna tablica/struktura z <c>{…}</c> lub napisem: zerowanie pętlą (gdy inicjalizator
    /// nie pokrywa całości), potem zapisy skalarów w miejscach (zagnieżdżone pola i elementy).</summary>
    private void EmitAggregateInit(Ast.Decl decl)
    {
        (string cell, CType type) = CellOf(decl.Name);
        var entries = new List<(int Offset, CType Type, Ast.Expr Value)>();
        CollectInit(type, decl.Init!, 0, entries);
        if (entries.Sum(static e => e.Type.Size) < type.Size)
        {
            _code.AppendLine("LDI 0");
            for (int page = 0; page * 256 < type.Size; page++)
            {
                int length = Math.Min(256, type.Size - (page * 256));
                string loop = Label("zero");
                string at = page == 0 ? cell : $"{cell}+{page * 256}";
                _code.AppendLine("LDX 0");
                _code.AppendLine($"{loop}: STA {at},X");
                _code.AppendLine("INX");
                _code.AppendLine($"CPX {length & 0xFF}");
                _code.AppendLine($"BNE {loop}");
            }
        }

        foreach ((int offset, CType entryType, Ast.Expr value) in entries)
        {
            string at = offset == 0 ? cell : $"{cell}+{offset}";
            if (entryType.Size == 1)
            {
                Eval(value, 0);
                _code.AppendLine($"STA {at}");
                continue;
            }

            EvalInt(value, 0, out string lo, out string hi);
            _code.AppendLine($"LDA {lo}");
            _code.AppendLine($"STA {at}");
            _code.AppendLine($"LDA {hi}");
            _code.AppendLine($"STA {cell}+{offset + 1}");
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
            if (!_functions.TryGetValue(_prefix, out CheckedFunction? current) || (current.Def.ReturnType == "void" && current.Def.ReturnStars == 0))
            {
                throw new CCodegenException("return with a value needs a function.");
            }

            if (current.Def.ReturnType is "int" or "uint" || current.Def.ReturnStars > 0)
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
