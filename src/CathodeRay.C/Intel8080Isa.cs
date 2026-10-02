using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CathodeRay.C;

/// <summary>Prymitywy Intel 8080 (mnemoniki Intel): A jako akumulator, HL jako rejestr adresowy (<c>LXI H,adres; ADD M</c>,
/// wskaźniki przez <c>LHLD</c>), bez rejestrów IX/IY i bez instrukcji Z80. Komórki mogą leżeć w B, C, D, E (pary BC/DE) jak na
/// <see cref="Z80Isa"/>: <c>mov a,c</c>, <c>add c</c>, <c>inr c</c>, <c>inx b</c>; słowo z pamięci do pary przez <c>lhld</c> i
/// <c>mov c,l; mov b,h</c> (8080 nie ma <c>ld bc,(nn)</c>).</summary>
internal sealed partial class Intel8080Isa : ByteIsa
{
    private static readonly HashSet<string> ReservedNames = new(
        ["A", "B", "C", "D", "E", "H", "L", "M", "SP", "PSW", "LOW", "HIGH", "MOD", "SHL", "SHR", "AND", "OR", "XOR", "NOT", "EQ", "NE", "LT", "LE", "GT", "GE"],
        StringComparer.OrdinalIgnoreCase);

    private int _position;

    public override IEnumerable<string> IndirectSymbols => ["__callhl"];

    public override IReadOnlyList<string> CellRegisters { get; } = ["c", "b", "e", "d"];

    public override IReadOnlyList<string> CellPairs { get; } = ["bc", "de"];

    public override bool ReturnsInResultReg => true;

    /// <summary>Cel obsługuje wywołanie ogonowe.</summary>
    public override bool SupportsTailCall => true;

    protected override IReadOnlySet<string> Reserved => ReservedNames;

    /// <summary>Nazwa CPU w <see cref="CpuModels"/>.</summary>
    protected override string CpuName => "8080";

    public override string Segment(string name) => $"SEGMENT \"{name}\"";

    public override string Global(string sym) => $"GLOBAL {sym}";

    public override string Extern(string sym) => $"EXTERN {sym}";

    public override string Bytes(IEnumerable<int> values) => $"DB {string.Join(", ", values.Select(static v => v.ToString(CultureInfo.InvariantCulture)))}";

    public override string Word(string expression) => $"DW {expression}";

    public override string Reserve(int size) => $"DS {size}";

    public override void LoadA(Octet value) =>
        L(value.IsImmediate ? $"mvi a,{value.Text}" : Resolve(value.Text) is { } register ? $"mov a,{register}" : $"lda {value.Text}");

    public override void StoreA(string address) => L(Resolve(address) is { } register ? $"mov {register},a" : $"sta {address}");

    public override void Alu(ByteAlu op, Octet value, bool first)
    {
        (string immediate, string memory) = op switch
        {
            ByteAlu.Add => first ? ("adi", "add") : ("aci", "adc"),
            ByteAlu.Sub => first ? ("sui", "sub") : ("sbi", "sbb"),
            ByteAlu.And => ("ani", "ana"),
            ByteAlu.Or => ("ori", "ora"),
            _ => ("xri", "xra"),
        };
        Operate(immediate, memory, value);
    }

    public override void Cmp(Octet value) => Operate("cpi", "cmp", value);

    public override void ShlA(bool first) => L(first ? "add a" : "ral");

    public override void ShrA(bool first)
    {
        if (first)
        {
            L("ora a");
        }

        L("rar");
    }

    public override void Jump(string label) => L($"jmp {label}");

    public override void JumpIf(ByteFlag flag, string label) => L(flag switch
    {
        ByteFlag.Zero => $"jz {label}",
        ByteFlag.NotZero => $"jnz {label}",
        ByteFlag.Borrow => $"jc {label}",
        _ => $"jnc {label}",
    });

    public override bool TryStep(IReadOnlyList<string> bytes, bool increment)
    {
        string op = increment ? "inr" : "dcr";
        if (bytes.Any(IsRegister))
        {
            // rejestr: inr c; para: inx b (bez flag, kontrakt pozwala); inny układ przez łańcuch ADD/SUB w selektorze
            string? register = bytes.Count == 1 ? Resolve(bytes[0]) : PairOf(new Word(false, bytes[0], bytes[1]))?[..1];
            if (register is not null)
            {
                L(bytes.Count == 1 ? $"{op} {register}" : $"{(increment ? "inx" : "dcx")} {register}");
            }

            return register is not null;
        }

        L($"lxi h,{bytes[0]}");
        if (bytes.Count == 1)
        {
            L($"{op} m");
            return true;
        }

        string skip = LocalLabel();
        if (increment)
        {
            L("inr m");
            L($"jnz {skip}");
            L("inx h");
            L("inr m");
            L($"{skip}:");
            return true;
        }

        L("mov a,m");
        L("dcr m");
        L("ora a");
        L($"jnz {skip}");
        L("inx h");
        L("dcr m");
        L($"{skip}:");
        return true;
    }

    /// <summary>Słowo &lt;&lt; 1: załaduj HL, <c>dad h</c>, odłóż (wynik zostaje w HL).</summary>
    /// <param name="dst">Cel.</param>
    /// <param name="src">Źródło.</param>
    /// <returns>Wynik jawny (<see cref="WordResult"/>).</returns>
    public override WordResult TryShlWord1(Word dst, Word src)
    {
        if (dst.IsImmediate || !Usable(dst) || !Usable(src))
        {
            return new(false, false);
        }

        LoadPair("hl", src);
        L("dad h");
        StorePair("hl", dst);
        return new(true, true);
    }

    /// <summary>Kopia słowa przez HL (<c>lxi h,wartość</c> albo <c>lhld adres</c>; <c>shld adres</c>), gdy bajty obu stron leżą obok
    /// siebie; cel w parze BC/DE: <c>lxi b,wartość</c>, <c>mov c,e; mov b,d</c> albo <c>lhld adres; mov c,l; mov b,h</c>.</summary>
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
        else
        {
            LoadPair("hl", src);
            StorePair("hl", dst);
        }

        return true;
    }

    /// <summary>Słowo z pamięci na stos: <c>lhld n; push h</c>.</summary>
    /// <param name="word">Słowo.</param>
    /// <returns><see langword="false"/>, gdy bajty nie są sąsiednie w pamięci.</returns>
    public override bool TryPushWord(Word word)
    {
        if (!InMemoryWord(word))
        {
            return false;
        }

        L($"lhld {word.Lo}");
        L("push h");
        return true;
    }

    /// <summary>Słowo ze stosu do pamięci: <c>pop h; shld n</c>; przy wyniku w HL chowany na czas zapisu w DE (w epilogu wolne):
    /// <c>xchg; pop h; shld n; xchg</c>.</summary>
    /// <param name="word">Słowo.</param>
    /// <param name="keepResult">HL niesie wynik.</param>
    /// <returns><see langword="false"/>, gdy bajty nie są sąsiednie w pamięci.</returns>
    public override bool TryPopWord(Word word, bool keepResult)
    {
        if (!InMemoryWord(word))
        {
            return false;
        }

        if (keepResult)
        {
            L("xchg");
        }

        L("pop h");
        L($"shld {word.Lo}");
        if (keepResult)
        {
            L("xchg");
        }

        return true;
    }

    /// <summary>HL ← słowo (<c>lxi h,nn</c>, <c>lhld n</c>, <c>mov l,c; mov h,b</c>, z DE <c>xchg</c>).</summary>
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
            L("xchg");
        }
        else
        {
            LoadPair("hl", value);
        }

        return true;
    }

    /// <summary>Słowo ← HL (<c>shld n</c>, <c>mov c,l; mov b,h</c>, do DE <c>xchg</c>).</summary>
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
            L("xchg");
        }
        else
        {
            StorePair("hl", dst);
        }

        return true;
    }

    public override void ResultByteFromA(int index) => L(index == 0 ? "mov l,a" : "mov h,a");

    public override void ResultByteToA(int index) => L(index == 0 ? "mov a,l" : "mov a,h");

    public override void PushPair(string pair) => L($"push {pair[..1]}");

    public override void PopPair(string pair) => L($"pop {pair[..1]}");

    public override void PushA() => L("push psw");

    public override void PopA() => L("pop psw");

    public override void Call(string symbol) => L($"call {symbol}");

    public override void CallIndirect(string cell)
    {
        LoadPair("hl", new Word(false, cell, cell + "+1"));
        L("call __callhl");
    }

    public override void TailCallIndirect(string cell)
    {
        LoadPair("hl", new Word(false, cell, cell + "+1"));
        L("pchl");
    }

    /// <summary>Skok na znaku i zerze słowa bez odejmowania: wartość do HL, potem bit 7 przez <c>ani</c> albo OR.</summary>
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
                L("mov a,h");
                L("ani 128");
                L($"jnz {target}");
                break;
            case Ir.Cond.Ge:
                L("mov a,h");
                L("ani 128");
                L($"jz {target}");
                break;
            case Ir.Cond.Le:
                L("mov a,h");
                L("ora l");
                L($"jz {target}");
                L("mov a,h");
                L("ani 128");
                L($"jnz {target}");
                break;
            default:
                string skip = LocalLabel();
                L("mov a,h");
                L("ora l");
                L($"jz {skip}");
                L("mov a,h");
                L("ani 128");
                L($"jz {target}");
                L($"{skip}:");
                break;
        }

        return true;
    }

    public override void Return() => L("ret");

    public override void PtrSetup(string cell, int offset, bool mustCopy = false)
    {
        LoadPair("hl", new Word(false, cell, cell + "+1"));
        if (offset is > 0 and <= 3)
        {
            for (int i = 0; i < offset; i++)
            {
                L("inx h");
            }
        }
        else if (offset > 3)
        {
            // para pomocnicza tylko wolna (DE, potem BC); obie z komórkami: DE przechowane na stosie
            string? pair = Scratch();
            if (pair is null)
            {
                L("push d");
            }

            L($"lxi {pair?[..1] ?? "d"},{offset}");
            L($"dad {pair?[..1] ?? "d"}");
            if (pair is null)
            {
                L("pop d");
            }
        }

        _position = 0;
    }

    public override void PtrLoad(int index)
    {
        Advance(index);
        L("mov a,m");
    }

    public override void PtrStore(int index)
    {
        Advance(index);
        L("mov m,a");
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
            lxi sp,1000h
            lxi h,__bss_start
            __crt_z: lxi d,__bss_end
            mov a,l
            cmp e
            jnz __crt_z1
            mov a,h
            cmp d
            jz __crt_zd
            __crt_z1: mvi m,0
            inx h
            jmp __crt_z
            __crt_zd: lxi h,__init_start
            __crt_i: lxi d,__init_end
            mov a,l
            cmp e
            jnz __crt_i1
            mov a,h
            cmp d
            jz __crt_id
            __crt_i1: mov e,m
            inx h
            mov d,m
            inx h
            push h
            xchg
            call __callhl
            pop h
            jmp __crt_i
            __crt_id: call main
            shld cc_ret
            hlt
            __callhl: pchl
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

    /// <summary>Usuwa skoki do etykiety tuż za nimi (<see cref="BranchRelaxer.DropJumpToNext"/>); 8080 nie ma krótkich skoków.
    /// Usuwa też zbędne przeniesienia bajtów (BC/DE ↔ HL po kopii).</summary>
    /// <param name="text">Tekst funkcji.</param>
    /// <returns>Tekst po zmianie.</returns>
    protected override string Relax(string text) => BranchRelaxer.DropJumpToNext(AsmPeephole.Tidy8080(text));

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

    private void Operate(string immediate, string memory, Octet value)
    {
        if (immediate == "cpi" && value is { IsImmediate: true, Text: "0" })
        {
            // A - 0: te same Z i C (zero), 1 B krócej
            L("ora a");
            return;
        }

        if (value.IsImmediate)
        {
            L($"{immediate} {value.Text}");
            return;
        }

        if (Resolve(value.Text) is { } register)
        {
            L($"{memory} {register}");
            return;
        }

        L($"lxi h,{value.Text}");
        L($"{memory} m");
    }

    /// <summary>Słowo do pary rejestrów (<c>hl</c>, <c>de</c>, <c>bc</c>); nie zmienia flag ani A. Z pamięci do BC/DE przez HL
    /// (<c>lhld</c>, potem <c>mov</c>), więc niszczy HL. Z rejestrów najpierw młodszy bajt.</summary>
    private void LoadPair(string pair, Word word)
    {
        if (word.IsImmediate)
        {
            L($"lxi {pair[..1]},{word.Lo}");
        }
        else if (InRegisters(word))
        {
            Move(pair[1..], Resolve(word.Lo)!);
            Move(pair[..1], Resolve(word.Hi)!);
        }
        else
        {
            L($"lhld {word.Lo}");
            Move(pair[1..], "l");
            Move(pair[..1], "h");
        }
    }

    /// <summary>Para rejestrów do słowa (rejestry albo pamięć przez <c>shld</c>); nie zmienia flag ani A.</summary>
    private void StorePair(string pair, Word word)
    {
        if (InRegisters(word))
        {
            Move(Resolve(word.Lo)!, pair[1..]);
            Move(Resolve(word.Hi)!, pair[..1]);
            return;
        }

        Move("l", pair[1..]);
        Move("h", pair[..1]);
        L($"shld {word.Lo}");
    }

    private void Move(string dst, string src)
    {
        if (dst != src)
        {
            L($"mov {dst},{src}");
        }
    }

    private void Advance(int index)
    {
        while (_position < index)
        {
            L("inx h");
            _position++;
        }
    }
}
