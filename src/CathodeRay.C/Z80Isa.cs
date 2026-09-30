using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CathodeRay.C;

/// <summary>Prymitywy Z80 (składnia Zilog): A jako akumulator, HL jako rejestr adresowy (operandy pamięciowe ALU przez
/// <c>LD HL,adres; op A,(HL)</c>, wskaźniki przez <c>LD HL,(komórka)</c>), zapis/odczyt komórek przez <c>LD A,(adres)</c>.</summary>
internal sealed partial class Z80Isa : ByteIsa
{
    private static readonly HashSet<string> ReservedNames = new(
        ["A", "B", "C", "D", "E", "H", "L", "I", "R", "AF", "BC", "DE", "HL", "SP", "IX", "IY", "IXH", "IXL", "IYH", "IYL", "NZ", "Z", "NC", "PO", "PE", "P", "M", "LOW", "HIGH", "MOD", "SHL", "SHR", "AND", "OR", "XOR", "NOT"],
        StringComparer.OrdinalIgnoreCase);

    private int _position;

    /// <summary>Rejestry, które prymitywy niszczą niezależnie od mapy rejestrów: A i HL (rejestr adresowy); wszystko niszczą
    /// tylko wołania (<see cref="Call"/>, <see cref="CallIndirect"/>). Rejestry B, C, D, E przypisane komórkom przez
    /// <see cref="ByteIsa.AssignRegisters"/> prymitywy zachowują: parę pomocniczą (<see cref="TryAddWord"/>, <see cref="TryAddLong"/>,
    /// <see cref="PtrSetup"/> z przesunięciem &gt; 3) biorą tylko wolną, inaczej <c>push de</c>/<c>pop de</c> albo łańcuch przez A.
    /// Alokator może więc dać komórce dowolny z B, C, D, E, jeśli nie żyje przez wołanie.</summary>
    public static IReadOnlySet<string> Clobbers { get; } = new HashSet<string>(["a", "h", "l"], StringComparer.Ordinal);

    public override IEnumerable<string> IndirectSymbols => ["__callhl"];

    public override IReadOnlyList<string> CellRegisters { get; } = ["c", "b", "e", "d"];

    public override IReadOnlyList<string> CellPairs { get; } = ["bc", "de"];

    public override bool HasOverflowFlag => true;

    public override bool ReturnsInResultReg => true;

    protected override IReadOnlySet<string> Reserved => ReservedNames;

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
    public override bool TryMoveWord(Word dst, Word src)
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
    public override bool TryPushWord(Word word)
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
    public override bool TryPopWord(Word word, bool keepResult)
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
    public override bool TryMoveToResultReg(Word value)
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
    public override bool TryMoveFromResultReg(Word dst)
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

    public override void ResultByteFromA(int index) => L(index == 0 ? "ld l,a" : "ld h,a");

    public override void ResultByteToA(int index) => L(index == 0 ? "ld a,l" : "ld a,h");

    /// <summary>Dodawanie/odejmowanie przez HL: <c>ld hl,a; add hl,de</c> albo <c>or a; sbc hl,de</c>, stała ±1..3 przez
    /// <c>inc hl</c>/<c>dec hl</c>, odjęcie stałej liczbowej jako dodanie jej przeciwieństwa.</summary>
    /// <param name="dst">Cel.</param>
    /// <param name="a">Lewy operand.</param>
    /// <param name="b">Prawy operand.</param>
    /// <param name="subtract">Odejmowanie.</param>
    /// <returns><see langword="false"/>, gdy bajty nie są sąsiednie albo oba operandy są stałymi.</returns>
    public override bool TryAddWord(Word dst, Word a, Word b, bool subtract)
    {
        if (dst.IsImmediate || (a.IsImmediate && b.IsImmediate) || !Usable(dst) || !Usable(a) || !Usable(b))
        {
            return false;
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
            return false;
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

        StorePair("hl", dst);
        return true;
    }

    /// <summary>32 bity przez HL i DE: <c>add hl,de</c> (albo <c>or a; sbc hl,de</c>) na młodszych połówkach, <c>adc hl,de</c>
    /// (<c>sbc hl,de</c>) na starszych; <c>ld</c> między nimi nie rusza przeniesienia.</summary>
    /// <param name="dst">Cel.</param>
    /// <param name="a">Lewy operand.</param>
    /// <param name="b">Prawy operand.</param>
    /// <param name="subtract">Odejmowanie.</param>
    /// <returns><see langword="false"/>, gdy bajty którejś połówki nie są sąsiednie.</returns>
    public override bool TryAddLong((Word Lo, Word Hi) dst, (Word Lo, Word Hi) a, (Word Lo, Word Hi) b, bool subtract)
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
    public override void JumpIfSigned(bool less, string label)
    {
        string skip = LocalLabel();
        L($"jp po,{skip}");
        L("xor 128");
        L($"{skip}:");
        L(less ? $"jp m,{label}" : $"jp p,{label}");
    }

    public override void PushPair(string pair) => L($"push {pair}");

    public override void PopPair(string pair) => L($"pop {pair}");

    public override void PushA() => L("push af");

    public override void PopA() => L("pop af");

    public override void Call(string symbol) => L($"call {symbol}");

    public override void CallIndirect(string cell)
    {
        LoadPair("hl", new Word(false, cell, cell + "+1"));
        L("call __callhl");
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

    /// <summary>Zmiany w tekście asemblera: usuwa zbędne przeniesienia bajtów (BC/DE ↔ HL po kopii).</summary>
    /// <param name="text">Tekst funkcji.</param>
    /// <returns>Tekst po zmianie.</returns>
    internal static string Tidy(string text)
    {
        text = RedundantBcToHl().Replace(text, "$1");
        text = RedundantDeToHl().Replace(text, "$1");
        return text;
    }

    /// <summary>Relaksacja <c>jp</c> → <c>jr</c> (bezwarunkowe i z/nz/c/nc; <c>jr</c> nie ma po/pe/p/m). Zasięg <c>jr</c>:
    /// cel − adres skoku w −126..+129 (przesunięcie −128..127 liczone od adresu po 2-bajtowym skoku).</summary>
    /// <param name="text">Tekst funkcji.</param>
    /// <returns>Tekst po relaksacji.</returns>
    protected override string Relax(string text) => BranchRelaxer.Shorten(BranchRelaxer.DropJumpToNext(SkipOverJump().Replace(Tidy(text), Invert)), Size, Shorten, -126, 129);

    /// <summary><c>jp cc,X; jp T; X:</c> (skok przez skok z porównania na równość) → <c>jp !cc,T; X:</c>.</summary>
    private static string Invert(Match match)
    {
        string condition = match.Groups[2].Value switch
        {
            "nz" => "z",
            "z" => "nz",
            "nc" => "c",
            "c" => "nc",
            "po" => "pe",
            "pe" => "po",
            "p" => "m",
            _ => "p",
        };
        return $"{match.Groups[1].Value}jp {condition},{match.Groups[4].Value}{Environment.NewLine}{match.Groups[5].Value}";
    }

    private static (string Short, string Target)? Shorten(string line)
    {
        Match jump = LongJump().Match(line);
        return jump.Success ? ($"jr {jump.Groups[1].Value}{jump.Groups[2].Value}", jump.Groups[2].Value) : null;
    }

    /// <summary>Rozmiar instrukcji w bajtach dla form, które emituje selektor; nieznana forma liczy się jako 4 B (najdłuższa
    /// bez IX/IY), bo zawyżenie tylko osłabia relaksację, a zaniżenie dałoby <c>jr</c> poza zasięgiem.</summary>
    private static int Size(string line)
    {
        string text = line.Trim().ToLowerInvariant();
        int space = text.IndexOf(' ', StringComparison.Ordinal);
        string op = space < 0 ? text : text[..space];
        string[] args = space < 0 ? [] : text[(space + 1)..].Replace(" ", string.Empty, StringComparison.Ordinal).Split(',');
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

    [GeneratedRegex(@"(?m)^(\s*ld c,l\r?\n\s*ld b,h\r?\n)\s*ld l,c\r?\n\s*ld h,b\r?\n", RegexOptions.Multiline)]
    private static partial Regex RedundantBcToHl();

    [GeneratedRegex(@"(?m)^(\s*ld e,l\r?\n\s*ld d,h\r?\n)\s*ld l,e\r?\n\s*ld h,d\r?\n", RegexOptions.Multiline)]
    private static partial Regex RedundantDeToHl();

    [GeneratedRegex(@"^\s*jp\s+((?:nz|z|nc|c),)?\s*([A-Za-z_.$][\w.$]*)\s*$")]
    private static partial Regex LongJump();

    [GeneratedRegex(@"^([ \t]*)jp (nz|z|nc|c|po|pe|p|m),([A-Za-z_.$][\w.$]*)\r?\n[ \t]*jp ([A-Za-z_.$][\w.$]*)\r?\n([ \t]*\3:)", RegexOptions.Multiline)]
    private static partial Regex SkipOverJump();

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
            // A - 0: te same Z i C (zero), 1 B krócej
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
