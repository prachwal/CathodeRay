using System.Globalization;
using System.Text;

namespace CathodeRay.C;

/// <summary>Prymitywy Motorola 6800: akumulator A, rejestr indeksowy X do dostępu pośredniego (<c>LDAA n,X</c>), słowa
/// big-endian (komórka 2-bajtowa: starszy bajt pod <c>sym</c>, więc <c>LDX komórka</c> ładuje wskaźnik). Komórki na stronie
/// bezpośredniej (<c>$00xx</c>) dostają przedrostek <c>z:</c>, ale tylko w instrukcjach z trybem bezpośrednim (INC/DEC/TST/CLR/NEG/COM
/// i przesunięcia pamięci go nie mają, więc zostają przy adresie rozszerzonym).</summary>
internal sealed class M6800Isa : ByteIsa
{
    private static readonly HashSet<string> ReservedNames = new(["A", "B", "X"], StringComparer.OrdinalIgnoreCase);

    private static readonly string[] FixedZeroPage =
        ["cc_arg1", "cc_arg1_h", "cc_arg2", "cc_arg2_h", "cc_arg3", "cc_arg3_h", "cc_ret", "cc_ret_h", "cc_t0", "cc_t1"];

    private readonly HashSet<string> _zeroPage = new(FixedZeroPage, StringComparer.Ordinal);

    private int _labels;

    private int _offset;

    public override bool BigEndian => true;

    protected override IReadOnlySet<string> Reserved => ReservedNames;

    public override string Segment(string name) => $".segment \"{name}\"";

    public override string Global(string sym) => $".global {sym}";

    public override string Extern(string sym) => $".extern {sym}";

    public override string Bytes(IEnumerable<int> values) => $".byte {string.Join(", ", values.Select(static v => v.ToString(CultureInfo.InvariantCulture)))}";

    public override string Word(string expression) => $".word {expression}";

    public override string Reserve(int size) => $".res {size}";

    public override string? AddressByte(string expression, int index) => (index == 0 ? "<(" : ">(") + expression + ")";

    /// <summary>Dopisuje komórki modułu przeniesione na stronę bezpośrednią (operandy dostają przedrostek <c>z:</c>).</summary>
    /// <param name="names">Nazwy symboli (bez przesunięć).</param>
    public void AddZeroPage(IEnumerable<string> names) => _zeroPage.UnionWith(names);

    public override void LoadA(Octet value) => L(value.IsImmediate ? $"ldaa #{value.Text}" : $"ldaa {Mem(value.Text)}");

    public override void StoreA(string address) => L($"staa {Mem(address)}");

    public override void Alu(ByteAlu op, Octet value, bool first)
    {
        string operand = value.IsImmediate ? $"#{value.Text}" : Mem(value.Text);
        string mnemonic = op switch
        {
            ByteAlu.Add => first ? "adda" : "adca",
            ByteAlu.Sub => first ? "suba" : "sbca",
            ByteAlu.And => "anda",
            ByteAlu.Or => "oraa",
            _ => "eora",
        };
        L($"{mnemonic} {operand}");
    }

    public override void Cmp(Octet value) => L(value.IsImmediate ? $"cmpa #{value.Text}" : $"cmpa {Mem(value.Text)}");

    public override void ShlA(bool first) => L(first ? "asla" : "rola");

    public override void ShrA(bool first) => L(first ? "lsra" : "rora");

    public override void Jump(string label) => L($"jmp {label}");

    public override void JumpIf(ByteFlag flag, string label)
    {
        string skip = $"__j{++_labels}";
        L(flag switch
        {
            ByteFlag.Zero => $"bne {skip}",
            ByteFlag.NotZero => $"beq {skip}",
            ByteFlag.Borrow => $"bcc {skip}",
            _ => $"bcs {skip}",
        });
        L($"jmp {label}");
        L($"{skip}:");
    }

    public override bool TryStep(IReadOnlyList<string> bytes, bool increment)
    {
        string op = increment ? "inc" : "dec";
        if (bytes.Count == 1)
        {
            L($"{op} {bytes[0]}");
            return true;
        }

        string skip = LocalLabel();
        if (increment)
        {
            L($"inc {bytes[0]}");
            L($"bne {skip}");
            L($"inc {bytes[1]}");
            L($"{skip}:");
            return true;
        }

        L($"tst {bytes[0]}");
        L($"bne {skip}");
        L($"dec {bytes[1]}");
        L($"{skip}:");
        L($"dec {bytes[0]}");
        return true;
    }

    public override void PushA() => L("psha");

    public override void PopA() => L("pula");

    public override void Call(string symbol) => L($"jsr {symbol}");

    public override void CallIndirect(string cell)
    {
        L($"ldx {Mem(cell)}");
        L("jsr 0,x");
    }

    /// <summary>Skok na znaku i zerze słowa bez odejmowania: starszy bajt do A, potem N/Z.
    /// Skoki warunkowe 6800 są krótkie, więc daleki cel idzie trampoliną jak w <see cref="JumpIf"/>.</summary>
    /// <param name="value">Słowo 2-bajtowe (nie natychmiastowe).</param>
    /// <param name="cond">Warunek w postaci <c>wartość cond 0</c>.</param>
    /// <param name="target">Etykieta docelowa.</param>
    /// <returns><see langword="true"/>, gdy sekwencja została wyemitowana.</returns>
    public override bool TryBranchZeroSigned(Word value, Ir.Cond cond, string target)
    {
        if (value.IsImmediate || cond is not (Ir.Cond.Lt or Ir.Cond.Ge or Ir.Cond.Le or Ir.Cond.Gt))
        {
            return false;
        }

        string hi = Mem(value.Hi);
        string lo = Mem(value.Lo);
        switch (cond)
        {
            case Ir.Cond.Lt:
                L($"ldaa {hi}");
                string ltSkip = LocalLabel();
                L($"bpl {ltSkip}");
                L($"jmp {target}");
                L($"{ltSkip}:");
                break;
            case Ir.Cond.Ge:
                L($"ldaa {hi}");
                string geSkip = LocalLabel();
                L($"bmi {geSkip}");
                L($"jmp {target}");
                L($"{geSkip}:");
                break;
            case Ir.Cond.Gt:
                // N testowane przed oraa (oraa nadpisuje A); osobne skipy, bo BranchRelaxer kasuje
                // etykietę z trójki (beq S; jmp T; S:) — współdzielona zawisłaby w bmi.
                L($"ldaa {hi}");
                string gtNeg = LocalLabel();
                L($"bmi {gtNeg}");
                L($"oraa {lo}");
                string gtZero = LocalLabel();
                L($"beq {gtZero}");
                L($"jmp {target}");
                L($"{gtNeg}:");
                L($"{gtZero}:");
                break;
            default:
                L($"ldaa {hi}");
                string leMid = LocalLabel();
                L($"bpl {leMid}");
                L($"jmp {target}");
                L($"{leMid}:");
                L($"oraa {lo}");
                string leSkip = LocalLabel();
                L($"bne {leSkip}");
                L($"jmp {target}");
                L($"{leSkip}:");
                break;
        }

        return true;
    }

    /// <summary>Kopia słowa przez X: <c>ldx źródło</c> (albo <c>ldx #stała</c>), <c>stx cel</c>. Obie strony w pamięci big-endian
    /// (starszy bajt tuż przed młodszym: <c>Lo</c> = <c>Hi+1</c>); para <c>cc_argN</c>/<c>cc_argN_h</c> leży odwrotnie (młodszy pod
    /// <c>cc_argN</c>), więc zostaje przy kopii bajtowej. A się nie zmienia; X i flagi N/Z/V tak (X nie trzyma stanu między prymitywami).</summary>
    /// <param name="dst">Cel.</param>
    /// <param name="src">Źródło.</param>
    /// <returns><see langword="false"/>, gdy któraś strona nie jest słowem big-endian w pamięci.</returns>
    public override bool TryMoveWord(Word dst, Word src)
    {
        if (!InMemory(dst) || !(src.IsImmediate || InMemory(src)))
        {
            return false;
        }

        L(src.IsImmediate ? $"ldx #{src.Lo}" : $"ldx {Mem(src.Hi)}");
        L($"stx {Mem(dst.Hi)}");
        return true;
    }

    public override void Return() => L("rts");

    public override void PtrSetup(string cell, int offset, bool mustCopy = false)
    {
        if (offset <= 254)
        {
            L($"ldx {Mem(cell)}");
            _offset = offset;
            return;
        }

        // Przesunięcie większe niż indeks 8-bitowy: dodajemy je do wskaźnika przez bajty pośrednie (BE: cc_t0 starszy, cc_t1 młodszy).
        L($"ldaa {Mem(cell + "+1")}");
        L($"adda #{offset & 0xFF}");
        L("staa z:cc_t1");
        L($"ldaa {Mem(cell)}");
        L($"adca #{offset >> 8}");
        L("staa z:cc_t0");
        L("ldx z:cc_t0");
        _offset = 0;
    }

    public override void PtrLoad(int index) => L($"ldaa {_offset + index},x");

    public override void PtrStore(int index) => L($"staa {_offset + index},x");

    public override string Crt0()
    {
        var text = new StringBuilder();
        foreach (string symbol in CrtCells())
        {
            text.AppendLine($".global {symbol}");
        }

        text.AppendLine(".global __bss_start");
        text.AppendLine(".global cc_retbuf");
        text.AppendLine(".global cc_rethi");
        text.AppendLine(".extern main");
        text.AppendLine(".extern __bss_end");
        text.AppendLine(".extern __init_start");
        text.AppendLine(".extern __init_end");
        text.AppendLine(".segment \"CODE\"");
        text.AppendLine("""
            lds #$0FFF
            ldx #$FF
            __crt_zz: clr 0,x
            dex
            bne __crt_zz
            ldx #__bss_start
            __crt_z: clr 0,x
            inx
            cpx #__bss_end
            bne __crt_z
            ldx #__init_start
            __crt_i: cpx #__init_end
            beq __crt_id
            stx z:__crt_ip
            ldx 0,x
            jsr 0,x
            ldx z:__crt_ip
            inx
            inx
            bra __crt_i
            __crt_id: jsr main
            __crt_halt: bra __crt_halt
            """);

        // strona bezpośrednia jest zerowana w całości ($01..$FF, obszar C_ZP to $10..$FF); pętla BSS sprawdza koniec po zapisie,
        // bo BSS nie jest pusty (__bss_start)
        text.AppendLine(".segment \"ZP\"");
        text.AppendLine("__crt_ip: .res 2");
        foreach (string symbol in CrtCells().Where(_zeroPage.Contains))
        {
            text.AppendLine($"{symbol}: .res 1");
        }

        text.AppendLine(".segment \"BSS\"");
        text.AppendLine("__bss_start: .res 1");
        foreach (string symbol in CrtCells().Where(symbol => !_zeroPage.Contains(symbol)))
        {
            text.AppendLine($"{symbol}: .res 1");
        }

        text.AppendLine(".segment \"DATA\"");
        text.AppendLine("cc_retbuf: .res 64");
        text.AppendLine("cc_rethi: .res 2");

        return text.ToString();
    }

    protected override string Relax(string text) => BranchRelaxer.Apply(text, Size, Invert);

    private static string? Invert(string branch) => branch switch
    {
        "bne" => "beq",
        "beq" => "bne",
        "bcc" => "bcs",
        "bcs" => "bcc",
        _ => null,
    };

    /// <summary>Rozmiar instrukcji 6800 w bajtach (do relaksacji skoków).</summary>
    private static int Size(string line)
    {
        string[] parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        string mnemonic = parts[0];
        if (parts.Length == 1)
        {
            return 1;
        }

        string operand = parts[1];
        if (mnemonic is "jmp" or "jsr")
        {
            return operand.EndsWith(",x", StringComparison.Ordinal) ? 2 : 3;
        }

        if (mnemonic.Length == 3 && mnemonic[0] == 'b' && mnemonic != "bit")
        {
            return 2;
        }

        if (operand.StartsWith('#'))
        {
            return mnemonic is "ldx" or "lds" or "cpx" ? 3 : 2;
        }

        return operand.StartsWith("z:", StringComparison.Ordinal) || operand.EndsWith(",x", StringComparison.Ordinal) ? 2 : 3;
    }

    /// <summary>Słowo w pamięci big-endian: starszy bajt pod <c>Hi</c>, młodszy pod <c>Hi+1</c> (bez pary <c>x</c>/<c>x_h</c> z crt0).</summary>
    private static bool InMemory(Word word) =>
        !word.IsImmediate && word.Lo != word.Hi + "_h" && Adjacent(new Word(false, word.Hi, word.Lo));

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

    /// <summary>Adres z przedrostkiem <c>z:</c> (tryb bezpośredni), gdy symbol bazowy leży na stronie bezpośredniej. Tylko dla
    /// instrukcji, które mają tryb bezpośredni (LDAA/STAA/ALU/CMPA/LDX/STX).</summary>
    private string Mem(string address)
    {
        int plus = address.IndexOfAny(['+', '-']);
        string symbol = plus < 0 ? address : address[..plus];
        return _zeroPage.Contains(symbol) ? "z:" + address : address;
    }
}
