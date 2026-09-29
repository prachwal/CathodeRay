namespace CathodeRay.C;

/// <summary>Lowering: wywołania funkcji (po nazwie, przez zmienną i przez wyrażenie) oraz kontrola stosu.</summary>
internal sealed partial class Lowering
{
    /// <summary>Największa struktura zwracana przez wartość (rozmiar wspólnego bufora <c>cc_retbuf</c> w crt0).</summary>
    public const int MaxReturnedStruct = 64;

    private const string ReturnBuffer = "cc_retbuf";

    private static int RetWidth(CType type) => type.Kind == "void" ? 0 : Width(type);

    /// <summary>Wołanie: argumenty od ostatniego (każdy w swojej tymczasowej głębiej niż poprzedni, żeby zagnieżdżone
    /// wywołania nie nadpisały wyników), potem adres funkcji (wołanie pośrednie), potem <see cref="Ir.Call"/>.</summary>
    private Ir.Op LowerCall(Ast.Expr expr, int depth, Ir.Cell? into)
    {
        IReadOnlyList<Ast.Expr> args;
        string? direct = null;
        Ast.Expr? calleeExpr = null;
        List<int> widths;
        IReadOnlyList<CType> paramTypes;
        switch (expr)
        {
            case Ast.Call call when _cells.TryGetValue(call.Name, out VarCell? variable) && variable.Type.Kind == "fptr":
                args = call.Args;
                calleeExpr = new Ast.Var(call.Name);
                _types[calleeExpr] = variable.Type;
                paramTypes = variable.Type.Sig!.Params;
                widths = [.. paramTypes.Select(static t => Width(t))];
                break;
            case Ast.Call call:
                if (!_functions.TryGetValue(call.Name, out CheckedFunction? target))
                {
                    throw new CCodegenException($"undefined function '{call.Name}'.");
                }

                args = call.Args;
                direct = call.Name;
                paramTypes = [.. target.Params.Select(static p => p.Type)];
                widths = [.. paramTypes.Select(static t => Width(t))];
                while (target.Def.IsVariadic && widths.Count < args.Count)
                {
                    widths.Add(2);
                }

                break;
            case Ast.CallExpr callExpr:
                args = callExpr.Args;
                calleeExpr = callExpr.Callee;
                paramTypes = TypeOf(callExpr.Callee).Sig!.Params;
                widths = [.. paramTypes.Select(static t => Width(t))];
                break;
            default:
                throw new CCodegenException($"'{expr.GetType().Name}' is not a call.");
        }

        if (args.Count > TypeChecker.MaxArgs)
        {
            throw new CCodegenException($"call takes at most {TypeChecker.MaxArgs} arguments.");
        }

        int count = args.Count;
        var values = new Ir.Op[count];
        for (int i = count - 1; i >= 0; i--)
        {
            int at = depth + 1 + (count - 1 - i);
            if (i < paramTypes.Count && paramTypes[i].Kind == "struct")
            {
                (Ir.Op pointer, int offset) = LValueAddr(args[i], at);
                values[i] = AddressValue(pointer, offset, at);
            }
            else
            {
                values[i] = Value(args[i], at);
            }
        }

        Ir.Cell? indirect = null;
        if (calleeExpr is not null)
        {
            Ir.Op callee = Value(calleeExpr, depth + 1 + count);
            switch (callee)
            {
                case Ir.AddrOf { Off: 0 } function:
                    direct = function.Sym;
                    break;
                case Ir.Cell cell:
                    indirect = cell;
                    break;
                default:
                    indirect = Temp(depth + 1 + count, 2);
                    Emit(new Ir.Mov(indirect, callee));
                    break;
            }
        }

        CType returned = TypeOf(expr);
        Ir.AddrOf? aggregate = null;
        if (returned.Kind == "struct")
        {
            aggregate = new Ir.AddrOf(NewAggregate(returned.Size), 0);
            _usesReturnBuffer = true;
        }

        int retW = expr is Ast.Call or Ast.CallExpr && returned.Kind != "struct" ? RetWidth(returned) : 0;
        Ir.Cell? result = retW == 0 ? null : into is not null && into.W == retW ? into : Temp(depth, retW);
        Emit(new Ir.Call(direct, indirect, values, widths, result));
        if (aggregate is not null)
        {
            // wynik struktury wraca we wspólnym buforze, który wołający kopiuje od razu do własnej tymczasowej
            // (bufor nie jest w ramce, więc odtworzenie komórek przez wołanego go nie zniszczy)
            Emit(new Ir.CopyBlock(aggregate, new Ir.AddrOf(ReturnBuffer, 0), returned.Size));
        }

        if (direct is not null)
        {
            if (!_calls.TryGetValue(_prefix, out HashSet<string>? callees))
            {
                _calls[_prefix] = callees = new HashSet<string>(StringComparer.Ordinal);
            }

            callees.Add(direct);
        }

        return aggregate is not null ? aggregate : result is null ? new Ir.Imm(0, 1) : result;
    }

    /// <summary>Stos sprzętowy ma ograniczony rozmiar: błąd, gdy najgłębsza nierekurencyjna ścieżka wołań
    /// (ramka = zapisane komórki + adres powrotu) się nie mieści; rekurencja dostaje ostrzeżenie
    /// z szacunkiem głębokości.</summary>
    private void CheckStack(List<string> warnings, int? limit)
    {
        int page = limit ?? int.MaxValue;
        var reported = new HashSet<string>(StringComparer.Ordinal);
        int Depth(string name, List<string> path)
        {
            int frame = _frames.TryGetValue(name, out int f) ? f : 2;
            int deepest = 0;
            path.Add(name);
            foreach (string callee in _calls.GetValueOrDefault(name) ?? [])
            {
                int at = path.IndexOf(callee);
                if (at >= 0)
                {
                    int cycle = path.Skip(at).Sum(n => _frames.GetValueOrDefault(n, 2));
                    if (reported.Add(string.Join(">", path.Skip(at).Order(StringComparer.Ordinal))))
                    {
                        warnings.Add($"'{callee}' is recursive: {cycle} B per cycle, at most ~{Math.Min(page, 65536) / cycle} nested calls fit the stack page.");
                    }

                    continue;
                }

                deepest = Math.Max(deepest, Depth(callee, path));
            }

            path.RemoveAt(path.Count - 1);
            return frame + deepest;
        }

        foreach (string root in _frames.Keys)
        {
            int total = Depth(root, []);
            if (total > page)
            {
                throw new CCodegenException($"call chain from '{root}' needs {total} B of stack (page is {page} B).");
            }
        }
    }
}
