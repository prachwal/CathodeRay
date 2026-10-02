using System.Globalization;
using System.Text;

namespace CathodeRay.C;

/// <summary>Prymitywy 6502 (składnia ca65). Wskaźnik dostępu pośredniego: <c>__p</c> na stronie zerowej + <c>LDY</c>.</summary>
internal sealed class Mos6502Isa : ByteIsa, IResultReg
{
    private static readonly HashSet<string> ReservedNames = new(["A", "X", "Y"], StringComparer.OrdinalIgnoreCase);

    private static readonly string[] FixedZeroPage =
        ["cc_arg1", "cc_arg1_h", "cc_arg2", "cc_arg2_h", "cc_arg3", "cc_arg3_h", "cc_ret", "cc_ret_h", "cc_t0", "cc_t1", "__p"];

    private readonly HashSet<string> _zeroPage = new(FixedZeroPage, StringComparer.Ordinal);

    private int _labels;

    private int _offset;

    private string _pointer = "__p";

    public override bool SupportsIndexed => true;

    /// <summary>Cel obsługuje wywołanie ogonowe (bezpośrednie: <c>jmp</c>; pośrednie: <c>jmp __icall</c>).</summary>
    public override bool SupportsTailCall => true;

    /// <summary>Wynik w rejestrze tylko na ścieżce v2 (A dla W1, A/X dla W2); v1 wraca przez <c>cc_ret</c>.</summary>
    public bool ReturnsInResultReg => AbiV2;

    /// <summary>Push ramki czyta komórki przez A, więc A wejściowe (młodszy bajt parametru) parkuje w <c>cc_t0</c>
    /// na czas pushy (X je przeżywa); scratch ginie dopiero w ciele funkcji.</summary>
    public override string? EntryParkCell => "cc_t0";

    /// <summary><c>eor</c>/<c>and</c>/<c>ora</c> nie ruszają C (model <c>CpuModels</c>): bias może iść wprost w A.</summary>
    public override bool XorPreservesCarry => true;

    protected override IReadOnlySet<string> Reserved => ReservedNames;

    /// <summary>Nazwa CPU w <see cref="CpuModels"/> (warianty nes/6510 dzielą model 6502).</summary>
    protected override string CpuName => "6502";

    public override string Segment(string name) => $".segment \"{name}\"";

    public override string Global(string sym) => $".global {sym}";

    public override string Extern(string sym) => $".extern {sym}";

    public override string Bytes(IEnumerable<int> values) => $".byte {string.Join(", ", values.Select(static v => v.ToString(CultureInfo.InvariantCulture)))}";

    public override string Word(string expression) => $".word {expression}";

    public override string Reserve(int size) => $".res {size}";

    public override string Preamble() => ".extern __p\n";

    public override string? AddressByte(string expression, int index) => (index == 0 ? "<(" : ">(") + expression + ")";

    /// <summary>Dopisuje komórki modułu przeniesione na stronę zerową (operandy dostają przedrostek <c>z:</c>).</summary>
    /// <param name="names">Nazwy symboli (bez przesunięć).</param>
    public void AddZeroPage(IEnumerable<string> names) => _zeroPage.UnionWith(names);

    public override void LoadA(Octet value) => L(value.IsImmediate ? $"lda #{value.Text}" : $"lda {Mem(value.Text)}");

    public override void StoreA(string address) => L($"sta {Mem(address)}");

    public override void Alu(ByteAlu op, Octet value, bool first)
    {
        string operand = value.IsImmediate ? $"#{value.Text}" : Mem(value.Text);
        switch (op)
        {
            case ByteAlu.Add:
                if (first)
                {
                    L("clc");
                }

                L($"adc {operand}");
                break;
            case ByteAlu.Sub:
                if (first)
                {
                    L("sec");
                }

                L($"sbc {operand}");
                break;
            case ByteAlu.And:
                L($"and {operand}");
                break;
            case ByteAlu.Or:
                L($"ora {operand}");
                break;
            default:
                L($"eor {operand}");
                break;
        }
    }

    public override void Cmp(Octet value) => L(value.IsImmediate ? $"cmp #{value.Text}" : $"cmp {Mem(value.Text)}");

    public override void ShlA(bool first) => L(first ? "asl a" : "rol a");

    public override void ShrA(bool first) => L(first ? "lsr a" : "ror a");

    public override void Jump(string label) => L($"jmp {label}");

    public override void JumpIf(ByteFlag flag, string label)
    {
        string skip = $"__j{++_labels}";
        L(flag switch
        {
            ByteFlag.Zero => $"bne {skip}",
            ByteFlag.NotZero => $"beq {skip}",
            ByteFlag.Borrow => $"bcs {skip}",
            _ => $"bcc {skip}",
        });
        L($"jmp {label}");
        L($"{skip}:");
    }

    public override bool TryStep(IReadOnlyList<string> bytes, bool increment)
    {
        if (bytes.Count == 1)
        {
            L($"{(increment ? "inc" : "dec")} {Mem(bytes[0])}");
            return true;
        }

        string skip = LocalLabel();
        if (increment)
        {
            L($"inc {Mem(bytes[0])}");
            L($"bne {skip}");
            L($"inc {Mem(bytes[1])}");
        }
        else
        {
            L($"lda {Mem(bytes[0])}");
            L($"bne {skip}");
            L($"dec {Mem(bytes[1])}");
            L($"{skip}:");
            L($"dec {Mem(bytes[0])}");
            return true;
        }

        L($"{skip}:");
        return true;
    }

    public override void IndexSetup(string index, int shift)
    {
        if (shift == 0)
        {
            L($"ldx {Mem(index)}");
            return;
        }

        L($"lda {Mem(index)}");
        for (int i = 0; i < shift; i++)
        {
            L("asl a");
        }

        L("tax");
    }

    public override void IndexLoad(string address) => L($"lda {Mem(address)},x");

    public override void IndexStore(string address) => L($"sta {Mem(address)},x");

    public override void PushA() => L("pha");

    public override void PopA() => L("pla");

    public override void Call(string symbol) => L($"jsr {symbol}");

    public override void CallIndirect(string cell)
    {
        SetupFp(cell);
        L("jsr __icall");
    }

    /// <summary>Skok pośredni w pozycji ogonowej: ten sam wskaźnik <c>cc_fp</c>, ale skok zamiast wołania.</summary>
    /// <param name="cell">Symbol komórki z adresem.</param>
    public override void TailCallIndirect(string cell)
    {
        SetupFp(cell);
        L("jmp __icall");
    }

    /// <summary>Wskaźnik wołania pośredniego do <c>cc_fp</c> (bez skoku; selektor stawia go przed argumentami v2).</summary>
    /// <param name="cell">Symbol komórki z adresem.</param>
    public override void SetupFp(string cell)
    {
        L($"lda {Mem(cell)}");
        L("sta cc_fp");
        L($"lda {Mem(cell + "+1")}");
        L("sta cc_fp+1");
    }

    /// <summary>Lokalizacja argumentu na v2: pierwszy argument W≤2 w A (W1) albo A/X (W2, młodszy/starszy);
    /// reszta i szersze w <c>cc_argN</c> pozycyjnie (nadmiar wariadyczny też tam).</summary>
    /// <param name="index">Numer argumentu od zera.</param>
    /// <param name="part">0 = młodszy bajt, 1 = starszy.</param>
    /// <param name="width">Szerokość argumentu (pary tylko dla W≤2).</param>
    /// <returns>Nazwa rejestru albo symbol komórki.</returns>
    public override string ArgCell(int index, int part, int width) =>
        AbiV2 && index == 0 && width <= 2 ? (part == 0 ? "a" : "x") : base.ArgCell(index, part, width);

    /// <summary>Bajt wyniku do rejestru: w A już leży (nic do roboty) albo <c>tax</c> dla starszego do X.</summary>
    /// <param name="index">0 = młodszy (już w A), 1 = starszy (A → X).</param>
    public void ResultByteFromA(int index)
    {
        if (index != 0)
        {
            L("tax");
        }
    }

    /// <summary>A z rejestru wyniku: już w A (nic) albo <c>txa</c> dla starszego z X.</summary>
    /// <param name="index">0 = młodszy (już w A), 1 = starszy (X → A).</param>
    public void ResultByteToA(int index)
    {
        if (index != 0)
        {
            L("txa");
        }
    }

    /// <summary>A ← rejestr argumentu (v2): <c>a</c> już w A, <c>x</c> przez <c>txa</c>.</summary>
    /// <param name="reg">Rejestr argumentu z modelu.</param>
    public override void FetchArg(string reg)
    {
        if (reg == "x")
        {
            L("txa");
        }
        else if (reg != "a")
        {
            throw new ArgumentException($"6502 nie trzyma argumentów w '{reg}'.", nameof(reg));
        }
    }

    /// <summary>Rejestr argumentu ← A (v2): <c>a</c> już w A, <c>x</c> przez <c>tax</c>.</summary>
    /// <param name="reg">Rejestr argumentu z modelu.</param>
    public override void StoreArg(string reg)
    {
        if (reg == "x")
        {
            L("tax");
        }
        else if (reg != "a")
        {
            throw new ArgumentException($"6502 nie trzyma argumentów w '{reg}'.", nameof(reg));
        }
    }

    /// <summary>Zdejmuje bajt ze stosu z zachowaniem wyniku w A/X: wynik parkuje w scratchu <c>cc_t0</c>
    /// (na wejściu do stopki nic w nim nie żyje), bo <c>pha</c> zakopałby zdejmowany bajt pod wynikiem.
    /// X (starszy bajt wyniku) nietknięty.</summary>
    /// <param name="address">Cel w pamięci.</param>
    /// <param name="keepResult">Wynik żyje w A/X.</param>
    public override void PopByte(string address, bool keepResult)
    {
        if (!keepResult)
        {
            PopA();
            StoreA(address);
            return;
        }

        StoreA("cc_t0");
        PopA();
        StoreA(address);
        LoadA(new Octet(false, "cc_t0"));
    }

    /// <summary>Skok na znaku i zerze słowa bez odejmowania: starszy bajt do A, potem N/Z.
    /// Skoki warunkowe 6502 są krótkie, więc daleki cel idzie trampoliną jak w <see cref="JumpIf"/>.</summary>
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
                L($"lda {hi}");
                string ltSkip = LocalLabel();
                L($"bpl {ltSkip}");
                L($"jmp {target}");
                L($"{ltSkip}:");
                break;
            case Ir.Cond.Ge:
                L($"lda {hi}");
                string geSkip = LocalLabel();
                L($"bmi {geSkip}");
                L($"jmp {target}");
                L($"{geSkip}:");
                break;
            case Ir.Cond.Gt:
                // N testowane przed ora (ora nadpisuje A); osobne skipy, bo BranchRelaxer kasuje
                // etykietę z trójki (beq S; jmp T; S:) — współdzielona zawisłaby w bmi.
                L($"lda {hi}");
                string gtNeg = LocalLabel();
                L($"bmi {gtNeg}");
                L($"ora {lo}");
                string gtZero = LocalLabel();
                L($"beq {gtZero}");
                L($"jmp {target}");
                L($"{gtNeg}:");
                L($"{gtZero}:");
                break;
            default:
                L($"lda {hi}");
                string leMid = LocalLabel();
                L($"bpl {leMid}");
                L($"jmp {target}");
                L($"{leMid}:");
                L($"ora {lo}");
                string leSkip = LocalLabel();
                L($"bne {leSkip}");
                L($"jmp {target}");
                L($"{leSkip}:");
                break;
        }

        return true;
    }

    public override void Return() => L("rts");

    public override void PtrSetup(string cell, int offset, bool mustCopy = false)
    {
        if (offset <= 254 && !mustCopy && _zeroPage.Contains(cell))
        {
            // wskaźnik już leży na stronie zerowej: (zp),Y wprost, bez kopii
            _pointer = cell;
            _offset = offset;
            return;
        }

        _pointer = "__p";
        if (offset <= 254)
        {
            L($"lda {Mem(cell)}");
            L("sta z:__p");
            L($"lda {Mem(cell + "+1")}");
            L("sta z:__p+1");
            _offset = offset;
            return;
        }

        L($"lda {Mem(cell)}");
        L("clc");
        L($"adc #{offset & 0xFF}");
        L("sta z:__p");
        L($"lda {Mem(cell + "+1")}");
        L($"adc #{offset >> 8}");
        L("sta z:__p+1");
        _offset = 0;
    }

    public override void PtrLoad(int index)
    {
        L($"ldy #{_offset + index}");
        L($"lda ({_pointer}),y");
    }

    public override void PtrStore(int index)
    {
        L($"ldy #{_offset + index}");
        L($"sta ({_pointer}),y");
    }

    public override string Crt0()
    {
        var text = new StringBuilder();
        foreach (string symbol in Cells())
        {
            text.AppendLine($".global {symbol}");
        }

        text.AppendLine(".global __p");
        text.AppendLine(".global __bss_start");
        text.AppendLine(".global cc_retbuf");
        text.AppendLine(".global cc_rethi");
        text.AppendLine(".global __icall");
        text.AppendLine(".extern main");
        text.AppendLine(".extern __bss_end");
        text.AppendLine(".extern __init_start");
        text.AppendLine(".extern __init_end");
        text.AppendLine(".segment \"CODE\"");
        text.AppendLine(".global __zp_start");
        text.AppendLine(".extern __zp_end");
        text.AppendLine("""
            ldx #255
            txs
            cld
            ldx #<__zp_start
            lda #0
            __crt_zz: sta 0,x
            inx
            cpx #<__zp_end
            bne __crt_zz
            lda #<__bss_start
            sta __q
            lda #>__bss_start
            sta __q+1
            ldy #0
            __crt_z: lda __q
            cmp #<__bss_end
            bne __crt_z1
            lda __q+1
            cmp #>__bss_end
            beq __crt_zd
            __crt_z1: lda #0
            sta (__q),y
            inc __q
            bne __crt_z
            inc __q+1
            jmp __crt_z
            __crt_zd: lda #<__init_start
            sta __q
            lda #>__init_start
            sta __q+1
            __crt_i: lda __q
            cmp #<__init_end
            bne __crt_i1
            lda __q+1
            cmp #>__init_end
            beq __crt_id
            __crt_i1: ldy #0
            lda (__q),y
            sta cc_fp
            iny
            lda (__q),y
            sta cc_fp+1
            jsr __icall
            clc
            lda __q
            adc #2
            sta __q
            bcc __crt_i
            inc __q+1
            jmp __crt_i
            __crt_id: jsr main
            __crt_halt: jmp __crt_halt
            __icall: jmp (cc_fp)
            """);
        text.AppendLine(".segment \"ZP\"");
        text.AppendLine("__zp_start:");
        text.AppendLine("__p: .res 2");
        text.AppendLine("__q: .res 2");
        foreach (string symbol in Cells().Where(_zeroPage.Contains))
        {
            text.AppendLine($"{symbol}: .res 1");
        }

        text.AppendLine(".segment \"BSS\"");
        text.AppendLine("__bss_start: .res 1");
        foreach (string symbol in Cells().Where(symbol => !_zeroPage.Contains(symbol)))
        {
            text.AppendLine($"{symbol}: .res 1");
        }

        text.AppendLine(".segment \"DATA\"");
        text.AppendLine("cc_retbuf: .res 64");
        text.AppendLine("cc_rethi: .res 2");

        text.AppendLine("cc_fp: .res 2");
        text.AppendLine(".global cc_fp");
        string code = text.ToString();
        if (AbiV2)
        {
            // wynik main w A/X wraca też do cc_ret (harness, mapy i narzędzia czytają pamięć)
            code = code.Replace("__crt_id: jsr main", "__crt_id: jsr main\n            sta cc_ret\n            stx cc_ret_h", StringComparison.Ordinal);
        }

        return code;
    }

    protected override string Relax(string text) =>
        AsmInsn.EmitAll(BranchRelaxer.Apply([.. AsmInsn.ParseAll(text)], Size, Invert));

    private static IEnumerable<string> Cells()
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

    private static string? Invert(string branch) => branch switch
    {
        "bne" => "beq",
        "beq" => "bne",
        "bcc" => "bcs",
        "bcs" => "bcc",
        _ => null,
    };

    /// <summary>Rozmiar instrukcji 6502 w bajtach (do relaksacji skoków).</summary>
    /// <param name="ins">Linia (tu docierają tylko instrukcje — puste, komentarze i etykiety filtruje wołający).</param>
    /// <returns>Rozmiar w bajtach.</returns>
    private static int Size(AsmInsn ins)
    {
        string mnemonic = ins.Mnemonic ?? string.Empty;
        if (ins.Gap.Length == 0)
        {
            return 1;
        }

        string operand = ins.Operands;
        if (mnemonic is "jmp" or "jsr")
        {
            return 3;
        }

        if (mnemonic.Length == 3 && mnemonic[0] == 'b' && mnemonic != "bit")
        {
            return 2;
        }

        if (mnemonic is "asl" or "lsr" or "rol" or "ror" && operand == "a")
        {
            return 1;
        }

        return operand.StartsWith('#') || operand.StartsWith("z:", StringComparison.Ordinal) || operand.StartsWith('(') || operand.StartsWith("0,x", StringComparison.Ordinal) ? 2 : 3;
    }

    /// <summary>Adres z przedrostkiem <c>z:</c> (strona zerowa), gdy symbol bazowy leży na stronie zerowej.</summary>
    private string Mem(string address)
    {
        int plus = address.IndexOfAny(['+', '-']);
        string symbol = plus < 0 ? address : address[..plus];
        return _zeroPage.Contains(symbol) ? "z:" + address : address;
    }
}
