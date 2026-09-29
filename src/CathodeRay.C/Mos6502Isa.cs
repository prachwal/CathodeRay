using System.Globalization;
using System.Text;

namespace CathodeRay.C;

/// <summary>Prymitywy 6502 (składnia ca65). Wskaźnik dostępu pośredniego: <c>__p</c> na stronie zerowej + <c>LDY</c>.</summary>
internal sealed class Mos6502Isa : ByteIsa
{
    private static readonly HashSet<string> ReservedNames = new(["A", "X", "Y"], StringComparer.OrdinalIgnoreCase);

    private int _labels;

    private int _offset;

    protected override IReadOnlySet<string> Reserved => ReservedNames;

    public override string Segment(string name) => $".segment \"{name}\"";

    public override string Global(string sym) => $".global {sym}";

    public override string Extern(string sym) => $".extern {sym}";

    public override string Bytes(IEnumerable<int> values) => $".byte {string.Join(", ", values.Select(static v => v.ToString(CultureInfo.InvariantCulture)))}";

    public override string Word(string expression) => $".word {expression}";

    public override string Reserve(int size) => $".res {size}";

    public override string Preamble() => ".extern __p\n";

    public override string? AddressByte(string expression, int index) => (index == 0 ? "<(" : ">(") + expression + ")";

    public override void LoadA(Octet value) => L(value.IsImmediate ? $"lda #{value.Text}" : $"lda {value.Text}");

    public override void StoreA(string address) => L($"sta {address}");

    public override void Alu(ByteAlu op, Octet value, bool first)
    {
        string operand = value.IsImmediate ? $"#{value.Text}" : value.Text;
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

    public override void Cmp(Octet value) => L(value.IsImmediate ? $"cmp #{value.Text}" : $"cmp {value.Text}");

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
            L($"{(increment ? "inc" : "dec")} {bytes[0]}");
            return true;
        }

        string skip = LocalLabel();
        if (increment)
        {
            L($"inc {bytes[0]}");
            L($"bne {skip}");
            L($"inc {bytes[1]}");
        }
        else
        {
            L($"lda {bytes[0]}");
            L($"bne {skip}");
            L($"dec {bytes[1]}");
            L($"{skip}:");
            L($"dec {bytes[0]}");
            return true;
        }

        L($"{skip}:");
        return true;
    }

    public override void PushA() => L("pha");

    public override void PopA() => L("pla");

    public override void Call(string symbol) => L($"jsr {symbol}");

    public override void CallIndirect(string cell)
    {
        L($"lda {cell}");
        L("sta cc_fp");
        L($"lda {cell}+1");
        L("sta cc_fp+1");
        L("jsr __icall");
    }

    public override void Return() => L("rts");

    public override void PtrSetup(string cell, int offset)
    {
        if (offset <= 254)
        {
            L($"lda {cell}");
            L("sta __p");
            L($"lda {cell}+1");
            L("sta __p+1");
            _offset = offset;
            return;
        }

        L($"lda {cell}");
        L("clc");
        L($"adc #{offset & 0xFF}");
        L("sta __p");
        L($"lda {cell}+1");
        L($"adc #{offset >> 8}");
        L("sta __p+1");
        _offset = 0;
    }

    public override void PtrLoad(int index)
    {
        L($"ldy #{_offset + index}");
        L("lda (__p),y");
    }

    public override void PtrStore(int index)
    {
        L($"ldy #{_offset + index}");
        L("sta (__p),y");
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
        text.AppendLine("""
            ldx #255
            txs
            cld
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
        text.AppendLine("__p: .res 2");
        text.AppendLine("__q: .res 2");
        text.AppendLine(".segment \"BSS\"");
        text.AppendLine("__bss_start: .res 1");
        foreach (string symbol in Cells())
        {
            text.AppendLine($"{symbol}: .res 1");
        }

        text.AppendLine(".segment \"DATA\"");
        text.AppendLine("cc_retbuf: .res 64");
        text.AppendLine("cc_rethi: .res 2");

        text.AppendLine("cc_fp: .res 2");
        text.AppendLine(".global cc_fp");
        return text.ToString();
    }

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
}
