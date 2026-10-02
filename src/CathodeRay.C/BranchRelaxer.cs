namespace CathodeRay.C;

/// <summary>Relaksacja skoków warunkowych CPU z krótkimi (względnymi) rozgałęzieniami: selektor emituje bezpieczną postać
/// <c>bXX pomiń; jmp cel; pomiń:</c> (5 B), a tu każda taka trójka, której cel jest w zasięgu, staje się jednym skokiem
/// <c>bYY cel</c> (2 B) z odwróconym warunkiem. Zasięg liczony na układzie z długimi skokami; skracanie innych trójek tylko go zmniejsza,
/// więc wszystkie zamiany można wykonać jednocześnie. <see cref="Shorten"/> robi to samo dla pojedynczych długich skoków (Z80).
/// Wszystko na rozłożonych liniach (<see cref="AsmInsn"/>) — koniec z regexami na surowym tekście.</summary>
internal static partial class BranchRelaxer
{
    private const int Reach = 120;

    /// <summary>Zamienia trójki w tekście funkcji.</summary>
    /// <param name="lines">Linie funkcji.</param>
    /// <param name="size">Rozmiar linii (instrukcji) w bajtach; etykiety i komentarze mają 0.</param>
    /// <param name="invert">Odwrócenie mnemonika skoku warunkowego (<c>bne</c> → <c>beq</c>) albo <see langword="null"/>, gdy to nie jest skok warunkowy.</param>
    /// <returns>Nowe linie.</returns>
    public static List<AsmInsn> Apply(List<AsmInsn> lines, Func<AsmInsn, int> size, Func<string, string?> invert)
    {
        (int[] offsets, Dictionary<string, int> labels) = Layout(lines, size);
        var output = new List<AsmInsn>(lines.Count);
        bool changed = false;
        for (int i = 0; i < lines.Count; i++)
        {
            if (i + 2 < lines.Count && Triple(lines, i) is { } triple && invert(triple.Branch) is { } inverted && labels.TryGetValue(triple.Target, out int at))
            {
                int distance = at - (offsets[i] + 2);
                if (distance is >= -Reach and <= Reach)
                {
                    output.Add(new AsmInsn("        ", null, string.Empty, inverted, " ", triple.Target, null, lines[i].Newline));
                    i += 2;
                    changed = true;
                    continue;
                }
            }

            output.Add(lines[i]);
        }

        return changed ? output : lines;
    }

    /// <summary>Usuwa bezwarunkowy skok (Z80 <c>jp</c>, 8080 <c>jmp</c>) do etykiety, która stoi tuż za nim (dzielą je tylko etykiety
    /// i komentarze), np. skok na koniec <c>if</c> z pustą gałęzią <c>else</c>.</summary>
    /// <param name="lines">Linie funkcji.</param>
    /// <returns>Linie bez takich skoków.</returns>
    public static List<AsmInsn> DropJumpToNext(List<AsmInsn> lines)
    {
        var output = new List<AsmInsn>(lines.Count);
        int i = 0;
        while (i < lines.Count)
        {
            if (IsPlainJump(lines[i]) is { } target)
            {
                int j = i + 1;
                while (j < lines.Count && ((IsBareLabel(lines[j]) && lines[j].Label != target + ":") || IsCommentOnly(lines[j])))
                {
                    j++;
                }

                if (j < lines.Count && lines[j].Label == target + ":")
                {
                    i++;
                    continue;
                }
            }

            output.Add(lines[i]);
            i++;
        }

        return output;
    }

    /// <summary><c>jp cc,X; jp T; X:</c> (skok przez skok z porównania na równość) → <c>jp !cc,T; X:</c>.</summary>
    /// <param name="lines">Linie funkcji.</param>
    /// <param name="negate">Negacja warunku (<c>nz</c> → <c>z</c>) albo null dla nieobsługiwanych.</param>
    /// <returns>Linie po złożeniu skoków przez skok.</returns>
    public static List<AsmInsn> SkipOverJump(List<AsmInsn> lines, Func<string, string?> negate)
    {
        var output = new List<AsmInsn>(lines.Count);
        int i = 0;
        while (i < lines.Count)
        {
            if (i + 2 < lines.Count && JumpCondTarget(lines[i]) is { } first && JumpTarget(lines[i + 1]) is { } target
                && IsBareLabel(lines[i + 2]) && lines[i + 2].Label == first.Target + ":" && negate(first.Cond) is { } negated)
            {
                output.Add(new AsmInsn(lines[i].Indent, null, string.Empty, "jp", " ", negated + "," + target, null, lines[i].Newline));
                output.Add(lines[i + 2]);
                i += 3;
                continue;
            }

            output.Add(lines[i]);
            i++;
        }

        return output;
    }

    /// <summary>Zamienia pojedyncze długie skoki na krótkie względne (Z80: <c>jp cc,L</c> → <c>jr cc,L</c>), gdy cel jest w zasięgu.
    /// Odległość = adres celu − adres skoku, liczona na układzie z długimi skokami; w przód pomniejszona o skrócenie samego skoku
    /// (leży między nim a celem). Skracanie innych skoków tylko zbliża cele, więc wszystkie zamiany można wykonać jednocześnie.</summary>
    /// <param name="lines">Linie funkcji.</param>
    /// <param name="size">Rozmiar linii (instrukcji) w bajtach; wolno go zawyżać, nie wolno zaniżać.</param>
    /// <param name="shorten">Krótka postać skoku i jego cel albo <see langword="null"/>, gdy linia nie ma krótkiej postaci.</param>
    /// <param name="back">Najmniejsza dopuszczalna odległość (ujemna).</param>
    /// <param name="forward">Największa dopuszczalna odległość.</param>
    /// <returns>Nowe linie.</returns>
    public static List<AsmInsn> Shorten(List<AsmInsn> lines, Func<AsmInsn, int> size, Func<AsmInsn, (AsmInsn Short, string Target)?> shorten, int back, int forward)
    {
        (int[] offsets, Dictionary<string, int> labels) = Layout(lines, size);
        var output = new List<AsmInsn>(lines.Count);
        bool changed = false;
        for (int i = 0; i < lines.Count; i++)
        {
            if (shorten(lines[i]) is { } jump && labels.TryGetValue(jump.Target, out int at))
            {
                int distance = at - offsets[i];
                if (distance >= back && distance - (size(lines[i]) - size(jump.Short)) <= forward)
                {
                    output.Add(jump.Short);
                    changed = true;
                    continue;
                }
            }

            output.Add(lines[i]);
        }

        return changed ? output : lines;
    }

    /// <summary>Skok bezwarunkowy <c>jp/jmp cel</c> (bez warunku i nawiasów) albo null.</summary>
    private static string? IsPlainJump(AsmInsn line)
    {
        if (line.Label is not null || line.Comment is not null || line.Mnemonic is not "jp" and not "jmp" || line.Gap.Length == 0)
        {
            return null;
        }

        string target = line.Operands.TrimEnd();
        return target.Length > 0 && target.IndexOfAny([' ', '\t', ',', '(']) < 0 ? target : null;
    }

    /// <summary>Samotna etykieta (bez instrukcji i komentarza, jak w lookaheadach starego tekstu).</summary>
    private static bool IsBareLabel(AsmInsn line) =>
        line.Label is not null && line.Mnemonic is null && line.Comment is null;

    /// <summary>Samotny komentarz.</summary>
    private static bool IsCommentOnly(AsmInsn line) => line.Label is null && line.Mnemonic is null && line.Comment is not null;

    /// <summary>Warunek i cel skoku <c>jp cc,cel</c> albo null.</summary>
    private static (string Cond, string Target)? JumpCondTarget(AsmInsn line)
    {
        if (line.Label is not null || line.Comment is not null || line.Mnemonic != "jp" || line.Gap.Length == 0)
        {
            return null;
        }

        string[] parts = line.Operands.TrimEnd().Split(',');
        return parts.Length == 2 && parts[0] is "nz" or "z" or "nc" or "c" or "po" or "pe" or "p" or "m"
            && IsSymbol(parts[1]) ? (parts[0], parts[1]) : null;
    }

    /// <summary>Cel bezwarunkowego <c>jp cel</c> albo null.</summary>
    private static string? JumpTarget(AsmInsn line)
    {
        if (line.Label is not null || line.Comment is not null || line.Mnemonic != "jp" || line.Gap.Length == 0)
        {
            return null;
        }

        string target = line.Operands.TrimEnd();
        return IsSymbol(target) ? target : null;
    }

    /// <summary>Nazwa symbolu (jak w celach skoków emitera).</summary>
    private static bool IsSymbol(string text) =>
        text.Length > 0 && (char.IsLetter(text[0]) || text[0] == '_' || text[0] == '.' || text[0] == '$')
        && text.All(static c => char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '$');

    /// <summary>Adresy linii (od początku tekstu) i etykiet.</summary>
    private static (int[] Offsets, Dictionary<string, int> Labels) Layout(List<AsmInsn> lines, Func<AsmInsn, int> size)
    {
        var offsets = new int[lines.Count + 1];
        var labels = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < lines.Count; i++)
        {
            AsmInsn line = lines[i];
            bool marker = line.Mnemonic is null;
            offsets[i + 1] = offsets[i] + (marker ? 0 : size(line));
            if (marker && line.Label is { } label)
            {
                labels[label.TrimEnd(':')] = offsets[i];
            }
        }

        return (offsets, labels);
    }

    /// <summary>Trójka <c>bXX pomiń; jmp cel; pomiń:</c> (mnemonik skoku warunkowego, cel).</summary>
    private static (string Branch, string Target)? Triple(List<AsmInsn> lines, int index)
    {
        AsmInsn first = lines[index];
        AsmInsn jump = lines[index + 1];
        AsmInsn skip = lines[index + 2];
        string target = first.Operands.Trim();
        if (first.Label is not null || first.Comment is not null || first.Mnemonic is not { Length: 3 } branch
            || branch[0] != 'b' || !char.IsLower(branch[1]) || !char.IsLower(branch[2])
            || first.Gap.Length == 0 || HasSpace(target))
        {
            return null;
        }

        if (skip.Label != target + ":" || skip.Mnemonic is not null || skip.Comment is not null)
        {
            return null;
        }

        string dest = jump.Operands.Trim();
        if (jump.Label is not null || jump.Comment is not null || jump.Mnemonic != "jmp" || jump.Gap.Length == 0
            || HasSpace(dest))
        {
            return null;
        }

        return (branch, dest);
    }

    /// <summary>Białe znaki w środku operandów (stary regex wymagał jednego zwartego celu).</summary>
    private static bool HasSpace(string text) => text.IndexOfAny([' ', '\t']) >= 0;
}
