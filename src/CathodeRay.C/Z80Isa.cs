using System.Globalization;
using System.Text;

namespace CathodeRay.C;

/// <summary>Prymitywy Z80 (składnia Zilog): A jako akumulator, HL jako rejestr adresowy (operandy pamięciowe ALU przez
/// <c>LD HL,adres; op A,(HL)</c>, wskaźniki przez <c>LD HL,(komórka)</c>), zapis/odczyt komórek przez <c>LD A,(adres)</c>.</summary>
internal sealed partial class Z80Isa : ByteIsa, ICopyLoop, ISignedBranch, IWordArithmetic, IWordShift, IPairMoves, IPairStack, IResultReg, IResultPairs, ITailCall
{
    private static readonly HashSet<string> ReservedNames = new(
        ["A", "B", "C", "D", "E", "H", "L", "I", "R", "AF", "BC", "DE", "HL", "SP", "IX", "IY", "IXH", "IXL", "IYH", "IYL", "NZ", "Z", "NC", "PO", "PE", "P", "M", "LOW", "HIGH", "MOD", "SHL", "SHR", "AND", "OR", "XOR", "NOT"],
        StringComparer.OrdinalIgnoreCase);

    private int _position;

    public override IEnumerable<string> IndirectSymbols => ["__callhl"];

    public override IReadOnlyList<string> CellRegisters { get; } = ["c", "b", "e", "d"];

    public override IReadOnlyList<string> CellPairs { get; } = ["bc", "de"];

    public bool HasOverflowFlag => true;

    public bool ReturnsInResultReg => true;

    /// <summary>Cel obsługuje wywołanie ogonowe.</summary>
    public bool SupportsTailCall => true;

    protected override IReadOnlySet<string> Reserved => ReservedNames;

    /// <summary>Nazwa CPU w <see cref="CpuModels"/>.</summary>
    protected override string CpuName => "z80";

    public override string Segment(string name) => $"SEGMENT \"{name}\"";

    public override string Global(string sym) => $"GLOBAL {sym}";

    public override string Extern(string sym) => $"EXTERN {sym}";

    public override string Bytes(IEnumerable<int> values) => $"DB {string.Join(", ", values.Select(static v => v.ToString(CultureInfo.InvariantCulture)))}";

    public override string Word(string expression) => $"DW {expression}";

    public override string Reserve(int size) => $"DS {size}";

    public override void LoadA(Octet value) => L(value.IsImmediate ? $"ld a,{value.Text}" : $"ld a,{Operand(value.Text)}");

    public override void StoreA(string address) => L($"ld {Operand(address)},a");

    public override void Alu(ByteAlu op, Octet value, bool first)
    {
        string mnemonic = op switch
        {
            ByteAlu.Add => first ? "add a," : "adc a,",
            ByteAlu.Sub => first ? "sub " : "sbc a,",
            ByteAlu.And => "and ",
            ByteAlu.Or => "or ",
            _ => "xor ",
        };
        Operate(mnemonic, value);
    }

    public override void Cmp(Octet value) => Operate("cp ", value);

    public override void ShlA(bool first) => L(first ? "sla a" : "rl a");

    public override void ShrA(bool first) => L(first ? "srl a" : "rr a");

    public override void Jump(string label) => L($"jp {label}");

    public override void JumpIf(ByteFlag flag, string label) => L(flag switch
    {
        ByteFlag.Zero => $"jp z,{label}",
        ByteFlag.NotZero => $"jp nz,{label}",
        ByteFlag.Borrow => $"jp c,{label}",
        _ => $"jp nc,{label}",
    });

    public override bool TryStep(IReadOnlyList<string> bytes, bool increment)
    {
        string op = increment ? "inc" : "dec";
        if (bytes.Any(IsRegister))
        {
            // rejestr: inc c; para: inc bc (bez flag, kontrakt pozwala); inny układ przez łańcuch ADD/SUB w selektorze
            string? register = bytes.Count == 1 ? Resolve(bytes[0]) : PairOf(new Word(false, bytes[0], bytes[1]));
            if (register is not null)
            {
                L($"{op} {register}");
            }

            return register is not null;
        }

        L($"ld hl,{bytes[0]}");
        if (bytes.Count == 1)
        {
            L($"{op} (hl)");
            return true;
        }

        string skip = LocalLabel();
        if (increment)
        {
            L("inc (hl)");
            L($"jr nz,{skip}");
            L("inc hl");
            L("inc (hl)");
            L($"{skip}:");
            return true;
        }

        L("ld a,(hl)");
        L("dec (hl)");
        L("or a");
        L($"jr nz,{skip}");
        L("inc hl");
        L("dec (hl)");
        L($"{skip}:");
        return true;
    }

    /// <summary>Kopia słowa przez HL (<c>ld hl,(src)</c> albo <c>ld hl,stała</c>; <c>ld (dst),hl</c>), gdy bajty obu stron leżą obok siebie;
    /// cel w parze BC/DE bez HL (<c>ld bc,(src)</c>, <c>ld bc,stała</c>, <c>ld c,e; ld b,d</c>), źródło w parze do pamięci <c>ld (dst),bc</c>.</summary>
    /// <param name="dst">Cel.</param>
    /// <param name="src">Źródło.</param>
    /// <returns><see langword="false"/>, gdy bajty nie są sąsiednie.</returns>
    public bool TryMoveWord(Word dst, Word src)
    {
        if (dst.IsImmediate || !Usable(dst) || !Usable(src))
        {
            return false;
        }

        if (PairOf(dst) is { } pair)
        {
            LoadPair(pair, src);
        }
        else if (PairOf(src) is { } source && !InRegisters(dst))
        {
            L($"ld ({dst.Lo}),{source}");
        }
        else
        {
            LoadPair("hl", src);
            StorePair("hl", dst);
        }

        return true;
    }

    /// <summary>Słowo z pamięci na stos: <c>ld hl,(n); push hl</c>.</summary>
    /// <param name="word">Słowo.</param>
    /// <returns><see langword="false"/>, gdy bajty nie są sąsiednie w pamięci.</returns>
    public bool TryPushWord(Word word)
    {
        if (!InMemoryWord(word))
        {
            return false;
        }

        L($"ld hl,({word.Lo})");
        L("push hl");
        return true;
    }

    /// <summary>Słowo ze stosu do pamięci: <c>pop hl; ld (n),hl</c>; przy wyniku w HL przez DE (w epilogu wolne): <c>pop de; ld (n),de</c>.</summary>
    /// <param name="word">Słowo.</param>
    /// <param name="keepResult">HL niesie wynik.</param>
    /// <returns><see langword="false"/>, gdy bajty nie są sąsiednie w pamięci.</returns>
    public bool TryPopWord(Word word, bool keepResult)
    {
        if (!InMemoryWord(word))
        {
            return false;
        }

        string pair = keepResult ? "de" : "hl";
        L($"pop {pair}");
        L($"ld ({word.Lo}),{pair}");
        return true;
    }

    /// <summary>HL ← słowo (<c>ld hl,nn</c>, <c>ld hl,(n)</c>, <c>ld l,c; ld h,b</c>, z DE <c>ex de,hl</c>).</summary>
    /// <param name="value">Wartość.</param>
    /// <returns><see langword="false"/>, gdy bajty nie leżą parą.</returns>
    public bool TryMoveToResultReg(Word value)
    {
        if (!Usable(value))
        {
            return false;
        }

        // DE nie żyje po powrocie (epilog odtwarza ramkę bez niej): zamiana jest krótsza niż dwie kopie bajtów
        if (PairOf(value) == "de")
        {
            L("ex de,hl");
        }
        else
        {
            LoadPair("hl", value);
        }

        return true;
    }

    /// <summary>Słowo ← HL (<c>ld (n),hl</c>, <c>ld c,l; ld b,h</c>, do DE <c>ex de,hl</c>).</summary>
    /// <param name="dst">Cel.</param>
    /// <returns><see langword="false"/>, gdy bajty nie leżą parą.</returns>
    public bool TryMoveFromResultReg(Word dst)
    {
        if (dst.IsImmediate || !Usable(dst))
        {
            return false;
        }

        // HL po wołaniu to już tylko pomocniczy rejestr: zamiana zamiast dwóch kopii bajtów
        if (PairOf(dst) == "de")
        {
            L("ex de,hl");
        }
        else
        {
            StorePair("hl", dst);
        }

        return true;
    }

    public void ResultByteFromA(int index) => L(index == 0 ? "ld l,a" : "ld h,a");

    public void ResultByteToA(int index) => L(index == 0 ? "ld a,l" : "ld a,h");

    /// <summary>Słowo &lt;&lt; 1: <c>ld hl,src; add hl,hl; ld (dst),hl</c> (wynik zostaje w HL).</summary>
    /// <param name="dst">Cel.</param>
    /// <param name="src">Źródło.</param>
    /// <returns>Wynik jawny (<see cref="WordResult"/>).</returns>
    public WordResult TryShlWord1(Word dst, Word src)
    {
        if (dst.IsImmediate || !Usable(dst) || !Usable(src))
        {
            return new(false, false);
        }

        LoadPair("hl", src);
        L("add hl,hl");
        StorePair("hl", dst);
        return new(true, true);
    }

    /// <summary>Kopia bloku o liczbie z pary BC: test zera, <c>push bc; push de; ld hl,src; ld de,dst;
    /// ld bc,count; ldir; pop de; pop bc</c>. Push chroni pary z komórkami (selektor nie sprawdza
    /// zajętości); liczba niezerowa z konstrukcji (wejście tylko spadkiem testu).</summary>
    /// <param name="dst">Cel.</param>
    /// <param name="src">Źródło.</param>
    /// <param name="count">Liczba bajtów.</param>
    /// <returns><see langword="false"/>, gdy operandy nieużywalne.</returns>
    public bool TryCopyLoop(Word dst, Word src, Word count)
    {
        if (dst.IsImmediate || src.IsImmediate || count.IsImmediate
            || !Usable(dst) || !Usable(src) || !Usable(count))
        {
            return false;
        }

        LoadPair("bc", count);
        L("ld a,b");
        L("or c");
        string skip = LocalLabel();
        L($"jp z,{skip}");
        L("push bc");
        L("push de");
        LoadPair("hl", src);
        LoadPair("de", dst);
        L("ldir");
        L("pop de");
        L("pop bc");
        L($"{skip}:");
        return true;
    }

    /// <summary>Dodawanie/odejmowanie przez HL: <c>ld hl,a; add hl,de</c> albo <c>or a; sbc hl,de</c>, stała ±1..3 przez
    /// <c>inc hl</c>/<c>dec hl</c>, odjęcie stałej liczbowej jako dodanie jej przeciwieństwa.</summary>
    /// <param name="dst">Cel.</param>
    /// <param name="a">Lewy operand.</param>
    /// <param name="b">Prawy operand.</param>
    /// <param name="subtract">Odejmowanie.</param>
    /// <returns><see langword="false"/>, gdy bajty nie są sąsiednie albo oba operandy są stałymi.</returns>
    public WordResult TryAddWord(Word dst, Word a, Word b, bool subtract)
    {
        if (dst.IsImmediate || (a.IsImmediate && b.IsImmediate) || !Usable(dst) || !Usable(a) || !Usable(b))
        {
            return new(false, false);
        }

        if (!subtract && a.IsImmediate)
        {
            (a, b) = (b, a);
        }

        int? constant = null;
        if (b.IsImmediate && int.TryParse(b.Lo, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            constant = (subtract ? -value : value) & 0xFFFF;
            subtract = false;
        }

        // prawy operand: jego własna para BC/DE albo para pomocnicza bez komórek; bez wolnej pary: pamięć przez DE przechowane
        // na stosie (cel zapisuje się dopiero po pop de), stała tak samo, chyba że i lewy operand, i cel są w rejestrach (wtedy łańcuch przez A w selektorze jest krótszy)
        bool steps = constant is { } c && (c <= 3 || c >= 0xFFFD);
        string? rhs = (constant is null ? PairOf(b) : null) ?? Scratch();
        bool save = rhs is null && !steps && (constant is null || !(InRegisters(a) && InRegisters(dst)));
        if (rhs is null && !steps && !save)
        {
            return new(false, false);
        }

        rhs ??= save ? "de" : null;
        LoadPair("hl", a);
        if (save)
        {
            L("push de");
        }

        if (constant is { } k && steps)
        {
            string step = k <= 3 ? "inc hl" : "dec hl";
            for (int i = 0; i < (k <= 3 ? k : 0x10000 - k); i++)
            {
                L(step);
            }
        }
        else
        {
            LoadPair(rhs!, constant is { } n ? new Word(true, n.ToString(CultureInfo.InvariantCulture), string.Empty) : b);
            if (subtract)
            {
                L("or a");
                L($"sbc hl,{rhs}");
            }
            else
            {
                L($"add hl,{rhs}");
            }
        }

        if (save)
        {
            L("pop de");
        }

        // Cel w parze DE: zamiana zamiast dwóch kopii (1 B mniej); wynik nie zostaje w HL.
        // Martwe DE: nadpisane tak czy owak.
        if (PairOf(dst) == "de")
        {
            L("ex de,hl");
            return new(true, false);
        }

        StorePair("hl", dst);
        return new(true, true);
    }

    /// <summary>32 bity przez HL i DE: <c>add hl,de</c> (albo <c>or a; sbc hl,de</c>) na młodszych połówkach, <c>adc hl,de</c>
    /// (<c>sbc hl,de</c>) na starszych; <c>ld</c> między nimi nie rusza przeniesienia.</summary>
    /// <param name="dst">Cel.</param>
    /// <param name="a">Lewy operand.</param>
    /// <param name="b">Prawy operand.</param>
    /// <param name="subtract">Odejmowanie.</param>
    /// <returns><see langword="false"/>, gdy bajty którejś połówki nie są sąsiednie.</returns>
    public bool TryAddLong((Word Lo, Word Hi) dst, (Word Lo, Word Hi) a, (Word Lo, Word Hi) b, bool subtract)
    {
        Word[] memory = [dst.Lo, dst.Hi, a.Lo, a.Hi, b.Lo, b.Hi];
        if (dst.Lo.IsImmediate || dst.Hi.IsImmediate
            || memory.Any(w => !w.IsImmediate && (!Adjacent(w) || IsRegister(w.Lo) || IsRegister(w.Hi))))
        {
            return false;
        }

        if (!subtract && a.Lo.IsImmediate && a.Hi.IsImmediate)
        {
            (a, b) = (b, a);
        }

        // wszystkie połówki w pamięci; DE zajęte w tej funkcji przechowane na stosie
        bool save = Scratch() != "de";
        if (save)
        {
            L("push de");
        }

        L(a.Lo.IsImmediate ? $"ld hl,{a.Lo.Lo}" : $"ld hl,({a.Lo.Lo})");
        L(b.Lo.IsImmediate ? $"ld de,{b.Lo.Lo}" : $"ld de,({b.Lo.Lo})");
        if (subtract)
        {
            L("or a");
            L("sbc hl,de");
        }
        else
        {
            L("add hl,de");
        }

        L($"ld ({dst.Lo.Lo}),hl");
        L(a.Hi.IsImmediate ? $"ld hl,{a.Hi.Lo}" : $"ld hl,({a.Hi.Lo})");
        L(b.Hi.IsImmediate ? $"ld de,{b.Hi.Lo}" : $"ld de,({b.Hi.Lo})");
        L(subtract ? "sbc hl,de" : "adc hl,de");
        L($"ld ({dst.Hi.Lo}),hl");
        if (save)
        {
            L("pop de");
        }

        return true;
    }

    /// <summary>S xor V po odejmowaniu: przy przepełnieniu (P/V = 1) odwraca bit znaku A, wtedy S niesie wynik.</summary>
    /// <param name="less">Skok przy <c>x &lt; y</c> (<c>jp m</c>), inaczej przy <c>x &gt;= y</c> (<c>jp p</c>).</param>
    /// <param name="label">Etykieta.</param>
    public void JumpIfSigned(bool less, string label)
    {
        string skip = LocalLabel();
        L($"jp po,{skip}");
        L("xor 128");
        L($"{skip}:");
        L(less ? $"jp m,{label}" : $"jp p,{label}");
    }

    /// <summary>Skoki po porównaniu ze stałą bez trampoliny. Przepełnienie (V=1) pcha też znak (S=1 przy
    /// C &lt; 0, S=0 przy C &gt; 0), więc pojedynczy skok po znaku kłamie — gdy V rozstrzyga na spadek,
    /// materializuję pustą etykietę else (0 B). Układ: <c>Lt</c> z C &gt; 0 to <c>jp pe,T; jp m,T</c>,
    /// z C &lt; 0 to <c>jp pe,E; jp m,T; E:</c>, z C = 0 (V niemożliwe) samo <c>jp m,T</c>; <c>Ge</c> dualnie.</summary>
    /// <param name="less"><c>x &lt; C</c> albo <c>x &gt;= C</c>.</param>
    /// <param name="constant">Stała ze znakiem.</param>
    /// <param name="target">Etykieta gałęzi prawdy (spadek to fałsz).</param>
    /// <returns>Zawsze <see langword="true"/>.</returns>
    public bool TryBranchSignedConst(bool less, int constant, string target)
    {
        string sign = less ? $"jp m,{target}" : $"jp p,{target}";
        if (constant == 0)
        {
            L(sign);
            return true;
        }

        bool overMeansTrue = less == (constant > 0);
        if (overMeansTrue)
        {
            L($"jp pe,{target}");
            L(sign);
            return true;
        }

        string skip = LocalLabel();
        L($"jp pe,{skip}");
        L(sign);
        L($"{skip}:");
        return true;
    }

    /// <summary>Skok na znaku i zerze słowa bez odejmowania: wartość do HL, potem bit 7 albo OR.</summary>
    /// <param name="value">Słowo 2-bajtowe (nie natychmiastowe).</param>
    /// <param name="cond">Warunek w postaci <c>wartość cond 0</c>.</param>
    /// <param name="target">Etykieta docelowa.</param>
    /// <returns><see langword="true"/>, gdy sekwencja została wyemitowana.</returns>
    public override bool TryBranchZeroSigned(Word value, Ir.Cond cond, string target)
    {
        if (value.IsImmediate || !Usable(value) || cond is not (Ir.Cond.Lt or Ir.Cond.Ge or Ir.Cond.Le or Ir.Cond.Gt))
        {
            return false;
        }

        LoadPair("hl", value);
        switch (cond)
        {
            case Ir.Cond.Lt:
                L("bit 7,h");
                L($"jp nz,{target}");
                break;
            case Ir.Cond.Ge:
                L("bit 7,h");
                L($"jp z,{target}");
                break;
            case Ir.Cond.Le:
                L("ld a,h");
                L("or l");
                L($"jp z,{target}");
                L("bit 7,h");
                L($"jp nz,{target}");
                break;
            default:
                string skip = LocalLabel();
                L("ld a,h");
                L("or l");
                L($"jp z,{skip}");
                L("bit 7,h");
                L($"jp z,{target}");
                L($"{skip}:");
                break;
        }

        return true;
    }

    public void PushPair(string pair) => L($"push {pair}");

    public void PopPair(string pair) => L($"pop {pair}");

    public override void PushA() => L("push af");

    public override void PopA() => L("pop af");

    public override void Call(string symbol) => L($"call {symbol}");

    public override void CallIndirect(string cell)
    {
        LoadPair("hl", new Word(false, cell, cell + "+1"));
        L("call __callhl");
    }

    public void TailCallIndirect(string cell)
    {
        LoadPair("hl", new Word(false, cell, cell + "+1"));
        L("jp (hl)");
    }

    public override void Return() => L("ret");

    public override void PtrSetup(string cell, int offset, bool mustCopy = false)
    {
        LoadPair("hl", new Word(false, cell, cell + "+1"));
        if (offset is > 0 and <= 3)
        {
            for (int i = 0; i < offset; i++)
            {
                L("inc hl");
            }
        }
        else if (offset > 3)
        {
            // para pomocnicza tylko wolna (DE, potem BC); obie z komórkami: DE przechowane na stosie
            string? pair = Scratch();
            if (pair is null)
            {
                L("push de");
            }

            L($"ld {pair ?? "de"},{offset}");
            L($"add hl,{pair ?? "de"}");
            if (pair is null)
            {
                L("pop de");
            }
        }

        _position = 0;
    }

    public override void PtrLoad(int index)
    {
        Advance(index);
        L("ld a,(hl)");
    }

    public override void PtrStore(int index)
    {
        Advance(index);
        L("ld (hl),a");
    }

    public override string Crt0()
    {
        var text = new StringBuilder();
        foreach (string symbol in CrtCells())
        {
            text.AppendLine($"GLOBAL {symbol}");
        }

        text.AppendLine("GLOBAL __bss_start");
        text.AppendLine("GLOBAL cc_retbuf");
        text.AppendLine("GLOBAL cc_rethi");
        text.AppendLine("GLOBAL __callhl");
        text.AppendLine("EXTERN main");
        text.AppendLine("EXTERN __bss_end");
        text.AppendLine("EXTERN __init_start");
        text.AppendLine("EXTERN __init_end");
        text.AppendLine("SEGMENT \"CODE\"");
        text.AppendLine("""
            ld sp,1000h
            ld hl,__bss_start
            __crt_z: ld de,__bss_end
            ld a,l
            cp e
            jr nz,__crt_z1
            ld a,h
            cp d
            jr z,__crt_zd
            __crt_z1: ld (hl),0
            inc hl
            jr __crt_z
            __crt_zd: ld hl,__init_start
            __crt_i: ld de,__init_end
            ld a,l
            cp e
            jr nz,__crt_i1
            ld a,h
            cp d
            jr z,__crt_id
            __crt_i1: ld e,(hl)
            inc hl
            ld d,(hl)
            inc hl
            push hl
            ex de,hl
            call __callhl
            pop hl
            jr __crt_i
            __crt_id: call main
            ld (cc_ret),hl
            halt
            __callhl: jp (hl)
            """);
        text.AppendLine("SEGMENT \"BSS\"");
        text.AppendLine("__bss_start: DS 1");
        foreach (string symbol in CrtCells())
        {
            text.AppendLine($"{symbol}: DS 1");
        }

        text.AppendLine("SEGMENT \"DATA\"");
        text.AppendLine("cc_retbuf: DS 64");
        text.AppendLine("cc_rethi: DS 2");

        return text.ToString();
    }

    /// <summary>Relaksacja <c>jp</c> → <c>jr</c> (bezwarunkowe i z/nz/c/nc; <c>jr</c> nie ma po/pe/p/m). Zasięg <c>jr</c>:
    /// cel − adres skoku w −126..+129 (przesunięcie −128..127 liczone od adresu po 2-bajtowym skoku).</summary>
    /// <param name="text">Tekst funkcji.</param>
    /// <returns>Tekst po relaksacji.</returns>
    protected override string Relax(string text)
    {
        List<AsmInsn> lines = [.. AsmInsn.ParseAll(AsmPeephole.TidyZ80(text))];
        lines = BranchRelaxer.SkipOverJump(lines, NegateCondition);
        lines = BranchRelaxer.DropJumpToNext(lines);
        lines = BranchRelaxer.Shorten(lines, Size, Shorten, -126, 129);
        return AsmInsn.EmitAll(lines);
    }

    /// <summary>Negacja warunku skoku Z80 (te same 8 co w starym tekście).</summary>
    /// <param name="condition">Warunek (<c>nz</c>, <c>z</c>, <c>nc</c>, <c>c</c>, <c>po</c>, <c>pe</c>, <c>p</c>, <c>m</c>).</param>
    /// <returns>Warunek przeciwny albo null.</returns>
    private static string? NegateCondition(string condition) => condition switch
    {
        "nz" => "z",
        "z" => "nz",
        "nc" => "c",
        "c" => "nc",
        "po" => "pe",
        "pe" => "po",
        "p" => "m",
        "m" => "p",
        _ => null,
    };

    /// <summary>Krótka postać skoku Z80 (<c>jp [cc,]cel</c> → <c>jr [cc,]cel</c>) albo null.</summary>
    /// <param name="line">Linia.</param>
    /// <returns>Krótka linia i cel.</returns>
    private static (AsmInsn Short, string Target)? Shorten(AsmInsn line)
    {
        if (line.Label is not null || line.Comment is not null || line.Mnemonic != "jp" || line.Gap.Length == 0)
        {
            return null;
        }

        string ops = line.Operands.TrimEnd();
        string cond = string.Empty;
        string target = ops;
        int comma = ops.IndexOf(',');
        if (comma >= 0)
        {
            cond = ops[..comma];
            target = ops[(comma + 1)..].TrimStart();
            if (cond is not ("nz" or "z" or "nc" or "c"))
            {
                return null;
            }

            cond += ",";
        }

        if (!IsJumpTarget(target))
        {
            return null;
        }

        return (new(string.Empty, null, string.Empty, "jr", " ", cond + target, null, line.Newline), target);
    }

    /// <summary>Cel skoku (litera, <c>_.$</c>, potem to samo i cyfry).</summary>
    private static bool IsJumpTarget(string target) =>
        target.Length > 0 && (char.IsLetter(target[0]) || target[0] is '_' or '.' or '$')
        && target.All(static c => char.IsLetterOrDigit(c) || c is '_' or '.' or '$');

    /// <summary>Rozmiar instrukcji w bajtach dla form, które emituje selektor; nieznana forma liczy się jako 4 B (najdłuższa
    /// bez IX/IY), bo zawyżenie tylko osłabia relaksację, a zaniżenie dałoby <c>jr</c> poza zasięgiem. Liczy na rozłożonej
    /// linii (bez ponownego parsowania tekstu).</summary>
    /// <param name="ins">Linia.</param>
    /// <returns>Rozmiar w bajtach.</returns>
    private static int Size(AsmInsn ins)
    {
        string op = ins.Mnemonic?.ToLowerInvariant() ?? string.Empty;
        string[] args = ins.Mnemonic is null ? [] : ins.Operands.ToLowerInvariant().Replace(" ", string.Empty, StringComparison.Ordinal).Split(',');
        string last = args.Length == 0 ? string.Empty : args[^1];
        return op switch
        {
            "global" or "extern" => 0,
            "ret" or "halt" or "nop" or "exx" => 1,
            "push" or "pop" => last.StartsWith('i') ? 2 : 1,
            "ex" => args[0] is "de" or "af" ? 1 : 2,
            "jr" or "djnz" => 2,
            "jp" => last == "(hl)" ? 1 : 3,
            "call" => 3,
            "sla" or "sra" or "srl" or "sll" or "rl" or "rr" or "rlc" or "rrc" or "bit" or "set" or "res" => Reg8(last) ? 2 : 4,
            "inc" or "dec" => (Reg8(last) || Pair(last)) ? 1 : 4,
            "add" when args.Length == 2 && args[0] == "hl" => 1,
            "adc" or "sbc" when args.Length == 2 && args[0] == "hl" => 2,
            "add" or "adc" or "sub" or "sbc" or "and" or "or" or "xor" or "cp" => Reg8(last) ? 1 : (Paren(last) ? 4 : 2),
            "ld" when args.Length == 2 => LoadSize(args[0], args[1]),
            _ => 4,
        };
    }

    private static int LoadSize(string dst, string src)
    {
        if (Reg8(dst) && Reg8(src))
        {
            return 1;
        }

        if ((dst == "a" && src is "(bc)" or "(de)") || (src == "a" && dst is "(bc)" or "(de)") || (dst == "sp" && src == "hl"))
        {
            return 1;
        }

        if (Reg8(dst) && !Paren(src))
        {
            return 2;
        }

        if ((dst == "a" && Paren(src)) || (src == "a" && Paren(dst)) || (dst == "hl" && (Paren(src) || !Reg8(src))) || (src == "hl" && Paren(dst)))
        {
            return 3;
        }

        return (Pair(dst) && !Paren(src)) ? 3 : 4;
    }

    private static bool Reg8(string operand) => operand is "a" or "b" or "c" or "d" or "e" or "h" or "l" or "(hl)";

    private static bool Pair(string operand) => operand is "bc" or "de" or "hl" or "sp";

    private static bool Paren(string operand) => operand.StartsWith('(');

    private static IEnumerable<string> CrtCells()
    {
        for (int arg = 1; arg <= TypeChecker.MaxArgs; arg++)
        {
            yield return $"cc_arg{arg}";
            yield return $"cc_arg{arg}_h";
        }

        yield return "cc_ret";
        yield return "cc_ret_h";
        yield return "cc_t0";
        yield return "cc_t1";
    }

    private void Operate(string mnemonic, Octet value)
    {
        if (mnemonic == "cp " && value is { IsImmediate: true, Text: "0" })
        {
            // A - 0: te same Z i C (zero), 1 B krócej. V (parzystość zamiast 0) i N (0 zamiast 1)
            // inne niż CP — wolno, bo konsumenci (Eq/Ne) czytają tylko Z/C (test PrimEffectsFuzzTests).
            L("or a");
            return;
        }

        if (value.IsImmediate || IsRegister(value.Text))
        {
            L($"{mnemonic}{(value.IsImmediate ? value.Text : Resolve(value.Text))}");
            return;
        }

        L($"ld hl,{value.Text}");
        L($"{mnemonic}(hl)");
    }

    /// <summary>Operand bajtu: rejestr albo <c>(adres)</c>.</summary>
    private string Operand(string address) => Resolve(address) ?? $"({address})";

    /// <summary>Słowo do pary rejestrów (<c>hl</c>, <c>de</c>, <c>bc</c>); nie zmienia flag. Z rejestrów najpierw młodszy bajt: młodszy
    /// rejestr celu (C/E/L) nigdy nie jest starszym rejestrem źródła (B/D).</summary>
    private void LoadPair(string pair, Word word)
    {
        if (word.IsImmediate)
        {
            L($"ld {pair},{word.Lo}");
        }
        else if (InRegisters(word))
        {
            Move(pair[1..], Resolve(word.Lo)!);
            Move(pair[..1], Resolve(word.Hi)!);
        }
        else
        {
            L($"ld {pair},({word.Lo})");
        }
    }

    /// <summary>Para rejestrów do słowa (rejestry albo pamięć); nie zmienia flag.</summary>
    private void StorePair(string pair, Word word)
    {
        if (InRegisters(word))
        {
            Move(Resolve(word.Lo)!, pair[1..]);
            Move(Resolve(word.Hi)!, pair[..1]);
        }
        else
        {
            L($"ld ({word.Lo}),{pair}");
        }
    }

    private void Move(string dst, string src)
    {
        if (dst != src)
        {
            L($"ld {dst},{src}");
        }
    }

    private void Advance(int index)
    {
        while (_position < index)
        {
            L("inc hl");
            _position++;
        }
    }
}
