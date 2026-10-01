using System.Text.Json;

namespace CathodeRay.Tests;

/// <summary>Interpreter Motorola 6800 do testów celu: tablica opkodów, tryby i cykle z <c>mcp_6800_instructions.json</c>
/// (mnemonik z operandem, np. <c>ADDA #d8</c>, <c>LDX a16</c>, <c>STAA d8,X</c>). Nieobsłużone (DAA, SWI, RTI) rzucają
/// wyjątek. Zatrzymanie: WAI albo skok/rozgałęzienie do samego siebie.</summary>
public sealed class Mc6800Cpu
{
    public const int Carry = 0x01;
    public const int Overflow = 0x02;
    public const int Zero = 0x04;
    public const int Negative = 0x08;

    private static readonly Lazy<Dictionary<int, Entry>> Table = new(Load);

    private static readonly HashSet<string> Binary = ["ADD", "ADC", "SUB", "SBC", "AND", "ORA", "EOR", "CMP", "BIT", "LDA", "STA"];

    public byte[] Memory { get; } = new byte[0x10000];

    public byte A { get; set; }

    public byte B { get; set; }

    public ushort X { get; set; }

    public ushort Sp { get; set; }

    public ushort Pc { get; set; }

    public byte Cc { get; set; } = 0xC0;

    public long Cycles { get; private set; }

    public bool Halted { get; private set; }

    public bool Flag(int mask) => (Cc & mask) != 0;

    public void Step()
    {
        if (Halted)
        {
            return;
        }

        ushort start = Pc;
        int opcode = Memory[Pc++];
        if (!Table.Value.TryGetValue(opcode, out Entry? entry))
        {
            throw new NotSupportedException($"6800: opcode ${opcode:X2} at ${start:X4} is not supported.");
        }

        Cycles += entry.Cycles;
        Execute(entry);
        if (Pc == start && !Halted)
        {
            Halted = true;
        }
    }

    private static Dictionary<int, Entry> Load()
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Repo.IsaFile("mcp_6800_instructions.json")));
        var table = new Dictionary<int, Entry>();
        foreach (JsonElement e in doc.RootElement.GetProperty("instructions").EnumerateArray())
        {
            string[] parts = e.GetProperty("mnemonic").GetString()!.Split(' ');
            table[Convert.ToInt32(e.GetProperty("opcode").GetString(), 16)] = new Entry(parts[0], parts.Length > 1 ? parts[1] : string.Empty, e.GetProperty("cycles").GetInt32());
        }

        return table;
    }

    private void Set(int mask, bool on) => Cc = (byte)(on ? Cc | mask : Cc & ~mask);

    private byte Nz(byte value)
    {
        Set(Negative, (value & 0x80) != 0);
        Set(Zero, value == 0);
        return value;
    }

    private ushort Fetch16()
    {
        int hi = Memory[Pc++];
        return (ushort)((hi << 8) | Memory[Pc++]);
    }

    private ushort Read16(int address) => (ushort)((Memory[address & 0xFFFF] << 8) | Memory[(address + 1) & 0xFFFF]);

    private void Write16(int address, ushort value)
    {
        Memory[address & 0xFFFF] = (byte)(value >> 8);
        Memory[(address + 1) & 0xFFFF] = (byte)value;
    }

    private void Push(byte value) => Memory[Sp--] = value;

    private byte Pull() => Memory[++Sp];

    /// <summary>Adres efektywny operandu pamięciowego (direct, indexed, extended) albo -1 dla natychmiastowego.</summary>
    private int Address(string mode)
    {
        switch (mode)
        {
            case "d8":
                return Memory[Pc++];
            case "d8,X":
                return (X + Memory[Pc++]) & 0xFFFF;
            case "a16":
                return Fetch16();
            default:
                return -1;
        }
    }

    private void Execute(Entry entry)
    {
        string name = entry.Name;
        switch (name)
        {
            case "NOP": return;
            case "WAI": Halted = true; return;
            case "CLC": Set(Carry, false); return;
            case "SEC": Set(Carry, true); return;
            case "CLV": Set(Overflow, false); return;
            case "SEV": Set(Overflow, true); return;
            case "CLI": case "SEI": return;
            case "INX": X++; Set(Zero, X == 0); return;
            case "DEX": X--; Set(Zero, X == 0); return;
            case "INS": Sp++; return;
            case "DES": Sp--; return;
            case "TAB": B = Nz(A); Set(Overflow, false); return;
            case "TBA": A = Nz(B); Set(Overflow, false); return;
            case "TAP": Cc = (byte)(A | 0xC0); return;
            case "TPA": A = Cc; return;
            case "TSX": X = (ushort)(Sp + 1); return;
            case "TXS": Sp = (ushort)(X - 1); return;
            case "ABA": A = Add(A, B, 0); return;
            case "SBA": A = Sub(A, B, 0); return;
            case "CBA": Sub(A, B, 0); return;
            case "PSHA": Push(A); return;
            case "PSHB": Push(B); return;
            case "PULA": A = Pull(); return;
            case "PULB": B = Pull(); return;
            case "RTS": Pc = (ushort)((Pull() << 8) | Pull()); return;
            case "JMP": Pc = (ushort)Address(entry.Mode); return;
            case "JSR":
            case "BSR":
                ushort target = name == "BSR" ? Relative() : (ushort)Address(entry.Mode);
                Push((byte)Pc);
                Push((byte)(Pc >> 8));
                Pc = target;
                return;
            case "LDX": X = LoadWord(entry.Mode); return;
            case "LDS": Sp = LoadWord(entry.Mode); return;
            case "STX": StoreWord(entry.Mode, X); return;
            case "STS": StoreWord(entry.Mode, Sp); return;
            case "CPX": Compare16(entry.Mode); return;
        }

        if (entry.Mode == "rel")
        {
            Branch(name, Relative());
            return;
        }

        ExecuteRegisterOrMemory(entry);
    }

    private ushort Relative()
    {
        sbyte offset = (sbyte)Memory[Pc++];
        return (ushort)(Pc + offset);
    }

    private void Branch(string name, ushort target)
    {
        bool n = Flag(Negative);
        bool z = Flag(Zero);
        bool v = Flag(Overflow);
        bool c = Flag(Carry);
        bool taken = name switch
        {
            "BRA" => true,
            "BCC" => !c,
            "BCS" => c,
            "BEQ" => z,
            "BNE" => !z,
            "BGE" => n == v,
            "BGT" => !z && n == v,
            "BHI" => !c && !z,
            "BLE" => z || n != v,
            "BLS" => c || z,
            "BLT" => n != v,
            "BMI" => n,
            "BPL" => !n,
            "BVC" => !v,
            "BVS" => v,
            _ => throw new NotSupportedException($"6800: {name}."),
        };
        if (taken)
        {
            Pc = target;
        }
    }

    private ushort LoadWord(string mode)
    {
        if (mode == "#d16")
        {
            return Fetch16();
        }

        ushort value = Read16(Address(mode));
        Set(Negative, (value & 0x8000) != 0);
        Set(Zero, value == 0);
        Set(Overflow, false);
        return value;
    }

    private void StoreWord(string mode, ushort value)
    {
        Write16(Address(mode), value);
        Set(Negative, (value & 0x8000) != 0);
        Set(Zero, value == 0);
        Set(Overflow, false);
    }

    private void Compare16(string mode)
    {
        ushort operand = mode == "#d16" ? Fetch16() : Read16(Address(mode));
        int diff = X - operand;
        Set(Zero, (diff & 0xFFFF) == 0);
        Set(Negative, (diff & 0x8000) != 0);
        Set(Overflow, ((X ^ operand) & (X ^ diff) & 0x8000) != 0);
    }

    private byte Add(byte a, byte m, int carry)
    {
        int sum = a + m + carry;
        Set(Carry, sum > 0xFF);
        Set(Overflow, (~(a ^ m) & (a ^ sum) & 0x80) != 0);
        return Nz((byte)sum);
    }

    private byte Sub(byte a, byte m, int borrow)
    {
        int diff = a - m - borrow;
        Set(Carry, diff < 0);
        Set(Overflow, ((a ^ m) & (a ^ diff) & 0x80) != 0);
        return Nz((byte)diff);
    }

    private void ExecuteRegisterOrMemory(Entry entry)
    {
        string name = entry.Name;
        char reg = ' ';
        string op = name;
        bool accumulatorForm = entry.Mode.Length == 0;
        if (!accumulatorForm && name.Length >= 3 && name[^1] is 'A' or 'B' && Binary.Contains(name[..^1]))
        {
            reg = name[^1];
            op = name[..^1];
        }

        if (reg != ' ')
        {
            int address = entry.Mode == "#d8" ? Pc++ : Address(entry.Mode);
            byte m = op == "STA" ? (byte)0 : Memory[address];
            byte a = reg == 'A' ? A : B;
            byte result = a;
            switch (op)
            {
                case "ADD": result = Add(a, m, 0); break;
                case "ADC": result = Add(a, m, Flag(Carry) ? 1 : 0); break;
                case "SUB": result = Sub(a, m, 0); break;
                case "SBC": result = Sub(a, m, Flag(Carry) ? 1 : 0); break;
                case "AND": result = Nz((byte)(a & m)); Set(Overflow, false); break;
                case "ORA": result = Nz((byte)(a | m)); Set(Overflow, false); break;
                case "EOR": result = Nz((byte)(a ^ m)); Set(Overflow, false); break;
                case "LDA": result = Nz(m); Set(Overflow, false); break;
                case "CMP": Sub(a, m, 0); return;
                case "BIT": Nz((byte)(a & m)); Set(Overflow, false); return;
                case "STA":
                    Memory[address] = a;
                    return;
            }

            if (reg == 'A')
            {
                A = result;
            }
            else
            {
                B = result;
            }

            return;
        }

        // Jednoargumentowe: rejestr (NEGA…) albo pamięć (NEG a16 / d8,X).
        string unary = accumulatorForm ? name[..^1] : name;
        int target = accumulatorForm ? -1 : Address(entry.Mode);
        byte value = accumulatorForm ? (name[^1] == 'A' ? A : B) : Memory[target];
        byte outValue = value;
        bool store = true;
        switch (unary)
        {
            case "NEG":
                outValue = Sub(0, value, 0);
                Set(Carry, value != 0);
                Set(Overflow, value == 0x80);
                break;
            case "COM":
                outValue = Nz((byte)~value);
                Set(Overflow, false);
                Set(Carry, true);
                break;
            case "LSR":
                Set(Carry, (value & 1) != 0);
                outValue = Nz((byte)(value >> 1));
                Set(Overflow, Flag(Negative) != Flag(Carry));
                break;
            case "ROR":
            {
                bool carryIn = Flag(Carry);
                Set(Carry, (value & 1) != 0);
                outValue = Nz((byte)((value >> 1) | (carryIn ? 0x80 : 0)));
                Set(Overflow, Flag(Negative) != Flag(Carry));
                break;
            }

            case "ASR":
                Set(Carry, (value & 1) != 0);
                outValue = Nz((byte)((value >> 1) | (value & 0x80)));
                Set(Overflow, Flag(Negative) != Flag(Carry));
                break;
            case "ASL":
                Set(Carry, (value & 0x80) != 0);
                outValue = Nz((byte)(value << 1));
                Set(Overflow, Flag(Negative) != Flag(Carry));
                break;
            case "ROL":
            {
                bool carryIn = Flag(Carry);
                Set(Carry, (value & 0x80) != 0);
                outValue = Nz((byte)((value << 1) | (carryIn ? 1 : 0)));
                Set(Overflow, Flag(Negative) != Flag(Carry));
                break;
            }

            case "DEC":
                outValue = Nz((byte)(value - 1));
                Set(Overflow, value == 0x80);
                break;
            case "INC":
                outValue = Nz((byte)(value + 1));
                Set(Overflow, value == 0x7F);
                break;
            case "TST":
                Nz(value);
                Set(Overflow, false);
                Set(Carry, false);
                store = false;
                break;
            case "CLR":
                outValue = 0;
                Nz(0);
                Set(Overflow, false);
                Set(Carry, false);
                break;
            default:
                throw new NotSupportedException($"6800: {name} is not supported.");
        }

        if (!store)
        {
            return;
        }

        if (!accumulatorForm)
        {
            Memory[target] = outValue;
        }
        else if (name[^1] == 'A')
        {
            A = outValue;
        }
        else
        {
            B = outValue;
        }
    }

    private sealed record Entry(string Name, string Mode, int Cycles);
}
