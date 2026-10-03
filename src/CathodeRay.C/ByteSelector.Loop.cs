using System.Globalization;
using System.Text;

namespace CathodeRay.C;

internal sealed partial class ByteSelector
{
    /// <summary>Wykrywa pętlę licznikową <c>uchar</c> (pod <c>djnz</c> w poz. 6): kształt while
    /// (test nagłówka + <c>Jmp(top)</c> na końcu) albo do-while (test na dole do <c>top</c>).
    /// Licznik: jedno <c>-1</c> tuż przed skokiem powrotnym, brak innych zapisów w regionie,
    /// brak użyć za pętlą (bez writebacku) i brak obcych skoków do środka. Bez emisji.</summary>
    /// <param name="function">Funkcja.</param>
    /// <param name="index">Pozycja etykiety pętli.</param>
    /// <returns>Dopasowanie albo null.</returns>
    internal static CountLoopMatch? MatchCountLoop(Ir.Function function, int index)
    {
        int i = index;
        while (i < function.Body.Count && function.Body[i] is Ir.Src)
        {
            i++;
        }

        if (i >= function.Body.Count || function.Body[i] is not Ir.Label top)
        {
            return null;
        }

        var labels = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int k = 0; k < function.Body.Count; k++)
        {
            if (function.Body[k] is Ir.Label label && !labels.ContainsKey(label.Name))
            {
                labels[label.Name] = k;
            }
        }

        // Kształt while: BrCmp(counter, imm) -> end ... dec ... Jmp(top) ... Label(end).
        int j = i + 1;
        while (j < function.Body.Count && function.Body[j] is Ir.Src)
        {
            j++;
        }

        if (j < function.Body.Count && function.Body[j] is Ir.BrCmp header && header.A is Ir.Cell hc && hc.W == 1
            && header.B is Ir.Imm && labels.TryGetValue(header.Target, out int endIdx) && endIdx > j)
        {
            int k = endIdx - 1;
            while (k > j && function.Body[k] is Ir.Src)
            {
                k--;
            }

            if (k > j && function.Body[k] is Ir.Jmp back && back.Target == top.Name)
            {
                int d = k - 1;
                while (d > j && function.Body[d] is Ir.Src)
                {
                    d--;
                }

                if (d > j && IsDec(function.Body[d], hc.Sym) && CheckCounter(function, labels, hc.Sym, i, endIdx))
                {
                    return new CountLoopMatch(hc.Sym, top.Name, header.Target, endIdx + 1);
                }
            }

            return null;
        }

        // Kształt do-while: ... dec ... BrCmp(counter, imm) -> top ... Label(end).
        for (int b = i + 1; b < function.Body.Count; b++)
        {
            if (function.Body[b] is Ir.BrCmp bottom && bottom.Target == top.Name
                && bottom.A is Ir.Cell bc && bc.W == 1 && bottom.B is Ir.Imm)
            {
                int d = b - 1;
                while (d > i && function.Body[d] is Ir.Src)
                {
                    d--;
                }

                if (d <= i || !IsDec(function.Body[d], bc.Sym))
                {
                    return null;
                }

                int e = b + 1;
                while (e < function.Body.Count && function.Body[e] is Ir.Src)
                {
                    e++;
                }

                if (e >= function.Body.Count || function.Body[e] is not Ir.Label end)
                {
                    return null;
                }

                if (!CheckCounter(function, labels, bc.Sym, i, e))
                {
                    return null;
                }

                return new CountLoopMatch(bc.Sym, top.Name, end.Name, e + 1);
            }
        }

        return null;
    }

    /// <summary>Krok <c>c = c - 1</c> na komórce 1-bajtowej (<c>Sub 1</c> albo równoważne <c>Add 255</c>).</summary>
    private static bool IsDec(Ir.Ins ins, string sym) => ins is Ir.Bin step
        && step.Dst is { W: 1 } dst && dst.Sym == sym
        && step.A is Ir.Cell a && a.Sym == sym && a.W == 1
        && ((step.Kind == Ir.BinOp.Sub && step.B is Ir.Imm { Value: 1 })
            || (step.Kind == Ir.BinOp.Add && step.B is Ir.Imm { Value: 255 }));

    /// <summary>Licznik ma w regionie dokładnie jeden zapis (ten z <c>IsDec</c>), nie jest czytany
    /// za pętlą (bez writebacku) i nikt obcy nie skacze do środka regionu. Zapisy przed pętlą
    /// (inicjalizacja) są dozwolone.</summary>
    private static bool CheckCounter(Ir.Function function, Dictionary<string, int> labels, string sym, int from, int to)
    {
        int writes = 0;
        for (int k = from; k <= to; k++)
        {
            if (IrFacts.Def(function.Body[k]) is { } def && def.Sym == sym)
            {
                writes++;
            }
        }

        if (writes != 1)
        {
            return false;
        }

        for (int k = to + 1; k < function.Body.Count; k++)
        {
            foreach (Ir.Op op in IrFacts.Uses(function.Body[k]))
            {
                if ((op is Ir.Cell cell && cell.Sym == sym) || (op is Ir.AddrOf addr && addr.Sym == sym))
                {
                    return false;
                }
            }
        }

        for (int k = 0; k < function.Body.Count; k++)
        {
            if (k >= from && k <= to)
            {
                continue;
            }

            if ((function.Body[k] is Ir.Jmp jmp && labels.TryGetValue(jmp.Target, out int jt) && jt >= from && jt <= to)
                || (function.Body[k] is Ir.BrCmp other && labels.TryGetValue(other.Target, out int bt) && bt >= from && bt <= to))
            {
                return false;
            }
        }

        return true;
    }

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

    /// <summary>Emisja pętli licznikowej z <c>djnz</c> (plan 43, poz. 6; tylko Z80 przez <c>ICountLoop</c>).
    /// Licznik musi być w B (pre-kolor z alokatora) — inaczej stara emisja. Ciało idzie normalną ścieżką
    /// bez instrukcji <c>dec</c> (jej licznik żyje w B), na końcu <c>djnz top</c> albo zachowawczo
    /// <c>dec b; jp nz,top</c> dla długich ciał (szacunek 6 B na instrukcję IR).</summary>
    /// <param name="function">Emitowana funkcja.</param>
    /// <param name="bodyIndex">Pozycja etykiety pętli.</param>
    /// <param name="next">Pozycja za etykietą końca.</param>
    /// <returns>Czy wyemitowano blok z <c>djnz</c>.</returns>
    private bool TryEmitCountLoop(Ir.Function function, int bodyIndex, out int next)
    {
        next = bodyIndex;
        if (_isa is not ICountLoop || MatchCountLoop(function, bodyIndex) is not { } match)
        {
            return false;
        }

        string home = _isa.Cells.Resolve(_isa.Loc(match.Counter, 1, 0)) ?? string.Empty;
        if (!home.Equals("b", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var labels = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int k = 0; k < function.Body.Count; k++)
        {
            if (function.Body[k] is Ir.Label label && !labels.ContainsKey(label.Name))
            {
                labels[label.Name] = k;
            }
        }

        int topIdx = bodyIndex;
        while (topIdx < function.Body.Count && function.Body[topIdx] is Ir.Src)
        {
            topIdx++;
        }

        int endIdx = labels[match.End];
        int decIdx = -1;
        for (int k = topIdx; k <= endIdx; k++)
        {
            if (IrFacts.Def(function.Body[k]) is { } def && def.Sym == match.Counter)
            {
                if (decIdx >= 0)
                {
                    return false;
                }

                decIdx = k;
            }
        }

        if (decIdx < 0)
        {
            return false;
        }

        int j = decIdx + 1;
        while (j <= endIdx && function.Body[j] is Ir.Src)
        {
            j++;
        }

        bool isJump = j <= endIdx && function.Body[j] is Ir.Jmp jmp && jmp.Target == match.Top;
        bool isBranch = j <= endIdx && function.Body[j] is Ir.BrCmp br && br.Target == match.Top;
        if (!isJump && !isBranch)
        {
            return false;
        }

        for (int k = bodyIndex; k <= endIdx; k++)
        {
            if (k == decIdx)
            {
                continue;
            }

            if (k == j)
            {
                if ((endIdx - topIdx) * 6 > 120)
                {
                    Raw("dec b");
                    _isa.JumpIf(ByteFlag.NotZero, Mangle(function, match.Top));
                }
                else
                {
                    Raw($"djnz {Mangle(function, match.Top)}");
                }

                continue;
            }

            EmitIns(function, function.Body[k], false);
        }

        _acc.Clear();
        next = match.After;
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

        EmitTailJump(call);

        next = retIndex + 1;
        return true;
    }

    /// <summary>Emituje argumenty i skok ogonowy do wołania (wołający sprawdził już zdolność <see cref="ITailCall"/>
    /// i brak ramki).</summary>
    /// <param name="call">Wołanie.</param>
    private void EmitTailJump(Ir.Call call)
    {
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
    }

    /// <summary>Wywołanie ogonowe rozdzielone joinem: obie gałęzie <c>if</c> kończą wołaniem, którego wynik trafia kopią
    /// do wspólnej komórki zwracanej przez jeden <c>Ret</c> (typowy <c>k ? f() : g()</c>). Każde wołanie staje się skokiem
    /// ogonowym, a kopia i wspólny powrót znikają. Konserwatywnie: dokładnie dwie gałęzie, wołania znane w module (W1-2),
    /// brak ramki i zapisów par wokół wołań, a cel joinu i else są osiągalne tylko z tych miejsc.</summary>
    /// <param name="function">Emitowana funkcja.</param>
    /// <param name="index">Pozycja skoku warunkowego <c>if</c>.</param>
    /// <param name="next">Pozycja za wspólnym <c>Ret</c> (gdy dopasowano).</param>
    /// <returns>Czy wyemitowano dwie gałęzie jako skoki ogonowe.</returns>
    private bool TryEmitJoinTailCall(Ir.Function function, int index, out int next)
    {
        next = index;
        if (_isa is not ITailCall || function.Saved.Count != 0 || function.Body[index] is not Ir.BrCmp branch)
        {
            return false;
        }

        int i = index + 1;
        var thenComments = new List<Ir.Src>();
        if (!SkipComments(function, thenComments, ref i) || function.Body[i] is not Ir.Call c1 || c1.Result is not { W: 1 or 2 } r1)
        {
            return false;
        }

        i++;
        var copyComments = new List<Ir.Src>();
        if (!SkipComments(function, copyComments, ref i)
            || function.Body[i] is not Ir.Mov { Dst: Ir.Cell join, Src: Ir.Cell from } || from.Sym != r1.Sym || from.W != r1.W
            || join.Sym == from.Sym || join.W != r1.W)
        {
            return false;
        }

        i++;
        if (!SkipComments(function, [], ref i) || function.Body[i] is not Ir.Jmp jmpDone)
        {
            return false;
        }

        int elseAt = FindLabel(function, branch.Target, index + 1);
        if (elseAt < 0)
        {
            return false;
        }

        int j = elseAt + 1;
        var elseComments = new List<Ir.Src>();
        if (!SkipComments(function, elseComments, ref j) || function.Body[j] is not Ir.Call c2 || c2.Result is not { W: 1 or 2 } r2)
        {
            return false;
        }

        j++;
        var elseCopyComments = new List<Ir.Src>();
        if (!SkipComments(function, elseCopyComments, ref j)
            || function.Body[j] is not Ir.Mov { Dst: Ir.Cell join2, Src: Ir.Cell from2 } || join2.Sym != join.Sym
            || from2.Sym != r2.Sym || from2.W != r2.W)
        {
            return false;
        }

        j++;
        if (!SkipComments(function, [], ref j) || function.Body[j] is not Ir.Label done || done.Name != jmpDone.Target)
        {
            return false;
        }

        j++;
        if (!SkipComments(function, [], ref j) || function.Body[j] is not Ir.Ret ret || ret.Value is not Ir.Cell retCell
            || retCell.Sym != join.Sym || ret.W != join.W)
        {
            return false;
        }

        if (CountJumpsTo(function, jmpDone.Target, index) != 1 || CountJumpsTo(function, branch.Target, index) != 0
            || !TailEligible(c1) || !TailEligible(c2))
        {
            return false;
        }

        EmitIns(function, branch, false);
        foreach (Ir.Src comment in thenComments.Concat(copyComments))
        {
            EmitSource(comment);
        }

        EmitTailJump(c1);
        Raw($"{Mangle(function, branch.Target)}:");
        foreach (Ir.Src comment in elseComments.Concat(elseCopyComments))
        {
            EmitSource(comment);
        }

        EmitTailJump(c2);
        next = j + 1;
        return true;
    }

    /// <summary>Wołanie nadaje się na skok ogonowy z wynikiem: znane w module, W1-2, bez zapisu par wokół.</summary>
    /// <param name="call">Wołanie.</param>
    /// <returns>Czy wynik wraca w miejscu docelowym.</returns>
    private bool TailEligible(Ir.Call call) =>
        RegCall(call) && call.Result is { W: 1 or 2 } && _isa.Cells.SavedAround(call).Count == 0;
}
