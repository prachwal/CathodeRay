using System.Text.Json;

namespace CathodeRay.Tests;

/// <summary>Minimalny interpreter NMOS 6502 do testów celu (plan 30, krok 9). Tablice dekodowania i cykle pochodzą z
/// <c>mcp_6502_instructions.json</c>; opcody nieudokumentowane i tryb dziesiętny (D) rzucają wyjątek. Zatrzymanie: KIL
/// albo skok/rozgałęzienie do samego siebie (<c>JMP *</c>).</summary>
public sealed class Mos6502
{
    public const byte Carry = 0x01;
    public const byte Zero = 0x02;
    public const byte Interrupt = 0x04;
    public const byte Decimal = 0x08;
    public const byte Overflow = 0x40;
    public const byte Negative = 0x80;

    private static readonly Lazy<Dictionary<int, (string Mnemonic, string Encoding, int Cycles)>> Table = new(LoadTable);

    private static readonly HashSet<string> Documented = new(
    [
        "ADC", "AND", "ASL", "BCC", "BCS", "BEQ", "BIT", "BMI", "BNE", "BPL", "BRK", "BVC", "BVS", "CLC", "CLD", "CLI", "CLV",
        "CMP", "CPX", "CPY", "DEC", "DEX", "DEY", "EOR", "INC", "INX", "INY", "JMP", "JSR", "LDA", "LDX", "LDY", "LSR", "NOP",
        "ORA", "PHA", "PHP", "PLA", "PLP", "ROL", "ROR", "RTI", "RTS", "SBC", "SEC", "SED", "SEI", "STA", "STX", "STY", "TAX",
        "TAY", "TSX", "TXA", "TXS", "TYA", "KIL",
    ]);

    public byte[] Memory { get; } = new byte[0x10000];

    public byte A { get; set; }

    public byte X { get; set; }

    public byte Y { get; set; }

    public byte Sp { get; set; } = 0xFD;

    public ushort Pc { get; set; }

    public byte P { get; set; } = 0x24;

    public long Cycles { get; private set; }

    public bool Halted { get; private set; }

    public bool Flag(byte mask) => (P & mask) != 0;

    public void Step()
    {
        if (Halted)
        {
            return;
        }

        ushort start = Pc;
        int opcode = Memory[Pc];
        if (!Table.Value.TryGetValue(opcode, out var info) || !Documented.Contains(info.Mnemonic))
        {
            throw new NotSupportedException($"6502: opcode ${opcode:X2} at ${Pc:X4} is not supported.");
        }

        Pc++;
        Cycles += info.Cycles;
        string m = info.Mnemonic;
        switch (m)
        {
            case "KIL":
                Halted = true;
                return;
            case "BRK":
                throw new NotSupportedException($"6502: BRK at ${start:X4}.");
            case "NOP":
                Pc += (ushort)Operand(info.Encoding).Skip;
                break;
            case "CLC": Set(Carry, false); break;
            case "SEC": Set(Carry, true); break;
            case "CLI": Set(Interrupt, false); break;
            case "SEI": Set(Interrupt, true); break;
            case "CLD": Set(Decimal, false); break;
            case "SED": Set(Decimal, true); break;
            case "CLV": Set(Overflow, false); break;
            case "TAX": X = Nz(A); break;
            case "TAY": Y = Nz(A); break;
            case "TXA": A = Nz(X); break;
            case "TYA": A = Nz(Y); break;
            case "TSX": X = Nz(Sp); break;
            case "TXS": Sp = X; break;
            case "INX": X = Nz((byte)(X + 1)); break;
            case "INY": Y = Nz((byte)(Y + 1)); break;
            case "DEX": X = Nz((byte)(X - 1)); break;
            case "DEY": Y = Nz((byte)(Y - 1)); break;
            case "PHA": Push(A); break;
            case "PHP": Push((byte)(P | 0x30)); break;
            case "PLA": A = Nz(Pull()); break;
            case "PLP": P = (byte)((Pull() & 0xCF) | 0x20); break;
            case "RTS": Pc = (ushort)((Pull() | (Pull() << 8)) + 1); break;
            case "RTI":
                P = (byte)((Pull() & 0xCF) | 0x20);
                Pc = (ushort)(Pull() | (Pull() << 8));
                break;
            case "JSR":
                ushort target = Fetch16();
                ushort ret = (ushort)(Pc - 1);
                Push((byte)(ret >> 8));
                Push((byte)ret);
                Pc = target;
                break;
            case "JMP":
                ushort dest = Fetch16();
                if (info.Encoding == "Indirect")
                {
                    // NMOS: wskaźnik na granicy strony nie przenosi się do następnej.
                    dest = (ushort)(Memory[dest] | (Memory[(dest & 0xFF00) | ((dest + 1) & 0xFF)] << 8));
                }

                Pc = dest;
                break;
            case "BPL": Branch(!Flag(Negative)); break;
            case "BMI": Branch(Flag(Negative)); break;
            case "BVC": Branch(!Flag(Overflow)); break;
            case "BVS": Branch(Flag(Overflow)); break;
            case "BCC": Branch(!Flag(Carry)); break;
            case "BCS": Branch(Flag(Carry)); break;
            case "BNE": Branch(!Flag(Zero)); break;
            case "BEQ": Branch(Flag(Zero)); break;
            default: Memory_Op(m, info.Encoding); break;
        }

        if (Pc == start)
        {
            Halted = true;
        }
    }

    private static Dictionary<int, (string Mnemonic, string Encoding, int Cycles)> LoadTable()
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Repo.IsaFile("mcp_6502_instructions.json")));
        var table = new Dictionary<int, (string Mnemonic, string Encoding, int Cycles)>();
        foreach (JsonElement e in doc.RootElement.GetProperty("instructions").EnumerateArray())
        {
            table[Convert.ToInt32(e.GetProperty("opcode").GetString(), 16)] =
                (e.GetProperty("mnemonic").GetString()!, e.GetProperty("encoding").GetString()!, e.GetProperty("cycles").GetInt32());
        }

        return table;
    }

    private void Set(byte mask, bool on) => P = (byte)(on ? P | mask : P & ~mask);

    private byte Nz(byte value)
    {
        Set(Zero, value == 0);
        Set(Negative, (value & 0x80) != 0);
        return value;
    }

    private void Push(byte value) => Memory[0x100 + Sp--] = value;

    private byte Pull() => Memory[0x100 + ++Sp];

    private ushort Fetch16()
    {
        ushort value = (ushort)(Memory[Pc] | (Memory[(ushort)(Pc + 1)] << 8));
        Pc += 2;
        return value;
    }

    private void Branch(bool taken)
    {
        sbyte offset = (sbyte)Memory[Pc++];
        if (taken)
        {
            Pc = (ushort)(Pc + offset);
        }
    }

    /// <summary>Adres efektywny (albo -1 dla trybu natychmiastowego/akumulatora; Skip = liczba bajtów operandu).</summary>
    private (int Address, int Skip) Operand(string encoding)
    {
        switch (encoding)
        {
            case "Implied":
                return (-1, 0);
            case "Accumulator":
                return (-2, 0);
            case "Immediate":
                return (Pc++, 1);
            case "ZeroPage":
                return (Memory[Pc++], 1);
            case "ZeroPageX":
                return ((Memory[Pc++] + X) & 0xFF, 1);
            case "ZeroPageY":
                return ((Memory[Pc++] + Y) & 0xFF, 1);
            case "Absolute":
                return (Fetch16(), 2);
            case "AbsoluteX":
                return ((Fetch16() + X) & 0xFFFF, 2);
            case "AbsoluteY":
                return ((Fetch16() + Y) & 0xFFFF, 2);
            case "IndirectX":
                int px = (Memory[Pc++] + X) & 0xFF;
                return (Memory[px] | (Memory[(px + 1) & 0xFF] << 8), 1);
            case "IndirectY":
                int py = Memory[Pc++];
                return (((Memory[py] | (Memory[(py + 1) & 0xFF] << 8)) + Y) & 0xFFFF, 1);
            default:
                throw new NotSupportedException($"6502: addressing mode {encoding}.");
        }
    }

    private void Memory_Op(string m, string encoding)
    {
        (int address, _) = Operand(encoding);
        byte Load() => address == -2 ? A : Memory[address];
        void Store(byte v)
        {
            if (address == -2)
            {
                A = v;
            }
            else
            {
                Memory[address] = v;
            }
        }

        switch (m)
        {
            case "LDA": A = Nz(Load()); break;
            case "LDX": X = Nz(Load()); break;
            case "LDY": Y = Nz(Load()); break;
            case "STA": Store(A); break;
            case "STX": Store(X); break;
            case "STY": Store(Y); break;
            case "AND": A = Nz((byte)(A & Load())); break;
            case "ORA": A = Nz((byte)(A | Load())); break;
            case "EOR": A = Nz((byte)(A ^ Load())); break;
            case "ADC": Add(Load()); break;
            case "SBC": Add((byte)~Load()); break;
            case "CMP": Compare(A, Load()); break;
            case "CPX": Compare(X, Load()); break;
            case "CPY": Compare(Y, Load()); break;
            case "INC": Store(Nz((byte)(Load() + 1))); break;
            case "DEC": Store(Nz((byte)(Load() - 1))); break;
            case "ASL":
                byte l = Load();
                Set(Carry, (l & 0x80) != 0);
                Store(Nz((byte)(l << 1)));
                break;
            case "LSR":
                byte r = Load();
                Set(Carry, (r & 1) != 0);
                Store(Nz((byte)(r >> 1)));
                break;
            case "ROL":
                byte rl = Load();
                int carryIn = Flag(Carry) ? 1 : 0;
                Set(Carry, (rl & 0x80) != 0);
                Store(Nz((byte)((rl << 1) | carryIn)));
                break;
            case "ROR":
                byte rr = Load();
                int carryHi = Flag(Carry) ? 0x80 : 0;
                Set(Carry, (rr & 1) != 0);
                Store(Nz((byte)((rr >> 1) | carryHi)));
                break;
            case "BIT":
                byte v = Load();
                Set(Zero, (A & v) == 0);
                Set(Negative, (v & 0x80) != 0);
                Set(Overflow, (v & 0x40) != 0);
                break;
            default:
                throw new NotSupportedException($"6502: {m}.");
        }
    }

    private void Add(byte value)
    {
        if (Flag(Decimal))
        {
            throw new NotSupportedException("6502: decimal mode is not supported.");
        }

        int sum = A + value + (Flag(Carry) ? 1 : 0);
        Set(Carry, sum > 0xFF);
        Set(Overflow, (~(A ^ value) & (A ^ sum) & 0x80) != 0);
        A = Nz((byte)sum);
    }

    private void Compare(byte register, byte value)
    {
        Set(Carry, register >= value);
        Nz((byte)(register - value));
    }
}
