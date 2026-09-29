using System.Globalization;
using System.Text;

namespace CathodeRay.C;

/// <summary>Prymitywy Z80 (składnia Zilog): A jako akumulator, HL jako rejestr adresowy (operandy pamięciowe ALU przez
/// <c>LD HL,adres; op A,(HL)</c>, wskaźniki przez <c>LD HL,(komórka)</c>), zapis/odczyt komórek przez <c>LD A,(adres)</c>.</summary>
internal sealed class Z80Isa : ByteIsa
{
    private static readonly HashSet<string> ReservedNames = new(
        ["A", "B", "C", "D", "E", "H", "L", "I", "R", "AF", "BC", "DE", "HL", "SP", "IX", "IY", "IXH", "IXL", "IYH", "IYL", "NZ", "Z", "NC", "PO", "PE", "P", "M", "LOW", "HIGH", "MOD", "SHL", "SHR", "AND", "OR", "XOR", "NOT"],
        StringComparer.OrdinalIgnoreCase);

    private int _position;

    public override IEnumerable<string> IndirectSymbols => ["__callhl"];

    protected override IReadOnlySet<string> Reserved => ReservedNames;

    public override string Segment(string name) => $"SEGMENT \"{name}\"";

    public override string Global(string sym) => $"GLOBAL {sym}";

    public override string Extern(string sym) => $"EXTERN {sym}";

    public override string Bytes(IEnumerable<int> values) => $"DB {string.Join(", ", values.Select(static v => v.ToString(CultureInfo.InvariantCulture)))}";

    public override string Word(string expression) => $"DW {expression}";

    public override string Reserve(int size) => $"DS {size}";

    public override void LoadA(Octet value) => L(value.IsImmediate ? $"ld a,{value.Text}" : $"ld a,({value.Text})");

    public override void StoreA(string address) => L($"ld ({address}),a");

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

    public override void PushA() => L("push af");

    public override void PopA() => L("pop af");

    public override void Call(string symbol) => L($"call {symbol}");

    public override void CallIndirect(string cell)
    {
        L($"ld hl,({cell})");
        L("call __callhl");
    }

    public override void Return() => L("ret");

    public override void PtrSetup(string cell, int offset)
    {
        L($"ld hl,({cell})");
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

        return text.ToString();
    }

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
        if (value.IsImmediate)
        {
            L($"{mnemonic}{value.Text}");
            return;
        }

        L($"ld hl,{value.Text}");
        L($"{mnemonic}(hl)");
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
