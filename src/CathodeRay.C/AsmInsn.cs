using System.Collections.Generic;
using System.Text;

namespace CathodeRay.C;

/// <summary>Jedna linia asemblera jako bezstratny rozkład (plan 41, poz. 7): wcięcie, etykieta, mnemonik,
/// odstęp, operandy, komentarz i koniec linii składają się z powrotem w identyczny tekst. Konsumenci
/// (docelowo Size/Tidy/Relax) dopasowują po <c>Mnemonic</c>/<c>Operands</c> zamiast po regexach na całości.</summary>
/// <param name="Indent">Wiodące spacje/taby (zwykle puste — emiter nie wcina).</param>
/// <param name="Label">Etykieta z dwukropkiem (<c>fib__ret:</c>) albo null.</param>
/// <param name="Gap1">Odstęp między etykietą a mnemonikiem (pusty też bez labelki).</param>
/// <param name="Mnemonic">Mnemonik/dyrektywa (<c>ld</c>, <c>GLOBAL</c>, <c>.global</c>) albo null.</param>
/// <param name="Gap">Odstęp między mnemonikiem a operandami (może pusty).</param>
/// <param name="Operands">Surowy tekst operandów do komentarza/końca (może pusty).</param>
/// <param name="Comment">Od <c>;</c> do końca (bez nowej linii) albo null; średnik w cudzysłowie nie liczy się.</param>
/// <param name="Newline">Terminacja linii (<c>\r\n</c>, <c>\n</c> albo pusta dla ostatniej).</param>
internal readonly record struct AsmInsn(string Indent, string? Label, string Gap1, string? Mnemonic, string Gap, string Operands, string? Comment, string Newline)
{
    /// <summary>Składa linię z powrotem (odwrotność <see cref="TryParse"/>).</summary>
    /// <returns>Tekst linii z terminatorem.</returns>
    public readonly string Emit() => Indent + Label + Gap1 + (Mnemonic is null ? string.Empty : Mnemonic + Gap + Operands) + Comment + Newline;

    /// <summary>Dzieli tekst na linie (<see cref="AsmInsn"/>).</summary>    /// <param name="text">Tekst asemblera.</param>
    /// <returns>Linie w kolejności.</returns>
    public static AsmInsn[] ParseAll(string text)
    {
        var lines = new List<AsmInsn>();
        int start = 0;
        while (start < text.Length)
        {
            int end = text.IndexOf('\n', start);
            string newline;
            string line;
            if (end < 0)
            {
                newline = string.Empty;
                line = text[start..];
                start = text.Length;
            }
            else
            {
                bool cr = end > start && text[end - 1] == '\r';
                newline = cr ? "\r\n" : "\n";
                line = cr ? text[start..(end - 1)] : text[start..end];
                start = end + 1;
            }

            if (!TryParse(line, newline, out AsmInsn ins))
            {
                throw new InvalidOperationException($"AsmInsn nie parsuje linii: '{line}'.");
            }

            lines.Add(ins);
        }

        return [.. lines];
    }

    /// <summary>Składa tekst z linii (odwrotność <see cref="ParseAll"/>).</summary>
    /// <param name="lines">Linie.</param>
    /// <returns>Tekst identyczny z wejściem.</returns>
    public static string EmitAll(IEnumerable<AsmInsn> lines)
    {
        var text = new StringBuilder();
        foreach (AsmInsn line in lines)
        {
            text.Append(line.Emit());
        }

        return text.ToString();
    }

    /// <summary>Dzieli pojedynczą linię (bez terminatora) na <see cref="AsmInsn"/>.</summary>
    /// <param name="line">Linia bez <c>\r\n</c> (końcowy <c>\r</c> zdjęty jak w <c>Trim</c>).</param>
    /// <returns>Linia.</returns>
    /// <exception cref="InvalidOperationException">Linia nie pasuje do gramatyki.</exception>
    public static AsmInsn ParseLine(string line)
    {
        string stripped = line.TrimEnd('\r', '\n');
        if (!TryParse(stripped, string.Empty, out AsmInsn ins))
        {
            throw new InvalidOperationException($"AsmInsn nie parsuje linii: '{line}'.");
        }

        return ins;
    }

    private static bool TryParse(string line, string newline, out AsmInsn ins)
    {
        ins = default;
        int i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
        {
            i++;
        }

        string indent = line[..i];
        string? label = null;
        int j = i;
        while (j < line.Length && (char.IsLetterOrDigit(line[j]) || line[j] == '_' || line[j] == '@' || line[j] == '.' || line[j] == '$'))
        {
            j++;
        }

        if (j > i && j < line.Length && line[j] == ':')
        {
            label = line[i..(j + 1)];
            i = j + 1;
        }

        int s = i;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
        {
            i++;
        }

        // Odstęp po labelce (albo pusty); bez labelki zawsze pusty, żeby Emit był jednoznaczny.
        string gap1 = label is null ? string.Empty : line[s..i];

        if (i < line.Length && line[i] == ';')
        {
            // cała reszta to komentarz (np. znaczniki ;c:plik:linia)
            ins = new(indent, label, gap1, null, string.Empty, string.Empty, line[i..], newline);
            return true;
        }

        if (i >= line.Length)
        {
            // pusta linia, samo wcięcie albo sama etykieta (z ewentualnym wcięciem/ogonem spacji)
            ins = new(indent, label, gap1, null, string.Empty, string.Empty, null, newline);
            return true;
        }

        int m = i;
        while (m < line.Length && line[m] != ' ' && line[m] != '\t' && line[m] != ';')
        {
            m++;
        }

        if (m == i)
        {
            return false;
        }

        string mnemonic = line[i..m];
        int g = m;
        while (g < line.Length && (line[g] == ' ' || line[g] == '\t'))
        {
            g++;
        }

        int c = g;
        bool quote = false;
        while (c < line.Length)
        {
            if (line[c] == '"')
            {
                quote = !quote;
            }
            else if (line[c] == ';' && !quote)
            {
                break;
            }

            c++;
        }

        ins = new(indent, label, gap1, mnemonic, line[m..g], line[g..c], c < line.Length ? line[c..] : null, newline);
        return true;
    }
}
