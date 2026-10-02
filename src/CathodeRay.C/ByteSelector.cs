using System.Globalization;
using System.Text;

namespace CathodeRay.C;

/// <summary>Dobór instrukcji dla procesorów akumulatorowych: składa kod z prymitywów <see cref="ByteIsa"/>. Komórki leżą
/// w pamięci, argumenty przechodzą przez komórki <c>cc_argN</c>, wynik przez <c>cc_ret</c>. Operacje, których CPU nie ma
/// (mnożenie, dzielenie, przesunięcia o zmienną liczbę, bloki), zamienia wcześniej <see cref="Legalizer"/>.</summary>
internal sealed partial class ByteSelector
{
    private readonly Ir.Module _module;

    private readonly ByteIsa _isa;

    private readonly Dictionary<string, string> _addressCells = new(StringComparer.Ordinal);

    private readonly List<(string Label, string Expression)> _addressList = [];

    /// <summary>Co wiadomo o akumulatorze: adresy pamięci i stałe (z prefiksem <c>#</c>), które mają teraz taką samą wartość jak A.
    /// Każdy zapis do pamięci to zapis A, więc zapisy zachowują tę wiedzę; zmienia ją zmiana A, wołanie i etykieta (wejście z innego miejsca).</summary>
    private readonly HashSet<string> _acc = new(StringComparer.Ordinal);

    private readonly HashSet<string> _volatile;

    /// <summary>Funkcje zdefiniowane w module (rozumieją v2); wołania reszty (helpery uruchomieniowe w ręcznym
    /// asemblerze) selektor marszuje jak v1: argumenty do <c>cc_argN</c>, wynik z <c>cc_ret</c>.</summary>
    private readonly HashSet<string> _defined;

    private int _labels;

    private bool _usesIcall;

    /// <summary>Skoki do etykiety powrotu bieżącej funkcji (gdy zero i ciało kończy skokiem — stopka martwa).</summary>
    private int _retJumps;

    public ByteSelector(Ir.Module module, ByteIsa isa)
    {
        _module = module;
        _isa = isa;
        _volatile = module.Volatile is null ? [] : [.. module.Volatile.Select(isa.Sym)];
        _defined = new([.. module.Functions.Select(static f => f.Name)], StringComparer.Ordinal);
    }

    /// <summary>Składa asembler modułu.</summary>
    /// <returns>Tekst dla asemblera CPU.</returns>
    public string Emit()
    {
        // komórka w rejestrze nie może być zewnętrzna ani zapisywana w ramce (push/pop jej bajtów)
        string? misplaced = _module.ExternCells.Concat(_module.Functions.SelectMany(static f => f.Saved).Select(static o => o.Sym))
            .FirstOrDefault(sym => _isa.Cells.IsRegister(_isa.Sym(sym)));
        if (misplaced is not null)
        {
            throw new InvalidOperationException($"cell {misplaced} is assigned to a register but is extern or saved in a frame.");
        }

        foreach (Ir.Function function in _module.Functions)
        {
            EmitFunction(function);
        }

        return Header() + _isa.Text + PrintInit() + PrintData() + PrintBss();
    }

    private static int WidthOf(Ir.Op op) => op switch
    {
        Ir.Cell cell => cell.W,
        Ir.Imm imm => imm.W,
        _ => 2,
    };

    private static string RetSym(int part) => part == 0 ? "cc_ret" : "cc_ret_h";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Mangle(Ir.Function function, string label) => $"{function.Name}__{label}";

    private static Octet Zero() => new(true, "0");

    private static bool IsZero(Ir.Op op) => op is Ir.Imm { Value: 0 };

    private static string Key(Octet value) => value.IsImmediate ? "#" + value.Text : value.Text;

    /// <summary>Symbol komórki bez przesunięcia (<c>x+1</c> → <c>x</c>).</summary>
    /// <param name="address">Adres bajtu.</param>
    /// <returns>Baza symbolu.</returns>
    private static string Base(string address)
    {
        int cut = address.IndexOfAny(['+', '-']);
        return cut < 0 ? address : address[..cut];
    }

    /// <summary>Wartość stałej 16-bitowej z bajtów operandu (bez znaku 0..65535).</summary>
    /// <param name="lo">Młodszy bajt.</param>
    /// <param name="hi">Starszy bajt.</param>
    /// <param name="value">Wartość albo 0.</param>
    /// <returns>Czy oba bajty są liczbami.</returns>
    private static bool Const16(Octet lo, Octet hi, out int value)
    {
        value = 0;
        if (!lo.IsImmediate || !hi.IsImmediate)
        {
            return false;
        }

        if (!int.TryParse(lo.Text, CultureInfo.InvariantCulture, out int low)
            || !int.TryParse(hi.Text, CultureInfo.InvariantCulture, out int high))
        {
            return false;
        }

        value = low + (high << 8);
        return true;
    }

    /// <summary>Pomija komentarze źródłowe, zbierając je do późniejszej emisji.</summary>
    /// <param name="function">Funkcja.</param>
    /// <param name="comments">Zebrane komentarze.</param>
    /// <param name="i">Pozycja (przesuwana za komentarze).</param>
    /// <returns>Czy pozycja w zakresie ciała.</returns>
    private static bool SkipComments(Ir.Function function, List<Ir.Src> comments, ref int i)
    {
        while (i < function.Body.Count && function.Body[i] is Ir.Src src)
        {
            comments.Add(src);
            i++;
        }

        return i < function.Body.Count;
    }

    /// <summary>Krok licznika/wskaźnika o 1 (<c>x = x + 1</c> albo <c>x = x - 1</c>).</summary>
    /// <param name="ins">Instrukcja.</param>
    /// <param name="sym">Oczekiwany symbol.</param>
    /// <param name="width">Szerokość.</param>
    /// <param name="increment">Plus 1 albo minus 1.</param>
    /// <returns>Czy instrukcja to taki krok.</returns>
    private static bool IsStep(Ir.Ins ins, string sym, int width, bool increment)
    {
        return ins is Ir.Bin step
            && (increment ? step.Kind == Ir.BinOp.Add : step.Kind == Ir.BinOp.Sub)
            && step.Dst is { W: 2 } dst && dst.Sym == sym && dst.W == width
            && step.A is Ir.Cell a && a.Sym == sym && a.W == width
            && step.B is Ir.Imm { Value: 1 };
    }

    /// <summary>Komórki pętli nie występują nigdzie indziej w funkcji, a do jej etykiet nie skacze nikt obcy.</summary>
    /// <param name="function">Funkcja.</param>
    /// <param name="from">Początek dopasowania.</param>
    /// <param name="to">Koniec dopasowania.</param>
    /// <param name="syms">Symbole pętli.</param>
    /// <param name="top">Etykieta góry.</param>
    /// <param name="end">Etykieta końca.</param>
    /// <returns>Czy pojedyncze użycie.</returns>
    private static bool SingleUse(Ir.Function function, int from, int to, string[] syms, string top, string end)
    {
        for (int k = 0; k < function.Body.Count; k++)
        {
            if (k >= from && k <= to)
            {
                continue;
            }

            foreach (Ir.Op op in IrFacts.Operands(function.Body[k]))
            {
                if ((op is Ir.Cell cell && syms.Contains(cell.Sym)) || (op is Ir.AddrOf addr && syms.Contains(addr.Sym)))
                {
                    return false;
                }
            }

            if ((function.Body[k] is Ir.Jmp jmp && (jmp.Target == top || jmp.Target == end))
                || (function.Body[k] is Ir.BrCmp other && (other.Target == top || other.Target == end)))
            {
                return false;
            }
        }

        return true;
    }

    private string At(string sym, int offset) => _isa.At(sym, offset);

    private bool IsVolatile(string address)
    {
        if (_volatile.Count == 0)
        {
            return false;
        }

        int cut = address.IndexOfAny(['+', '-']);
        return _volatile.Contains(cut < 0 ? address : address[..cut]);
    }

    private void LoadA(Octet value)
    {
        string key = Key(value);
        bool cacheable = value.IsImmediate || !IsVolatile(value.Text);
        if (cacheable && _acc.Contains(key))
        {
            return;
        }

        _isa.LoadA(value);
        _acc.Clear();
        if (cacheable)
        {
            _acc.Add(key);
        }
    }

    private void StoreA(string address)
    {
        _isa.StoreA(address);
        if (!IsVolatile(address))
        {
            _acc.Add(address);
        }
    }

    private void Alu(ByteAlu op, Octet value, bool first)
    {
        _isa.Alu(op, value, first);
        _acc.Clear();
    }

    private void Cmp(Octet value)
    {
        _isa.Cmp(value);
        _acc.Clear();
    }

    private void ShlA(bool first)
    {
        _isa.ShlA(first);
        _acc.Clear();
    }

    private void ShrA(bool first)
    {
        _isa.ShrA(first);
        _acc.Clear();
    }

    private void CallDirect(string symbol)
    {
        _isa.Call(symbol);
        _acc.Clear();
    }

    private void CallIndirect(string cell)
    {
        _isa.CallIndirect(cell);
        _acc.Clear();
    }

    private void PtrSetup(string cell, int offset, bool mustCopy = false)
    {
        _isa.PtrSetup(cell, offset, mustCopy);
        _acc.Clear();
    }

    private void PtrLoad(int index)
    {
        _isa.PtrLoad(index);
        _acc.Clear();
    }

    private void Raw(string line)
    {
        _isa.Raw(line);
        if (line.EndsWith(':'))
        {
            _acc.Clear();
        }
    }

    private string Label(string hint) => $"S{++_labels}_{hint}";

    private int MemIndex(int significance, int bytes) => _isa is IByteOrder order && order.BigEndian ? bytes - 1 - significance : significance;

    private Octet ByteOf(Ir.Op op, int index)
    {
        switch (op)
        {
            case Ir.Cell cell:
                return index < cell.W ? new Octet(false, _isa.Loc(cell.Sym, cell.W, index)) : Zero();
            case Ir.Imm imm:
                return new Octet(true, Number(index < imm.W ? (imm.Value >> (8 * index)) & 0xFF : 0));
            case Ir.AddrOf address:
                if (index >= 2)
                {
                    return Zero();
                }

                string? direct = (_isa as IAddressByte)?.AddressByte(At(address.Sym, address.Off), index);
                return direct is not null ? new Octet(true, direct) : new Octet(false, _isa.Loc(AddressCell(address), 2, index));
            default:
                throw new InvalidOperationException($"unsupported operand {op.GetType().Name}.");
        }
    }

    private string AddressCell(Ir.AddrOf address)
    {
        string key = $"{address.Sym}|{address.Off}";
        if (!_addressCells.TryGetValue(key, out string? label))
        {
            label = $"__a{_addressList.Count}";
            _addressCells[key] = label;
            _addressList.Add((label, At(address.Sym, address.Off)));
        }

        return label;
    }

    private string Dst(Ir.Cell cell, int index) => _isa.Loc(cell.Sym, cell.W, index);

    /// <summary>Operand jako słowo 16-bitowe dla <see cref="IPairMoves.TryMoveWord"/> albo <see langword="null"/> (komórka węższa, <c>volatile</c>).</summary>
    private Word? WordOf(Ir.Op op) => op switch
    {
        Ir.Cell { W: 2 } cell => Pair(Dst(cell, 0), Dst(cell, 1)),
        Ir.Imm imm => new Word(true, Number(imm.Value & (imm.W == 1 ? 0xFF : 0xFFFF)), string.Empty),
        Ir.AddrOf address => new Word(true, At(address.Sym, address.Off), string.Empty),
        _ => null,
    };

    /// <summary>Połowa (0 = młodsza, 1 = starsza) operandu 32-bitowego jako słowo albo <see langword="null"/>; węższy operand
    /// ma starszą połowę równą zeru.</summary>
    private Word? HalfOf(Ir.Op op, int half) => op switch
    {
        Ir.Cell { W: 4 } cell => Pair(Dst(cell, 2 * half), Dst(cell, (2 * half) + 1)),
        Ir.Imm { W: 4 } imm => new Word(true, Number((imm.Value >> (16 * half)) & 0xFFFF), string.Empty),
        Ir.Cell { W: 1 } when half == 0 => null,
        _ => half == 0 ? WordOf(op) : new Word(true, "0", string.Empty),
    };

    private Word? Pair(string lo, string hi) => (IsVolatile(lo) || IsVolatile(hi)) ? null : new Word(false, lo, hi);

    /// <summary>Kopia słowa przez ISA; A się nie zmienia, ale bajty celu już nie są równe A.</summary>
    private bool TryMoveWord(Word? dst, Word? src)
    {
        if (dst is not { } target || src is not { } source || _isa is not IPairMoves pairMoves || !pairMoves.TryMoveWord(target, source))
        {
            return false;
        }

        _acc.Remove(target.Lo);
        _acc.Remove(target.Hi);
        return true;
    }
}
