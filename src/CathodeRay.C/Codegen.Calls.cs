using System.Text;

namespace CathodeRay.C;

/// <summary>Generator kodu: wywołania funkcji i kontrola stosu.</summary>
public sealed partial class Codegen
{
    private void EmitCall(Ast.Call call, int depth)
    {
        if (!_functions.TryGetValue(call.Name, out CheckedFunction? target))
        {
            throw new CCodegenException($"undefined function '{call.Name}'.");
        }

        if (call.Args.Count > TypeChecker.MaxArgs)
        {
            throw new CCodegenException($"'{call.Name}' takes at most {TypeChecker.MaxArgs} arguments.");
        }

        // Argumenty od ostatniego, każdy w swojej parze temp (głębiej niż poprzedni),
        // żeby zagnieżdżone wywołania nie nadpisały wyników; kopiowanie do komórek
        // wejściowych dopiero tuż przed CALL.
        int count = call.Args.Count;
        var places = new (string Lo, string Hi)[count];
        for (int i = count - 1; i >= 0; i--)
        {
            int at = depth + 1 + (count - 1 - i);
            if (IsWide(target.Params[i].Type))
            {
                EvalInt(call.Args[i], at, out string lo, out string hi);
                places[i] = (lo, hi);
            }
            else
            {
                Eval(call.Args[i], at);
                string spill = Temp(at, hi: false);
                _code.AppendLine($"STA {spill}");
                places[i] = (spill, spill);
            }
        }

        for (int i = 1; i < count; i++)
        {
            (string cellLo, string? cellHi) = ArgCells(target.Params, i);
            if (cellLo == "cc_arg1_h")
            {
                continue;
            }

            _code.AppendLine($"LDA {places[i].Lo}");
            _code.AppendLine($"STA {cellLo}");
            if (cellHi is not null)
            {
                _code.AppendLine($"LDA {places[i].Hi}");
                _code.AppendLine($"STA {cellHi}");
            }
        }

        if (count >= 1 && IsWide(target.Params[0].Type))
        {
            _code.AppendLine($"LDA {places[0].Hi}");
            _code.AppendLine("TAX");
        }
        else if (count >= 2 && ArgCells(target.Params, 1).Lo == "cc_arg1_h")
        {
            _code.AppendLine($"LDA {places[1].Lo}");
            _code.AppendLine("TAX");
        }

        if (count >= 1)
        {
            _code.AppendLine($"LDA {places[0].Lo}");
        }

        _code.AppendLine($"CALL {call.Name}");
        if (!_calls.TryGetValue(_prefix, out HashSet<string>? callees))
        {
            _calls[_prefix] = callees = new HashSet<string>(StringComparer.Ordinal);
        }

        callees.Add(call.Name);
    }

    /// <summary>Stos sprzętowy to jedna strona (256 B): błąd, gdy najgłębsza nierekurencyjna
    /// ścieżka wołań (ramka = PUSHe + adres powrotu) się nie mieści; rekurencja dostaje ostrzeżenie
    /// z szacunkiem głębokości.</summary>
    private void CheckStack(List<string> warnings)
    {
        const int Page = 256;
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
                        warnings.Add($"'{callee}' is recursive: {cycle} B per cycle, at most ~{Page / cycle} nested calls fit the stack page.");
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
            if (total > Page)
            {
                throw new CCodegenException($"call chain from '{root}' needs {total} B of stack (page is {Page} B).");
            }
        }
    }
}
