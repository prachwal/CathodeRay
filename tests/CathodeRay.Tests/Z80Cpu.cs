namespace CathodeRay.Tests;

/// <summary>Interpreter podzbioru Z80 (i 8080, gdy <c>Intel8080</c>) do testów celów: rejestry główne, pełne ALU z flagami,
/// skoki, wołania, stos, CB (przesunięcia/bity), wybrane ED. Nieobsłużone opkody rzucają wyjątek. Zatrzymanie: HALT albo
/// skok do samego siebie.</summary>
public sealed class Z80Cpu
{
    public const int FlagC = 0x01;
    public const int FlagN = 0x02;
    public const int FlagP = 0x04;
    public const int FlagH = 0x10;
    public const int FlagZ = 0x40;
    public const int FlagS = 0x80;

    private readonly byte[] _r = new byte[8]; // B C D E H L (unused) A

    public Z80Cpu(bool intel8080 = false) => Intel8080 = intel8080;

    public bool Intel8080 { get; }

    public byte[] Memory { get; } = new byte[0x10000];

    public ushort Pc { get; set; }

    public ushort Sp { get; set; }

    public byte F { get; set; }

    public bool Halted { get; private set; }

    public byte A
    {
        get => _r[7];
        set => _r[7] = value;
    }

    public ushort Hl
    {
        get => (ushort)((_r[4] << 8) | _r[5]);
        set
        {
            _r[4] = (byte)(value >> 8);
            _r[5] = (byte)value;
        }
    }

    public ushort De
    {
        get => (ushort)((_r[2] << 8) | _r[3]);
        set
        {
            _r[2] = (byte)(value >> 8);
            _r[3] = (byte)value;
        }
    }

    public ushort Bc
    {
        get => (ushort)((_r[0] << 8) | _r[1]);
        set
        {
            _r[0] = (byte)(value >> 8);
            _r[1] = (byte)value;
        }
    }

    public bool Flag(int mask) => (F & mask) != 0;

    public void Step()
    {
        if (Halted)
        {
            return;
        }

        ushort start = Pc;
        int op = Fetch();
        int x = op >> 6;
        int y = (op >> 3) & 7;
        int z = op & 7;
        switch (x)
        {
            case 0:
                Group0(op, y, z);
                break;
            case 1:
                if (op == 0x76)
                {
                    Halted = true;
                    return;
                }

                SetReg(y, GetReg(z));
                break;
            case 2:
                Alu(y, GetReg(z));
                break;
            default:
                Group3(op, y, z);
                break;
        }

        if (Pc == start)
        {
            Halted = true;
        }
    }

    private static bool Parity(int value)
    {
        int bits = 0;
        for (int i = 0; i < 8; i++)
        {
            bits += (value >> i) & 1;
        }

        return (bits & 1) == 0;
    }

    private byte Fetch() => Memory[Pc++];

    private ushort Fetch16()
    {
        int lo = Fetch();
        return (ushort)(lo | (Fetch() << 8));
    }

    private byte GetReg(int r) => r == 6 ? Memory[Hl] : _r[r];

    private void SetReg(int r, byte value)
    {
        if (r == 6)
        {
            Memory[Hl] = value;
        }
        else
        {
            _r[r] = value;
        }
    }

    private ushort GetRp(int p) => p switch
    {
        0 => Bc,
        1 => De,
        2 => Hl,
        _ => Sp,
    };

    private void SetRp(int p, ushort value)
    {
        switch (p)
        {
            case 0:
                Bc = value;
                break;
            case 1:
                De = value;
                break;
            case 2:
                Hl = value;
                break;
            default:
                Sp = value;
                break;
        }
    }

    private void Push(ushort value)
    {
        Memory[--Sp] = (byte)(value >> 8);
        Memory[--Sp] = (byte)value;
    }

    private ushort Pop()
    {
        int lo = Memory[Sp++];
        return (ushort)(lo | (Memory[Sp++] << 8));
    }

    private bool Condition(int cc) => cc switch
    {
        0 => !Flag(FlagZ),
        1 => Flag(FlagZ),
        2 => !Flag(FlagC),
        3 => Flag(FlagC),
        4 => !Flag(FlagP),
        5 => Flag(FlagP),
        6 => !Flag(FlagS),
        _ => Flag(FlagS),
    };

    private void SetFlags(int value, bool carry, bool halfCarry, bool overflowOrParity, bool subtract)
    {
        F = (byte)((value & 0xFF) == 0 ? FlagZ : 0);
        F |= (byte)(value & FlagS);
        F |= (byte)(carry ? FlagC : 0);
        F |= (byte)(halfCarry ? FlagH : 0);
        F |= (byte)(overflowOrParity ? FlagP : 0);
        F |= (byte)(subtract ? FlagN : 0);
    }

    private void Alu(int kind, byte operand)
    {
        int a = A;
        int carryIn = Flag(FlagC) ? 1 : 0;
        switch (kind)
        {
            case 0:
            case 1:
            {
                int c = kind == 1 ? carryIn : 0;
                int sum = a + operand + c;
                SetFlags(sum, sum > 0xFF, ((a & 15) + (operand & 15) + c) > 15, (~(a ^ operand) & (a ^ sum) & 0x80) != 0, false);
                A = (byte)sum;
                break;
            }

            case 2:
            case 3:
            case 7:
            {
                int c = kind == 3 ? carryIn : 0;
                int diff = a - operand - c;
                SetFlags(diff, diff < 0, ((a & 15) - (operand & 15) - c) < 0, ((a ^ operand) & (a ^ diff) & 0x80) != 0, true);
                if (kind != 7)
                {
                    A = (byte)diff;
                }

                break;
            }

            case 4:
                A = (byte)(a & operand);
                SetFlags(A, false, true, Parity(A), false);
                break;
            case 5:
                A = (byte)(a ^ operand);
                SetFlags(A, false, false, Parity(A), false);
                break;
            default:
                A = (byte)(a | operand);
                SetFlags(A, false, false, Parity(A), false);
                break;
        }
    }

    private void Group0(int op, int y, int z)
    {
        int p = y >> 1;
        bool q = (y & 1) == 1;
        switch (z)
        {
            case 0:
                Relative(y);
                break;
            case 1:
                if (!q)
                {
                    SetRp(p, Fetch16());
                }
                else
                {
                    int sum = Hl + GetRp(p);
                    F = (byte)((F & ~(FlagC | FlagN)) | (sum > 0xFFFF ? FlagC : 0));
                    Hl = (ushort)sum;
                }

                break;
            case 2:
                Indirect(y);
                break;
            case 3:
                SetRp(p, (ushort)(GetRp(p) + (q ? -1 : 1)));
                break;
            case 4:
            case 5:
            {
                int before = GetReg(y);
                int after = (before + (z == 4 ? 1 : -1)) & 0xFF;
                byte keepCarry = (byte)(F & FlagC);
                SetFlags(after, false, false, z == 4 ? before == 0x7F : before == 0x80, z == 5);
                F |= keepCarry;
                SetReg(y, (byte)after);
                break;
            }

            case 6:
                SetReg(y, Fetch());
                break;
            default:
                Accumulator(op, y);
                break;
        }
    }

    private void Relative(int y)
    {
        if (Intel8080 && y != 0)
        {
            throw new NotSupportedException($"8080: opcode ${y << 3:X2} is not supported.");
        }

        switch (y)
        {
            case 0:
                break;
            case 2:
            {
                sbyte offset = (sbyte)Fetch();
                _r[0]--;
                if (_r[0] != 0)
                {
                    Pc = (ushort)(Pc + offset);
                }

                break;
            }

            case 3:
            {
                sbyte offset = (sbyte)Fetch();
                Pc = (ushort)(Pc + offset);
                break;
            }

            case >= 4:
            {
                sbyte offset = (sbyte)Fetch();
                if (Condition(y - 4))
                {
                    Pc = (ushort)(Pc + offset);
                }

                break;
            }

            default:
                throw new NotSupportedException($"Z80: EX AF,AF' at ${Pc - 1:X4}.");
        }
    }

    private void Indirect(int y)
    {
        switch (y)
        {
            case 0:
                Memory[Bc] = A;
                break;
            case 1:
                A = Memory[Bc];
                break;
            case 2:
                Memory[De] = A;
                break;
            case 3:
                A = Memory[De];
                break;
            case 4:
            {
                ushort address = Fetch16();
                Memory[address] = _r[5];
                Memory[(ushort)(address + 1)] = _r[4];
                break;
            }

            case 5:
            {
                ushort address = Fetch16();
                _r[5] = Memory[address];
                _r[4] = Memory[(ushort)(address + 1)];
                break;
            }

            case 6:
                Memory[Fetch16()] = A;
                break;
            default:
                A = Memory[Fetch16()];
                break;
        }
    }

    private void Accumulator(int op, int y)
    {
        switch (y)
        {
            case 0: // RLCA
            {
                int carry = A >> 7;
                A = (byte)((A << 1) | carry);
                F = (byte)((F & ~(FlagC | FlagN | FlagH)) | carry);
                break;
            }

            case 1: // RRCA
            {
                int carry = A & 1;
                A = (byte)((A >> 1) | (carry << 7));
                F = (byte)((F & ~(FlagC | FlagN | FlagH)) | carry);
                break;
            }

            case 2: // RLA
            {
                int carry = A >> 7;
                A = (byte)((A << 1) | (Flag(FlagC) ? 1 : 0));
                F = (byte)((F & ~(FlagC | FlagN | FlagH)) | carry);
                break;
            }

            case 3: // RRA
            {
                int carry = A & 1;
                A = (byte)((A >> 1) | (Flag(FlagC) ? 0x80 : 0));
                F = (byte)((F & ~(FlagC | FlagN | FlagH)) | carry);
                break;
            }

            case 5: // CPL
                A = (byte)~A;
                F |= FlagN | FlagH;
                break;
            case 6: // SCF
                F = (byte)((F & ~(FlagN | FlagH)) | FlagC);
                break;
            case 7: // CCF
                F = (byte)((F & ~(FlagN | FlagH | FlagC)) | (Flag(FlagC) ? 0 : FlagC));
                break;
            default:
                throw new NotSupportedException($"Z80: opcode ${op:X2} at ${Pc - 1:X4} is not supported.");
        }
    }

    private void Group3(int op, int y, int z)
    {
        int p = y >> 1;
        bool q = (y & 1) == 1;
        switch (z)
        {
            case 0:
                if (Condition(y))
                {
                    Pc = Pop();
                }

                break;
            case 1:
                if (!q)
                {
                    ushort value = Pop();
                    if (p == 3)
                    {
                        A = (byte)(value >> 8);
                        F = (byte)value;
                    }
                    else
                    {
                        SetRp(p, value);
                    }
                }
                else
                {
                    switch (p)
                    {
                        case 0:
                            Pc = Pop();
                            break;
                        case 2:
                            Pc = Hl;
                            break;
                        case 3:
                            Sp = Hl;
                            break;
                        default:
                            throw new NotSupportedException($"Z80: opcode ${op:X2} at ${Pc - 1:X4} is not supported.");
                    }
                }

                break;
            case 2:
            {
                ushort target = Fetch16();
                if (Condition(y))
                {
                    Pc = target;
                }

                break;
            }

            case 3:
                Group3Misc(op, y);
                break;
            case 4:
            {
                ushort target = Fetch16();
                if (Condition(y))
                {
                    Push(Pc);
                    Pc = target;
                }

                break;
            }

            case 5:
                if (!q)
                {
                    Push(p == 3 ? (ushort)((A << 8) | F) : GetRp(p));
                }
                else if (p == 0)
                {
                    ushort target = Fetch16();
                    Push(Pc);
                    Pc = target;
                }
                else if (p == 2 && !Intel8080)
                {
                    Extended(Fetch());
                }
                else
                {
                    throw new NotSupportedException($"Z80: opcode ${op:X2} at ${Pc - 1:X4} is not supported.");
                }

                break;
            case 6:
                Alu(y, Fetch());
                break;
            default:
                throw new NotSupportedException($"Z80: RST at ${Pc - 1:X4} is not supported.");
        }
    }

    private void Group3Misc(int op, int y)
    {
        switch (y)
        {
            case 0:
                Pc = Fetch16();
                break;
            case 1:
                if (Intel8080)
                {
                    throw new NotSupportedException($"8080: opcode ${op:X2} is not supported.");
                }

                Bits(Fetch());
                break;
            case 5:
            {
                ushort tmp = De;
                De = Hl;
                Hl = tmp;
                break;
            }

            default:
                throw new NotSupportedException($"Z80: opcode ${op:X2} at ${Pc - 1:X4} is not supported.");
        }
    }

    private void Bits(int op)
    {
        int x = op >> 6;
        int y = (op >> 3) & 7;
        int z = op & 7;
        int value = GetReg(z);
        if (x == 1)
        {
            F = (byte)((F & FlagC) | FlagH | ((value & (1 << y)) == 0 ? FlagZ : 0));
            return;
        }

        if (x == 2)
        {
            SetReg(z, (byte)(value & ~(1 << y)));
            return;
        }

        if (x == 3)
        {
            SetReg(z, (byte)(value | (1 << y)));
            return;
        }

        int carryIn = Flag(FlagC) ? 1 : 0;
        int result;
        int carry;
        switch (y)
        {
            case 0:
                carry = value >> 7;
                result = (value << 1) | carry;
                break;
            case 1:
                carry = value & 1;
                result = (value >> 1) | (carry << 7);
                break;
            case 2:
                carry = value >> 7;
                result = (value << 1) | carryIn;
                break;
            case 3:
                carry = value & 1;
                result = (value >> 1) | (carryIn << 7);
                break;
            case 4:
                carry = value >> 7;
                result = value << 1;
                break;
            case 5:
                carry = value & 1;
                result = (value >> 1) | (value & 0x80);
                break;
            case 6:
                carry = value >> 7;
                result = (value << 1) | 1;
                break;
            default:
                carry = value & 1;
                result = value >> 1;
                break;
        }

        SetFlags(result, carry == 1, false, Parity(result & 0xFF), false);
        SetReg(z, (byte)result);
    }

    private void Extended(int op)
    {
        switch (op)
        {
            case 0x42:
            case 0x52:
            case 0x62:
            case 0x72:
            {
                int rhs = GetRp((op >> 4) & 3);
                int diff = Hl - rhs - (Flag(FlagC) ? 1 : 0);
                bool overflow = ((Hl ^ rhs) & (Hl ^ diff) & 0x8000) != 0;
                SetFlags(diff >> 8, diff < 0, false, overflow, true);
                F = (byte)((F & ~FlagZ) | ((diff & 0xFFFF) == 0 ? FlagZ : 0));
                Hl = (ushort)diff;
                break;
            }

            case 0x4A:
            case 0x5A:
            case 0x6A:
            case 0x7A:
            {
                int rhs = GetRp((op >> 4) & 3);
                int sum = Hl + rhs + (Flag(FlagC) ? 1 : 0);
                bool overflow = (~(Hl ^ rhs) & (Hl ^ sum) & 0x8000) != 0;
                SetFlags(sum >> 8, sum > 0xFFFF, false, overflow, false);
                F = (byte)((F & ~FlagZ) | ((sum & 0xFFFF) == 0 ? FlagZ : 0));
                Hl = (ushort)sum;
                break;
            }

            case 0x43:
            case 0x53:
            case 0x63:
            case 0x73:
            {
                ushort address = Fetch16();
                ushort value = GetRp((op >> 4) & 3);
                Memory[address] = (byte)value;
                Memory[(ushort)(address + 1)] = (byte)(value >> 8);
                break;
            }

            case 0x4B:
            case 0x5B:
            case 0x6B:
            case 0x7B:
            {
                ushort address = Fetch16();
                SetRp((op >> 4) & 3, (ushort)(Memory[address] | (Memory[(ushort)(address + 1)] << 8)));
                break;
            }

            case 0xB0:
                do
                {
                    Memory[De] = Memory[Hl];
                    De++;
                    Hl++;
                    Bc--;
                }
                while (Bc != 0);
                F = (byte)(F & ~(FlagN | FlagH | FlagP));
                break;
            default:
                throw new NotSupportedException($"Z80: ED ${op:X2} at ${Pc - 2:X4} is not supported.");
        }
    }
}
