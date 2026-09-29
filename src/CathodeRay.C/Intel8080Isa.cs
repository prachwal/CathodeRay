using System.Globalization;
using System.Text;

namespace CathodeRay.C;

/// <summary>Prymitywy Intel 8080 (mnemoniki Intel): A jako akumulator, HL jako rejestr adresowy (<c>LXI H,adres; ADD M</c>,
/// wskaźniki przez <c>LHLD</c>), bez rejestrów IX/IY i bez instrukcji Z80.</summary>
internal sealed class Intel8080Isa : ByteIsa
{
    private static readonly HashSet<string> ReservedNames = new(
        ["A", "B", "C", "D", "E", "H", "L", "M", "SP", "PSW", "LOW", "HIGH", "MOD", "SHL", "SHR", "AND", "OR", "XOR", "NOT", "EQ", "NE", "LT", "LE", "GT", "GE"],
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

    public override void LoadA(Octet value) => L(value.IsImmediate ? $"mvi a,{value.Text}" : $"lda {value.Text}");

    public override void StoreA(string address) => L($"sta {address}");

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

    public override void PushA() => L("push psw");

    public override void PopA() => L("pop psw");

    public override void Call(string symbol) => L($"call {symbol}");

    public override void CallIndirect(string cell)
    {
        L($"lhld {cell}");
        L("call __callhl");
    }

    public override void Return() => L("ret");

    public override void PtrSetup(string cell, int offset)
    {
        L($"lhld {cell}");
        if (offset is > 0 and <= 3)
        {
            for (int i = 0; i < offset; i++)
            {
                L("inx h");
            }
        }
        else if (offset > 3)
        {
            L($"lxi d,{offset}");
            L("dad d");
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
        if (value.IsImmediate)
        {
            L($"{immediate} {value.Text}");
            return;
        }

        L($"lxi h,{value.Text}");
        L($"{memory} m");
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
