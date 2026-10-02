using System.Globalization;
using System.Text;

namespace CathodeRay.C;

internal sealed partial class ByteSelector
{
    /// <summary>Pętla kopiująca bajty (<c>while (n) { *d = *s; d++; s++; n--; }</c> albo z <c>n &gt; 0</c>)
    /// jako blok z prymitywu <see cref="ICopyLoop.TryCopyLoop"/> (dziś tylko Z80 z <c>ldir</c>; reszta nie ma
    /// <c>false</c> i pętla idzie starą drogą). Warunki: elementy W1, wskaźniki i licznik W2, ciało dokładnie
    /// [Load, Store, d+1, s+1, n-1], brak innych odwołań do d/s/n/t w funkcji (writeback zbędny) i brak obcych
    /// skoków do etykiet pętli. Licznik <c>Eq</c> ze znakiem ujemnym zawiesiłby oryginał (nieskończona pętla),
    /// więc zamiana jest nieobserwowalna w każdym kończącym się programie.</summary>
    /// <param name="function">Emitowana funkcja.</param>
    /// <param name="index">Pozycja kandydata (etykieta pętli lub wcześniejszy komentarz).</param>
    /// <param name="next">Pozycja za etykietą końca (gdy dopasowano).</param>
    /// <returns>Czy wyemitowano blok kopiujący.</returns>
    private bool TryEmitCopyLoop(Ir.Function function, int index, out int next)
    {
        next = index;
        int i = index;
        var comments = new List<Ir.Src>();
        while (i < function.Body.Count && function.Body[i] is Ir.Src lead)
        {
            comments.Add(lead);
            i++;
        }

        if (i >= function.Body.Count || function.Body[i] is not Ir.Label top)
        {
            return false;
        }

        i++;
        string? end = null;
        Ir.Cell? n = null;
        if (!SkipComments(function, comments, ref i) || function.Body[i] is not Ir.BrCmp branch
            || branch.C is not (Ir.Cond.Eq or Ir.Cond.Le) || branch.A is not Ir.Cell count || count.W != 2
            || branch.B is not Ir.Imm { Value: 0 })
        {
            return false;
        }

        end = branch.Target;
        n = count;
        i++;
        if (!SkipComments(function, comments, ref i) || function.Body[i] is not Ir.Load load
            || load.Dst is not { W: 1 } t || load.Ptr is not Ir.Cell { W: 2 } s || load.Off != 0
            || load.Bytes != 1 || load.Volatile)
        {
            return false;
        }

        i++;
        if (!SkipComments(function, comments, ref i) || function.Body[i] is not Ir.Store store
            || store.Ptr is not Ir.Cell { W: 2 } d || store.Off != 0 || store.Value is not Ir.Cell vt
            || vt.W != 1 || vt.Sym != t.Sym || store.Bytes != 1 || store.Volatile)
        {
            return false;
        }

        string dst = d.Sym;
        string src = s.Sym;
        i++;
        if (!SkipComments(function, comments, ref i) || !IsStep(function.Body[i], dst, 2, true))
        {
            return false;
        }

        i++;
        if (!SkipComments(function, comments, ref i) || !IsStep(function.Body[i], src, 2, true))
        {
            return false;
        }

        i++;
        if (!SkipComments(function, comments, ref i) || !IsStep(function.Body[i], n.Sym, 2, false))
        {
            return false;
        }

        i++;
        if (!SkipComments(function, comments, ref i) || function.Body[i] is not Ir.Jmp jmp || jmp.Target != top.Name)
        {
            return false;
        }

        i++;
        if (!SkipComments(function, comments, ref i) || function.Body[i] is not Ir.Label endLabel || endLabel.Name != end)
        {
            return false;
        }

        if (dst == src || dst == n.Sym || src == n.Sym)
        {
            return false;
        }

        if (!SingleUse(function, index, i, [dst, src, n.Sym, t.Sym], top.Name, end))
        {
            return false;
        }

        Word? dstWord = Pair(_isa.Loc(dst, 2, 0), _isa.Loc(dst, 2, 1));
        Word? srcWord = Pair(_isa.Loc(src, 2, 0), _isa.Loc(src, 2, 1));
        Word? countWord = Pair(_isa.Loc(n.Sym, 2, 0), _isa.Loc(n.Sym, 2, 1));
        if (dstWord is not { } dstW || srcWord is not { } srcW || countWord is not { } countW)
        {
            return false;
        }

        foreach (Ir.Src comment in comments)
        {
            EmitSource(comment);
        }

        if (_isa is not ICopyLoop copy || !copy.TryCopyLoop(dstW, srcW, countW))
        {
            return false;
        }

        _acc.Clear();
        Raw($"{Mangle(function, end)}:");
        next = i + 1;
        return true;
    }

    /// <summary>Wywołanie ogonowe: <c>Call</c> z wynikiem i zaraz <c>Ret</c> tej samej wartości
    /// zamienia w skok (bez call/ret i bez przenoszenia wyniku). Bezpieczne tylko bez ramki
    /// (<see cref="Ir.Function.Saved"/> puste), bez zapisów rejestrów wokół wołania i dla wyniku
    /// void albo 1-2 bajtów (już w miejscu docelowym).</summary>
    /// <param name="function">Emitowana funkcja.</param>
    /// <param name="index">Pozycja kandydata na <c>Call</c>.</param>
    /// <param name="next">Pozycja za zużytym <c>Ret</c> (gdy dopasowano).</param>
    /// <returns>Czy wyemitowano skok ogonowy.</returns>
    private bool TryEmitTailCall(Ir.Function function, int index, out int next)
    {
        next = index;
        if (_isa is not ITailCall || function.Body[index] is not Ir.Call call)
        {
            return false;
        }

        int retIndex = index + 1;
        var skipped = new List<Ir.Src>();
        while (retIndex < function.Body.Count && function.Body[retIndex] is Ir.Src comment)
        {
            skipped.Add(comment);
            retIndex++;
        }

        if (retIndex >= function.Body.Count || function.Body[retIndex] is not Ir.Ret ret)
        {
            return false;
        }

        bool voidTail = call.Result is null && ret.Value is null && ret.W == 0;
        bool valueTail = call.Result is { } result && ret.Value is Ir.Cell value
            && value.Sym == result.Sym && value.W == result.W && ret.W == result.W && result.W is 1 or 2;
        if (!voidTail && !valueTail)
        {
            return false;
        }

        if (function.Saved.Count != 0 || _isa.Cells.SavedAround(call).Count != 0)
        {
            return false;
        }

        // Wynik helpera v1 wraca przez cc_ret, więc skok ogonowy do niego gubiłby wynik w rejestrach
        // (void przechodzi: bez wyniku wystarczy jmp z argumentami w cc_argN).
        bool regCall = RegCall(call);
        if (!regCall && !voidTail)
        {
            return false;
        }

        foreach (Ir.Src comment in skipped)
        {
            EmitSource(comment);
        }

        bool earlyFp = false;
        if (call.Indirect is { } indirect && HasRegArgs(call))
        {
            earlyFp = true;
            _usesIcall = true;
            ((IRegArgs)_isa).SetupFp(_isa.Sym(indirect.Sym));
            _acc.Clear(); // SetupFp ładuje A, a EmitCallArgs zaczyna od LoadA
        }

        EmitCallArgs(call);
        if (call.Indirect is not null)
        {
            if (earlyFp)
            {
                _isa.Jump("__icall");
            }
            else
            {
                _usesIcall = true;
                ((ITailCall)_isa).TailCallIndirect(_isa.Sym(call.Indirect.Sym));
            }
        }
        else
        {
            _isa.TailCall(_isa.Sym(call.Direct!));
        }

        next = retIndex + 1;
        return true;
    }
}
