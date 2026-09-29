using System.Globalization;
using System.Text;

namespace CathodeRay.C;

/// <summary>Prymitywy Motorola 6800: akumulator A, rejestr indeksowy X do dostępu pośredniego (<c>LDAA n,X</c>), słowa
/// big-endian (komórka 2-bajtowa: starszy bajt pod <c>sym</c>, więc <c>LDX komórka</c> ładuje wskaźnik).</summary>
internal sealed class M6800Isa : ByteIsa
{
    private static readonly HashSet<string> ReservedNames = new(["A", "B", "X"], StringComparer.OrdinalIgnoreCase);

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

    public override void LoadA(Octet value) => L(value.IsImmediate ? $"ldaa #{value.Text}" : $"ldaa {value.Text}");

    public override void StoreA(string address) => L($"staa {address}");

    public override void Alu(ByteAlu op, Octet value, bool first)
    {
        string operand = value.IsImmediate ? $"#{value.Text}" : value.Text;
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

    public override void Cmp(Octet value) => L(value.IsImmediate ? $"cmpa #{value.Text}" : $"cmpa {value.Text}");

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

    public override void PushA() => L("psha");

    public override void PopA() => L("pula");

    public override void Call(string symbol) => L($"jsr {symbol}");

    public override void CallIndirect(string cell)
    {
        L($"ldx {cell}");
        L("jsr 0,x");
    }

    public override void Return() => L("rts");

    public override void PtrSetup(string cell, int offset)
    {
        if (offset <= 254)
        {
            L($"ldx {cell}");
            _offset = offset;
            return;
        }

        // Przesunięcie większe niż indeks 8-bitowy: dodajemy je do wskaźnika przez bajty pośrednie (BE: cc_t0 starszy, cc_t1 młodszy).
        L($"ldaa {cell}+1");
        L($"adda #{offset & 0xFF}");
        L("staa cc_t1");
        L($"ldaa {cell}");
        L($"adca #{offset >> 8}");
        L("staa cc_t0");
        L("ldx cc_t0");
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
        text.AppendLine(".extern main");
        text.AppendLine(".extern __bss_end");
        text.AppendLine(".extern __init_start");
        text.AppendLine(".extern __init_end");
        text.AppendLine(".segment \"CODE\"");
        text.AppendLine("""
            lds #$0FFF
            ldx #__bss_start
            __crt_z: cpx #__bss_end
            beq __crt_zd
            clra
            staa 0,x
            inx
            jmp __crt_z
            __crt_zd: ldx #__init_start
            __crt_i: cpx #__init_end
            beq __crt_id
            stx __crt_ip
            ldx 0,x
            jsr 0,x
            ldx __crt_ip
            inx
            inx
            jmp __crt_i
            __crt_id: jsr main
            __crt_halt: jmp __crt_halt
            """);
        text.AppendLine(".segment \"BSS\"");
        text.AppendLine("__bss_start: .res 1");
        text.AppendLine("__crt_ip: .res 2");
        foreach (string symbol in CrtCells())
        {
            text.AppendLine($"{symbol}: .res 1");
        }

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
}
