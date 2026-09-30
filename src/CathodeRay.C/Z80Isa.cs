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

    /// <summary>Adres bajtu komórki (tekst jak z <see cref="ByteIsa.Loc"/>) → rejestr <c>b</c>, <c>c</c>, <c>d</c> albo <c>e</c>.</summary>
    private readonly Dictionary<string, string> _registers = new(StringComparer.Ordinal);

    private int _position;

    public override IEnumerable<string> IndirectSymbols => ["__callhl"];

    public override bool HasOverflowFlag => true;

    protected override IReadOnlySet<string> Reserved => ReservedNames;

    public override string Segment(string name) => $"SEGMENT \"{name}\"";

    public override string Global(string sym) => $"GLOBAL {sym}";

    public override string Extern(string sym) => $"EXTERN {sym}";

    public override string Bytes(IEnumerable<int> values) => $"DB {string.Join(", ", values.Select(static v => v.ToString(CultureInfo.InvariantCulture)))}";

    public override string Word(string expression) => $"DW {expression}";

    public override string Reserve(int size) => $"DS {size}";

    /// <summary>Przypisuje komórkom rejestry; selektor dalej widzi nazwy symboliczne, a ISA tłumaczy operand przy emisji (jak strona
    /// zerowa w <see cref="Mos6502Isa"/>). Komórka 1-bajtowa dostaje <c>b</c>, <c>c</c>, <c>d</c> albo <c>e</c>, 2-bajtowa dwa rejestry,
    /// starszy pierwszy: młodszy <c>c</c>/<c>e</c>, starszy <c>b</c>/<c>d</c> (np. <c>bc</c>, <c>de</c>).</summary>
    /// <param name="cells">Symbol komórki z kodu pośredniego → rejestr(y).</param>
    public void AssignRegisters(IReadOnlyDictionary<string, string> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        foreach ((string sym, string registers) in cells)
        {
            bool valid = registers.Length == 1
                ? registers is "b" or "c" or "d" or "e"
                : registers.Length == 2 && registers[0] is 'b' or 'd' && registers[1] is 'c' or 'e';
            if (!valid)
            {
                throw new ArgumentException($"niedozwolone rejestry '{registers}' dla {sym}", nameof(cells));
            }

            _registers[Sym(sym)] = registers[^1..];
            if (registers.Length == 2)
            {
                _registers[At(sym, 1)] = registers[..1];
            }
        }
    }

    public override bool IsRegister(string address) => _registers.ContainsKey(address);

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

        // prawy operand: jego własna para BC/DE albo para pomocnicza bez komórek; bez wolnej pary łańcuch przez A w selektorze
        bool steps = constant is { } c && (c <= 3 || c >= 0xFFFD);
        string? rhs = (constant is null ? PairOf(b) : null) ?? Scratch();
        if (rhs is null && !steps)
        {
            return false;
        }

        LoadPair("hl", a);
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
        if (dst.Lo.IsImmediate || dst.Hi.IsImmediate || Scratch() != "de"
            || memory.Any(w => !w.IsImmediate && (!Adjacent(w) || IsRegister(w.Lo) || IsRegister(w.Hi))))
        {
            return false;
        }

        if (!subtract && a.Lo.IsImmediate && a.Hi.IsImmediate)
        {
            (a, b) = (b, a);
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
            L($"ld de,{offset}");
            L("add hl,de");
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
            jp nz,__crt_z1
            ld a,h
            cp d
            jp z,__crt_zd
            __crt_z1: ld (hl),0
            inc hl
            jp __crt_z
            __crt_zd: ld hl,__init_start
            __crt_i: ld de,__init_end
            ld a,l
            cp e
            jp nz,__crt_i1
            ld a,h
            cp d
            jp z,__crt_id
            __crt_i1: ld e,(hl)
            inc hl
            ld d,(hl)
            inc hl
            push hl
            ex de,hl
            call __callhl
            pop hl
            jp __crt_i
            __crt_id: call main
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
    protected override string Relax(string text) => BranchRelaxer.Shorten(text, Size, Shorten, -126, 129);

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

    [GeneratedRegex(@"^\s*jp\s+((?:nz|z|nc|c),)?\s*([A-Za-z_.$][\w.$]*)\s*$")]
    private static partial Regex LongJump();

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
        if (value.IsImmediate || IsRegister(value.Text))
        {
            L($"{mnemonic}{(value.IsImmediate ? value.Text : Resolve(value.Text))}");
            return;
        }

        L($"ld hl,{value.Text}");
        L($"{mnemonic}(hl)");
    }

    /// <summary>Rejestr przypisany bajtowi komórki albo <see langword="null"/> (pamięć).</summary>
    private string? Resolve(string address) => _registers.GetValueOrDefault(address);

    /// <summary>Operand bajtu: rejestr albo <c>(adres)</c>.</summary>
    private string Operand(string address) => Resolve(address) ?? $"({address})";

    /// <summary>Oba bajty słowa w rejestrach.</summary>
    private bool InRegisters(Word word) => !word.IsImmediate && IsRegister(word.Lo) && IsRegister(word.Hi);

    /// <summary>Słowo, które przeniosą <see cref="LoadPair"/> i <see cref="StorePair"/>: stała, oba bajty w rejestrach albo oba w pamięci obok siebie.</summary>
    private bool Usable(Word word) => word.IsImmediate || InRegisters(word) || (!IsRegister(word.Lo) && !IsRegister(word.Hi) && Adjacent(word));

    /// <summary>Para <c>bc</c>/<c>de</c>, gdy słowo leży w niej w całości (młodszy bajt w C/E).</summary>
    private string? PairOf(Word word) => InRegisters(word) && (_registers[word.Hi] + _registers[word.Lo]) is "bc" or "de" ? _registers[word.Hi] + _registers[word.Lo] : null;

    /// <summary>Para pomocnicza bez rejestrów przypisanych komórkom: DE, potem BC; <see langword="null"/>, gdy obie zajęte.</summary>
    private string? Scratch() => new[] { "de", "bc" }.FirstOrDefault(p => !_registers.ContainsValue(p[..1]) && !_registers.ContainsValue(p[1..]));

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
            Move(pair[1..], _registers[word.Lo]);
            Move(pair[..1], _registers[word.Hi]);
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
            Move(_registers[word.Lo], pair[1..]);
            Move(_registers[word.Hi], pair[..1]);
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
