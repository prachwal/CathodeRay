using System.Globalization;
using System.Text;

namespace CathodeRay.C;

internal sealed partial class ByteSelector
{
    /// <summary>Porównanie ze znakiem z zerem bez odejmowania: jedna strona to stała 0,
    /// druga idzie do <see cref="ByteIsa.TryBranchZeroSigned"/> jako <c>wartość cond 0</c>.</summary>
    /// <param name="branch">Skok warunkowy.</param>
    /// <param name="target">Etykieta docelowa (zmanglowana).</param>
    /// <returns><see langword="true"/>, gdy sekwencja została wyemitowana.</returns>
    private bool TryBranchZeroSigned(Ir.BrCmp branch, string target)
    {
        bool zeroLeft = IsZero(branch.A);
        bool zeroRight = IsZero(branch.B);
        if (zeroLeft == zeroRight)
        {
            return false;
        }

        Ir.Op value = zeroLeft ? branch.B : branch.A;
        Ir.Cond cond = (zeroLeft, branch.C) switch
        {
            (true, Ir.Cond.Lt) => Ir.Cond.Gt,
            (true, Ir.Cond.Gt) => Ir.Cond.Lt,
            (true, Ir.Cond.Ge) => Ir.Cond.Le,
            (true, Ir.Cond.Le) => Ir.Cond.Ge,
            _ => branch.C,
        };
        if (WordOf(value) is not { } word || !_isa.TryBranchZeroSigned(word, cond, target))
        {
            return false;
        }

        _acc.Clear();
        return true;
    }

    private void EmitBranch(Ir.Function function, Ir.BrCmp branch)
    {
        string target = Mangle(function, branch.Target);
        int width = Math.Max(WidthOf(branch.A), WidthOf(branch.B));
        Ir.Cond cond = branch.C;
        if (cond is Ir.Cond.Eq or Ir.Cond.Ne && width > 1 && (IsZero(branch.B) || IsZero(branch.A)))
        {
            // porównanie z zerem: A = suma bitowa (OR) wszystkich bajtów, flaga Z z ostatniej operacji
            Ir.Op value = IsZero(branch.B) ? branch.A : branch.B;
            LoadA(ByteOf(value, 0));
            for (int i = 1; i < width; i++)
            {
                Alu(ByteAlu.Or, ByteOf(value, i), i == 1);
            }

            _isa.JumpIf(cond == Ir.Cond.Eq ? ByteFlag.Zero : ByteFlag.NotZero, target);
            return;
        }

        if (cond is Ir.Cond.Eq or Ir.Cond.Ne)
        {
            string skip = Label("ne");
            for (int i = 0; i < width; i++)
            {
                LoadA(ByteOf(branch.A, i));
                Cmp(ByteOf(branch.B, i));
                _isa.JumpIf(ByteFlag.NotZero, cond == Ir.Cond.Eq ? skip : target);
            }

            if (cond == Ir.Cond.Eq)
            {
                _isa.Jump(target);
                Raw($"{skip}:");
            }

            return;
        }

        bool signed = cond is Ir.Cond.Lt or Ir.Cond.Le or Ir.Cond.Gt or Ir.Cond.Ge;
        if (signed && width == 2 && TryBranchZeroSigned(branch, target))
        {
            return;
        }

        if (signed && width == 2 && _isa is ISignedBranch signedIsa
            && EmitBranchSignedConst(signedIsa, cond, branch.A, branch.B, width, target))
        {
            return;
        }

        bool swap = cond is Ir.Cond.Gt or Ir.Cond.Le or Ir.Cond.Gtu or Ir.Cond.Leu;
        bool onBorrow = cond is Ir.Cond.Lt or Ir.Cond.Gt or Ir.Cond.Ltu or Ir.Cond.Gtu;
        Ir.Op x = swap ? branch.B : branch.A;
        Ir.Op y = swap ? branch.A : branch.B;
        bool overflow = signed && _isa is ISignedBranch;
        bool bias = signed && !overflow;
        Octet[] xs;
        bool biasX = false;
        if (bias && _isa is IXorCarry xorCarry && xorCarry.XorPreservesCarry)
        {
            xs = BiasedBytes(x, width, true, out biasX);
        }
        else
        {
            xs = Bytes(x, width, bias ? "cc_t0" : null);
        }

        Octet[] ys = Bytes(y, width, bias ? "cc_t1" : null);
        EmitSubBytes(xs, ys, width, overflow, biasX);

        if (overflow)
        {
            ((ISignedBranch)_isa).JumpIfSigned(onBorrow, target);
            return;
        }

        _isa.JumpIf(onBorrow ? ByteFlag.Borrow : ByteFlag.NoBorrow, target);
    }

    /// <summary>Odejmowanie bajt po bajcie do porównania (ładowanie + sub/sbc); z biasX dokleja xor 128
    /// po załadowaniu najstarszego bajtu lewej strony (bias w A zamiast komórki scratch).</summary>
    /// <param name="xs">Bajty lewej strony.</param>
    /// <param name="ys">Bajty prawej strony.</param>
    /// <param name="width">Szerokość.</param>
    /// <param name="overflow">CPU z flagą V (odejmowanie zamiast CMP).</param>
    /// <param name="biasX">Doklej xor 128 do najstarszego bajtu lewej strony.</param>
    private void EmitSubBytes(Octet[] xs, Octet[] ys, int width, bool overflow, bool biasX)
    {
        for (int i = 0; i < width; i++)
        {
            LoadA(xs[i]);
            if (i == width - 1 && biasX)
            {
                Alu(ByteAlu.Xor, new Octet(true, "128"), true);
            }

            if (width == 1 && !overflow)
            {
                Cmp(ys[i]);
            }
            else
            {
                Alu(ByteAlu.Sub, ys[i], i == 0);
            }
        }
    }

    /// <summary>Porównanie ze znakiem ze stałą 16-bitową na CPU z flagą V: normalizacja do <c>Lt</c>/<c>Ge</c>
    /// ze stałą po prawej (<c>Le</c>/<c>Gt</c> przez +1, gdy wynik mieści się w int), odejmowanie wspólną pętlą
    /// i skoki prymitywem bez trampoliny (V rozstrzyga samo). Zwraca <c>false</c> (wołający idzie starą drogą),
    /// gdy brak stałej W2 albo +1 nielegalne.</summary>
    /// <param name="isa">ISA ze zdolnością <see cref="ISignedBranch"/>.</param>
    /// <param name="cond">Warunek.</param>
    /// <param name="a">Lewy operand.</param>
    /// <param name="b">Prawy operand.</param>
    /// <param name="width">Szerokość (zawsze 2).</param>
    /// <param name="target">Etykieta gałęzi prawdy (spadek to fałsz).</param>
    /// <returns>Czy wyemitowano porównanie.</returns>
    private bool EmitBranchSignedConst(ISignedBranch isa, Ir.Cond cond, Ir.Op a, Ir.Op b, int width, string target)
    {
        // Stała jako Imm W1/W2 (węższe ujemne nie docierają tu: promocja typów poszerza je ze znakiem
        // do W2 wcześniej, co widać po sub/sbc z pełnym 0xFFFF dla -1); wartość z bajtów faktycznie
        // odejmowanych (niespójność reprezentacji wykluczona z konstrukcji).
        Ir.Op? constSide = a is Ir.Imm ? a : b is Ir.Imm ? b : null;
        if (constSide is not Ir.Imm imm || (imm.W != 1 && imm.W != 2))
        {
            return false;
        }

        if (!Const16(ByteOf(constSide, 0), ByteOf(constSide, 1), out int raw))
        {
            return false;
        }

        if (imm.W == 1 && raw > 0xFF)
        {
            return false;
        }

        int c = imm.W == 2 ? (short)raw : raw;

        bool constLeft = ReferenceEquals(constSide, a);
        bool less;
        int adjusted;
        switch (cond)
        {
            case Ir.Cond.Lt when !constLeft: less = true; adjusted = c; break;
            case Ir.Cond.Ge when !constLeft: less = false; adjusted = c; break;
            case Ir.Cond.Le when !constLeft && c <= 32766: less = true; adjusted = c + 1; break;
            case Ir.Cond.Gt when !constLeft && c <= 32766: less = false; adjusted = c + 1; break;
            case Ir.Cond.Lt when constLeft && c <= 32766: less = false; adjusted = c + 1; break;
            case Ir.Cond.Ge when constLeft && c <= 32766: less = true; adjusted = c + 1; break;
            case Ir.Cond.Le when constLeft: less = false; adjusted = c; break;
            case Ir.Cond.Gt when constLeft: less = true; adjusted = c; break;
            default: return false;
        }

        Octet[] xs = [ByteOf(constLeft ? b : a, 0), ByteOf(constLeft ? b : a, 1)];
        Octet[] ys = [new Octet(true, Number(adjusted & 0xFF)), new Octet(true, Number((adjusted >> 8) & 0xFF))];
        EmitSubBytes(xs, ys, width, overflow: true, biasX: false);
        if (!isa.TryBranchSignedConst(less, adjusted, target))
        {
            isa.JumpIfSigned(less, target);
        }

        return true;
    }

    /// <summary>Bajty operandu; dla porównania ze znakiem najstarszy bajt jest odwrócony (xor 128), więc porównanie
    /// bez znaku daje wynik ze znakiem.</summary>
    private Octet[] Bytes(Ir.Op op, int width, string? biasCell)
    {
        Octet[] bytes = [.. Enumerable.Range(0, width).Select(i => ByteOf(op, i))];
        if (biasCell is null)
        {
            return bytes;
        }

        Octet top = bytes[width - 1];
        if (top.IsImmediate && int.TryParse(top.Text, CultureInfo.InvariantCulture, out int value))
        {
            bytes[width - 1] = new Octet(true, Number(value ^ 0x80));
            return bytes;
        }

        LoadA(top);
        Alu(ByteAlu.Xor, new Octet(true, "128"), true);
        StoreA(biasCell);
        bytes[width - 1] = new Octet(false, biasCell);
        return bytes;
    }

    /// <summary>Bajty operandu do porównania ze znakiem bez komórki scratch: stały najstarszy bajt odwrócony
    /// w miejscu (xor 128 jak w <see cref="Bytes"/>), nie-stały wołający dokończy xorem po LoadA (biasInLoop,
    /// doklejane w <see cref="EmitSubBytes"/>).</summary>
    /// <param name="op">Operand.</param>
    /// <param name="width">Szerokość.</param>
    /// <param name="wantBias">Czy odwracać najstarszy bajt.</param>
    /// <param name="biasInLoop">Nie-stały wierzchołek wymaga xora w pętli.</param>
    /// <returns>Bajty operandu.</returns>
    private Octet[] BiasedBytes(Ir.Op op, int width, bool wantBias, out bool biasInLoop)
    {
        Octet[] bytes = [.. Enumerable.Range(0, width).Select(i => ByteOf(op, i))];
        biasInLoop = false;
        if (!wantBias)
        {
            return bytes;
        }

        Octet top = bytes[width - 1];
        if (top.IsImmediate && int.TryParse(top.Text, CultureInfo.InvariantCulture, out int value))
        {
            bytes[width - 1] = new Octet(true, Number(value ^ 0x80));
            return bytes;
        }

        biasInLoop = true;
        return bytes;
    }
}
