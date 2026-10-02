namespace CathodeRay.C;

/// <summary>Peephole na rozłożonych liniach (plan 41, poz. 8): te same reguły co tekstowe <c>Tidy</c>,
/// ale dopasowane po polach <see cref="AsmInsn"/>. Etykiety i komentarze są strukturalne (koniec z lookaheadami);
/// kolejność reguł jak w starym tekście (kaskady zachowane).</summary>
internal static class AsmPeephole
{
    /// <summary>Tekst po peephole dla Z80.</summary>
    /// <param name="text">Tekst funkcji.</param>
    /// <returns>Tekst po regułach.</returns>
    public static string TidyZ80(string text) => AsmInsn.EmitAll(Rules(AsmInsn.ParseAll(text), mov: "ld", xor: "xor", zeroLd: "ld", swapReload: true));

    /// <summary>Tekst po peephole dla 8080.</summary>
    /// <param name="text">Tekst funkcji.</param>
    /// <returns>Tekst po regułach.</returns>
    public static string Tidy8080(string text) => AsmInsn.EmitAll(Rules(AsmInsn.ParseAll(text), mov: "mov", xor: "xra", zeroLd: "mvi", swapReload: false));

    private static List<AsmInsn> Rules(AsmInsn[] lines, string mov, string xor, string zeroLd, bool swapReload)
    {
        // Przebiegi w kolejności starego tekstu (kaskady zachowane: kopia przed martwym zapisem).
        var output = new List<AsmInsn>(lines);
        output = Redundant(output, mov, "c", "b");
        output = Redundant(output, mov, "e", "d");
        if (swapReload)
        {
            output = SwapReload(output, mov);
        }

        output = DeadBeforeRet(output, mov, "c", "b");
        output = DeadBeforeRet(output, mov, "e", "d");
        output = XorAfter(output, zeroLd, xor, label: true);
        output = XorAfter(output, zeroLd, xor, label: false);
        return output;
    }

    /// <summary>Instrukcja kodu (bez etykiety i komentarza).</summary>
    private static bool IsCode(AsmInsn line) => line.Label is null && line.Comment is null && line.Mnemonic is not null;

    /// <summary>Samotna etykieta (jak w lookaheadach starego tekstu).</summary>
    private static bool IsLabel(AsmInsn line) => line.Label is not null && line.Mnemonic is null && line.Comment is null && line.Gap1.Length == 0;

    /// <summary>Samotny komentarz.</summary>
    private static bool IsComment(AsmInsn line) => line.Label is null && line.Mnemonic is null && line.Comment is not null;

    /// <summary>Ruch <c>mov dst,src</c> (dokładnie, jak literał w starym regexie).</summary>
    private static bool IsMove(AsmInsn line, string mov, string dst, string src) =>
        IsCode(line) && line.Mnemonic == mov && line.Gap == " " && line.Operands == dst + "," + src;

    /// <summary>Cztery kolejne kopie tam-i-z-powrotem: zostają pierwsze dwie.</summary>
    private static List<AsmInsn> Redundant(List<AsmInsn> lines, string mov, string lo, string hi)
    {
        var output = new List<AsmInsn>(lines.Count);
        int i = 0;
        while (i < lines.Count)
        {
            if (i + 3 < lines.Count
                && IsMove(lines[i], mov, lo, "l") && IsMove(lines[i + 1], mov, hi, "h")
                && IsMove(lines[i + 2], mov, "l", lo) && IsMove(lines[i + 3], mov, "h", hi))
            {
                output.Add(lines[i]);
                output.Add(lines[i + 1]);
                i += 4;
                continue;
            }

            output.Add(lines[i]);
            i++;
        }

        return output;
    }

    /// <summary><c>ex de,hl</c> z natychmiastowym przeładowaniem HL z DE → zwykła kopia.</summary>
    private static List<AsmInsn> SwapReload(List<AsmInsn> lines, string mov)
    {
        var output = new List<AsmInsn>(lines.Count);
        int i = 0;
        while (i < lines.Count)
        {
            if (i + 2 < lines.Count
                && IsCode(lines[i]) && lines[i].Mnemonic == "ex" && lines[i].Gap == " " && lines[i].Operands == "de,hl"
                && IsMove(lines[i + 1], mov, "l", "e") && IsMove(lines[i + 2], mov, "h", "d"))
            {
                output.Add(Copy(lines[i], mov, "e,l"));
                output.Add(Copy(lines[i], mov, "d,h") with { Newline = lines[i + 2].Newline });
                i += 3;
                continue;
            }

            output.Add(lines[i]);
            i++;
        }

        return output;
    }

    /// <summary>Martwy zapis do pary tuż przed ret (etykiety/komentarze po drodze zostają).</summary>
    private static List<AsmInsn> DeadBeforeRet(List<AsmInsn> lines, string mov, string lo, string hi)
    {
        var output = new List<AsmInsn>(lines.Count);
        int i = 0;
        while (i < lines.Count)
        {
            if (i + 1 < lines.Count && IsMove(lines[i], mov, lo, "l") && IsMove(lines[i + 1], mov, hi, "h"))
            {
                int j = i + 2;
                while (j < lines.Count && (IsLabel(lines[j]) || IsComment(lines[j])))
                {
                    j++;
                }

                if (j < lines.Count && lines[j].Label is null && lines[j].Mnemonic == "ret"
                    && lines[j].Gap.Length == 0 && lines[j].Operands.Length == 0 && lines[j].Comment is null)
                {
                    i += 2;
                    continue;
                }
            }

            output.Add(lines[i]);
            i++;
        }

        return output;
    }

    /// <summary>Ładowanie zera po etykiecie (<paramref name="label"/> = true) albo po bezwarunkowym
    /// <c>call</c> (false), przez same komentarze.</summary>
    private static List<AsmInsn> XorAfter(List<AsmInsn> lines, string zeroLd, string xor, bool label)
    {
        var output = new List<AsmInsn>(lines.Count);
        int i = 0;
        while (i < lines.Count)
        {
            int j = i;
            if (label)
            {
                if (!IsLabel(lines[j]))
                {
                    output.Add(lines[i]);
                    i++;
                    continue;
                }

                j++;
            }
            else
            {
                if (!IsCode(lines[j]) || lines[j].Mnemonic != "call" || lines[j].Gap != " "
                    || lines[j].Operands.Contains(','))
                {
                    output.Add(lines[i]);
                    i++;
                    continue;
                }

                j++;
            }

            while (j < lines.Count && IsComment(lines[j]))
            {
                j++;
            }

            if (j < lines.Count && IsCode(lines[j]) && lines[j].Mnemonic == zeroLd && lines[j].Gap == " "
                && lines[j].Operands == "a,0")
            {
                for (int k = i; k < j; k++)
                {
                    output.Add(lines[k]);
                }

                output.Add(Xor(lines[j], xor));
                i = j + 1;
                continue;
            }

            output.Add(lines[i]);
            i++;
        }

        return output;
    }

    /// <summary>Kopia z zamienionym mnemonikiem/operandami (wcięcie i newline z oryginału).</summary>
    private static AsmInsn Copy(AsmInsn line, string mnemonic, string operands) =>
        line with { Label = null, Gap1 = string.Empty, Mnemonic = mnemonic, Gap = " ", Operands = operands, Comment = null };

    /// <summary><c>xor a</c> w miejscu ładowania zera (wcięcie i newline z oryginału).</summary>
    private static AsmInsn Xor(AsmInsn line, string xor) => Copy(line, xor, "a");
}
