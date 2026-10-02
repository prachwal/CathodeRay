using System.Globalization;
using System.Text;

namespace CathodeRay.C;

internal sealed partial class ByteSelector
{
    private void EmitFunction(Ir.Function function)
    {
        int mark = _isa.Mark;
        _isa.Cells.BeginFunction(function);
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
        string? park = _isa.AbiV2 && function.Saved.Count > 0 && HasRegParams(function) ? (_isa as IRegArgs)?.EntryParkCell : null;
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
                    ((IRegArgs)_isa).FetchArg(reg);
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
        string? footerPark = keepResult && function.Saved.Count > 0 ? (_isa as IRegArgs)?.EntryParkCell : null;
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
}
