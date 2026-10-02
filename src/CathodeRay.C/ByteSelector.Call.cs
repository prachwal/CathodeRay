using System.Globalization;
using System.Text;

namespace CathodeRay.C;

internal sealed partial class ByteSelector
{
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
                ((IRegArgs)_isa).StoreArg(CallArgCell(call, i, part, call.ParamWidths[i]));
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
            ((IRegArgs)_isa).SetupFp(_isa.Sym(indirect.Sym));
            _acc.Clear(); // SetupFp ładuje A, a EmitCallArgs zaczyna od LoadA
        }

        EmitCallArgs(call);

        // rejestry komórek żywych za wołaniem: na stos po argumentach, ze stosu przed zapisem wyniku (wynik może leżeć w tej parze)
        IReadOnlyList<string> saved = _isa.Cells.SavedAround(call);
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
}
