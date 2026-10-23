using System.Text.Json;

namespace CathodeRay.Emulation;

/// <summary>Interpreter MC6809 do testów celu. Opcode'y i formy operandów są ładowane z mcp_6809_instructions.json.</summary>
public sealed class Mc6809Cpu
{
    public const int Carry = 0x01;
    public const int Overflow = 0x02;
    public const int Zero = 0x04;
    public const int Negative = 0x08;
    public const int HalfCarry = 0x20;
    public const int IrqMask = 0x10;
    public const int FirqMask = 0x40;
    public const int Entire = 0x80;

    private static readonly Lazy<Dictionary<int, Entry>> Table = new(LoadTable);

    public byte[] Memory { get; } = new byte[0x10000];

    public byte A { get; set; }

    public byte B { get; set; }

    public ushort D
    {
        get => (ushort)((A << 8) | B);
        set
        {
            A = (byte)(value >> 8);
            B = (byte)value;
        }
    }

    public ushort X { get; set; }

    public ushort Y { get; set; }

    public ushort U { get; set; }

    public ushort S { get; set; }

    public ushort Pc { get; set; }

    public byte Dp { get; set; }

    public byte Cc { get; set; } = IrqMask | FirqMask;

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
        int opcode = Fetch8();
        if (opcode is 0x10 or 0x11)
        {
            opcode = (opcode << 8) | Fetch8();
        }

        if (!Table.Value.TryGetValue(opcode, out Entry? entry))
        {
            throw new NotSupportedException($"6809: opcode ${opcode:X4} at ${start:X4} is not supported.");
        }

        Cycles += entry.Cycles;
        Execute(entry);
    }

    private static Dictionary<int, Entry> LoadTable()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(IsaFiles.Find("mcp_6809_instructions.json")));
        var result = new Dictionary<int, Entry>();
        foreach (JsonElement instruction in document.RootElement.GetProperty("instructions").EnumerateArray())
        {
            int opcode = Convert.ToInt32(instruction.GetProperty("opcode").GetString(), 16);
            string[] mnemonic = instruction.GetProperty("mnemonic").GetString()!.Split(' ', 2);
            string mode = instruction.GetProperty("encoding").GetString()!;
            result.Add(opcode, new Entry(mnemonic[0], mode, instruction.GetProperty("cycles").GetInt32()));
        }

        return result;
    }

    private byte Fetch8()
    {
        byte value = Memory[Pc];
        Pc++;
        return value;
    }

    private ushort Fetch16() => (ushort)((Fetch8() << 8) | Fetch8());

    private ushort Read16(int address) => (ushort)((Memory[address & 0xFFFF] << 8) | Memory[(address + 1) & 0xFFFF]);

    private void Write16(int address, ushort value)
    {
        Memory[address & 0xFFFF] = (byte)(value >> 8);
        Memory[(address + 1) & 0xFFFF] = (byte)value;
    }

    private void Execute(Entry entry)
    {
        string name = entry.Mnemonic;
        if (name is "BSR" or "LBSR")
        {
            int target = entry.Mode == "relative16" ? Relative16() : Relative8();
            Push16(() => S, value => S = value, Pc);
            Pc = (ushort)target;
            return;
        }

        switch (entry.Mode)
        {
            case "relative8":
            case "relative16":
                ExecuteBranch(name, entry.Mode == "relative16");
                return;
            case "registerpair":
                ExecuteRegisterPair(name);
                return;
            case "registermask":
                ExecuteRegisterMask(name);
                return;
        }

        if (ExecuteInherent(name))
        {
            return;
        }

        if (name == "JMP")
        {
            Pc = (ushort)EffectiveAddress(entry.Mode);
            return;
        }

        if (name is "JSR")
        {
            int target = EffectiveAddress(entry.Mode);
            Push16(() => S, value => S = value, Pc);
            Pc = (ushort)target;
            return;
        }

        if (TryLoadOrStore(entry))
        {
            return;
        }

        if (TryCompare(entry))
        {
            return;
        }

        if (TryArithmetic(entry))
        {
            return;
        }

        if (TryUnary(entry))
        {
            return;
        }

        throw new NotSupportedException($"6809: instruction {name}/{entry.Mode} is not implemented.");
    }

    private bool ExecuteInherent(string name)
    {
        switch (name)
        {
            case "NOP":
                return true;
            case "SYNC":
                Halted = true;
                return true;
            case "ABX":
                X = (ushort)(X + B);
                return true;
            case "LEAX":
                X = (ushort)EffectiveAddress("indexed");
                Set(Zero, X == 0);
                return true;
            case "LEAY":
                Y = (ushort)EffectiveAddress("indexed");
                Set(Zero, Y == 0);
                return true;
            case "LEAS":
                S = (ushort)EffectiveAddress("indexed");
                return true;
            case "LEAU":
                U = (ushort)EffectiveAddress("indexed");
                return true;
            case "SEX":
                A = (B & 0x80) != 0 ? (byte)0xFF : (byte)0;
                SetNz16(D);
                Set(Overflow, false);
                return true;
            case "MUL":
            {
                ushort product = (ushort)(A * B);
                D = product;
                Set(Zero, product == 0);
                Set(Carry, (product & 0x0080) != 0);
                return true;
            }

            case "DAA":
                DecimalAdjust();
                return true;
            case "ORCC":
                Cc |= Fetch8();
                return true;
            case "ANDCC":
                Cc &= Fetch8();
                return true;
            case "RTS":
                Pc = Pull16(() => S, value => S = value);
                return true;
            case "RTI":
                ReturnFromInterrupt();
                return true;
            case "SWI":
                SoftwareInterrupt(0xFFFA, setInterruptMasks: true);
                return true;
            case "SWI2":
                SoftwareInterrupt(0xFFF4, setInterruptMasks: false);
                return true;
            case "SWI3":
                SoftwareInterrupt(0xFFF2, setInterruptMasks: false);
                return true;
            case "CWAI":
                Cc &= Fetch8();
                Cc |= Entire;
                PushAll(() => S, value => S = value);
                Halted = true;
                return true;
            case "EXG":
            case "TFR":
                ExecuteRegisterPair(name);
                return true;
        }

        if (TryFlagInstruction(name))
        {
            return true;
        }

        if (TryAccumulatorUnary(name))
        {
            return true;
        }

        return false;
    }

    private bool TryFlagInstruction(string name)
    {
        int bit = name switch
        {
            "CLC" => Carry,
            "SEC" => Carry,
            "CLV" => Overflow,
            "SEV" => Overflow,
            "CLI" => IrqMask,
            "SEI" => IrqMask,
            "CLF" => FirqMask,
            "SEF" => FirqMask,
            _ => 0,
        };
        if (bit == 0)
        {
            return false;
        }

        Set(bit, name is "SEC" or "SEV" or "SEI" or "SEF");
        return true;
    }

    private bool TryLoadOrStore(Entry entry)
    {
        string name = entry.Mnemonic;
        if (name is "LDA" or "LDB")
        {
            byte value = Read8(entry.Mode);
            if (name == "LDA")
            {
                A = value;
            }
            else
            {
                B = value;
            }

            SetNz8(value);
            Set(Overflow, false);
            return true;
        }

        if (name is "LDD" or "LDX" or "LDY" or "LDU" or "LDS")
        {
            ushort value = Read16Operand(entry.Mode);
            WriteReg16(name switch { "LDD" => 0, "LDX" => 1, "LDY" => 2, "LDU" => 3, _ => 4 }, value);
            SetNz16(value);
            Set(Overflow, false);
            return true;
        }

        if (name is "STA" or "STB")
        {
            int address = EffectiveAddress(entry.Mode);
            byte value = name == "STA" ? A : B;
            Memory[address & 0xFFFF] = value;
            SetNz8(value);
            Set(Overflow, false);
            return true;
        }

        if (name is "STD" or "STX" or "STY" or "STU" or "STS")
        {
            int address = EffectiveAddress(entry.Mode);
            ushort value = ReadReg16(name switch { "STD" => 0, "STX" => 1, "STY" => 2, "STU" => 3, _ => 4 });
            Write16(address, value);
            SetNz16(value);
            Set(Overflow, false);
            return true;
        }

        return false;
    }

    private bool TryCompare(Entry entry)
    {
        string name = entry.Mnemonic;
        if (name is "CMPA" or "CMPB")
        {
            byte left = name == "CMPA" ? A : B;
            _ = Sub8(left, Read8(entry.Mode), 0);
            return true;
        }

        int reg = name switch
        {
            "CMPD" => 0,
            "CMPX" => 1,
            "CMPY" => 2,
            "CMPU" => 3,
            "CMPS" => 4,
            _ => -1,
        };
        if (reg < 0)
        {
            return false;
        }

        _ = Sub16(ReadReg16(reg), Read16Operand(entry.Mode), 0);
        return true;
    }

    private bool TryArithmetic(Entry entry)
    {
        string name = entry.Mnemonic;
        if (name is "ADDA" or "ADCA" or "SUBA" or "SBCA" or "ANDA" or "BITA" or "EORA" or "ORA")
        {
            A = ExecuteAccumulator(name, A, Read8(entry.Mode));
            return true;
        }

        if (name is "ADDB" or "ADCB" or "SUBB" or "SBCB" or "ANDB" or "BITB" or "EORB" or "ORB")
        {
            B = ExecuteAccumulator(name, B, Read8(entry.Mode));
            return true;
        }

        if (name is "ADDD" or "SUBD")
        {
            ushort value = Read16Operand(entry.Mode);
            D = name == "ADDD" ? Add16(D, value) : Sub16(D, value, 0);
            return true;
        }

        return false;
    }

    private bool TryUnary(Entry entry)
    {
        string name = entry.Mnemonic;
        if (name is not ("NEG" or "COM" or "LSR" or "ROR" or "ASR" or "ASL" or "LSL" or "ROL" or "DEC" or "INC" or "TST" or "CLR"))
        {
            return false;
        }

        if (entry.Mode == "inherent")
        {
            return false;
        }

        int address = EffectiveAddress(entry.Mode);
        Memory[address & 0xFFFF] = Unary8(name, Memory[address & 0xFFFF], write: name != "TST");
        return true;
    }

    private bool TryAccumulatorUnary(string name)
    {
        if (name.Length != 4 || name[^1] is not ('A' or 'B'))
        {
            return false;
        }

        string operation = name[..^1];
        if (operation is not ("NEG" or "COM" or "LSR" or "ROR" or "ASR" or "ASL" or "LSL" or "ROL" or "DEC" or "INC" or "TST" or "CLR"))
        {
            return false;
        }

        if (name[^1] == 'A')
        {
            A = Unary8(operation, A, write: operation != "TST");
        }
        else
        {
            B = Unary8(operation, B, write: operation != "TST");
        }

        return true;
    }

    private byte ExecuteAccumulator(string operation, byte accumulator, byte operand)
    {
        switch (operation)
        {
            case "ADDA": case "ADDB":
                accumulator = Add8(accumulator, operand, 0);
                break;
            case "ADCA": case "ADCB":
                accumulator = Add8(accumulator, operand, Flag(Carry) ? 1 : 0);
                break;
            case "SUBA": case "SUBB":
                accumulator = Sub8(accumulator, operand, 0);
                break;
            case "SBCA": case "SBCB":
                accumulator = Sub8(accumulator, operand, Flag(Carry) ? 1 : 0);
                break;
            case "ANDA": case "ANDB":
                accumulator = (byte)(accumulator & operand);
                SetNz8(accumulator);
                Set(Overflow, false);
                break;
            case "BITA": case "BITB":
                SetNz8((byte)(accumulator & operand));
                Set(Overflow, false);
                break;
            case "EORA": case "EORB":
                accumulator = (byte)(accumulator ^ operand);
                SetNz8(accumulator);
                Set(Overflow, false);
                break;
            case "ORA": case "ORB":
                accumulator = (byte)(accumulator | operand);
                SetNz8(accumulator);
                Set(Overflow, false);
                break;
        }

        return accumulator;
    }

    private byte Unary8(string operation, byte value, bool write)
    {
        byte result = value;
        switch (operation)
        {
            case "NEG":
                result = Sub8(0, value, 0);
                Set(Carry, value != 0);
                Set(Overflow, value == 0x80);
                break;
            case "COM":
                result = (byte)~value;
                SetNz8(result);
                Set(Overflow, false);
                Set(Carry, true);
                break;
            case "LSR":
                Set(Carry, (value & 1) != 0);
                result = (byte)(value >> 1);
                SetNz8(result);
                Set(Overflow, Flag(Negative) ^ Flag(Carry));
                break;
            case "ROR":
            {
                bool carryIn = Flag(Carry);
                Set(Carry, (value & 1) != 0);
                result = (byte)((value >> 1) | (carryIn ? 0x80 : 0));
                SetNz8(result);
                Set(Overflow, Flag(Negative) ^ Flag(Carry));
                break;
            }

            case "ASR":
                Set(Carry, (value & 1) != 0);
                result = (byte)((value >> 1) | (value & 0x80));
                SetNz8(result);
                Set(Overflow, Flag(Negative) ^ Flag(Carry));
                break;
            case "ASL": case "LSL":
                Set(Carry, (value & 0x80) != 0);
                result = (byte)(value << 1);
                SetNz8(result);
                Set(Overflow, Flag(Negative) ^ Flag(Carry));
                break;
            case "ROL":
            {
                bool carryIn = Flag(Carry);
                Set(Carry, (value & 0x80) != 0);
                result = (byte)((value << 1) | (carryIn ? 1 : 0));
                SetNz8(result);
                Set(Overflow, Flag(Negative) ^ Flag(Carry));
                break;
            }

            case "DEC":
                result = (byte)(value - 1);
                SetNz8(result);
                Set(Overflow, value == 0x80);
                break;
            case "INC":
                result = (byte)(value + 1);
                SetNz8(result);
                Set(Overflow, value == 0x7F);
                break;
            case "TST":
                SetNz8(value);
                Set(Overflow, false);
                return value;
            case "CLR":
                result = 0;
                SetNz8(0);
                Set(Overflow, false);
                Set(Carry, false);
                break;
        }

        return write ? result : value;
    }

    private byte Add8(byte left, byte right, int carry)
    {
        int result = left + right + carry;
        byte value = (byte)result;
        Set(Carry, result > byte.MaxValue);
        Set(Overflow, (~(left ^ right) & (left ^ value) & 0x80) != 0);
        Set(HalfCarry, ((left & 0x0F) + (right & 0x0F) + carry) > 0x0F);
        SetNz8(value);
        return value;
    }

    private byte Sub8(byte left, byte right, int borrow)
    {
        int result = left - right - borrow;
        byte value = (byte)result;
        Set(Carry, result < 0);
        Set(Overflow, ((left ^ right) & (left ^ value) & 0x80) != 0);
        SetNz8(value);
        return value;
    }

    private ushort Add16(ushort left, ushort right)
    {
        int result = left + right;
        ushort value = (ushort)result;
        Set(Carry, result > ushort.MaxValue);
        Set(Overflow, (~(left ^ right) & (left ^ value) & 0x8000) != 0);
        SetNz16(value);
        return value;
    }

    private ushort Sub16(ushort left, ushort right, int borrow)
    {
        int result = left - right - borrow;
        ushort value = (ushort)result;
        Set(Carry, result < 0);
        Set(Overflow, ((left ^ right) & (left ^ value) & 0x8000) != 0);
        SetNz16(value);
        return value;
    }

    private byte Read8(string mode) => mode == "immediate8" ? Fetch8() : Memory[EffectiveAddress(mode) & 0xFFFF];

    private ushort Read16Operand(string mode) => mode == "immediate16" ? Fetch16() : Read16(EffectiveAddress(mode));

    private int EffectiveAddress(string mode) => mode switch
    {
        "direct" => (Dp << 8) | Fetch8(),
        "extended" => Fetch16(),
        "indexed" => IndexedAddress(),
        _ => throw new NotSupportedException($"6809: no effective-address form for {mode}."),
    };

    private int IndexedAddress()
    {
        byte postbyte = Fetch8();
        if ((postbyte & 0x80) == 0)
        {
            ushort baseAddress = ReadPointer((postbyte >> 5) & 3);
            sbyte offset = (sbyte)(((postbyte & 0x10) != 0) ? postbyte | 0xE0 : postbyte & 0x1F);
            Cycles += 1;
            return (baseAddress + offset) & 0xFFFF;
        }

        int register = (postbyte >> 5) & 3;
        bool indirect = (postbyte & 0x10) != 0;
        int mode = postbyte & 0x0F;
        ushort basis = ReadPointer(register);
        int address;
        switch (mode)
        {
            case 0x0:
                address = basis;
                WritePointer(register, (ushort)(basis + 1));
                Cycles += 2;
                break;
            case 0x1:
                address = basis;
                WritePointer(register, (ushort)(basis + 2));
                Cycles += 3;
                break;
            case 0x2:
                basis--;
                WritePointer(register, basis);
                address = basis;
                Cycles += 2;
                break;
            case 0x3:
                basis -= 2;
                WritePointer(register, basis);
                address = basis;
                Cycles += 3;
                break;
            case 0x4:
                address = basis;
                break;
            case 0x5:
                address = (basis + (sbyte)B) & 0xFFFF;
                Cycles += 1;
                break;
            case 0x6:
                address = (basis + (sbyte)A) & 0xFFFF;
                Cycles += 1;
                break;
            case 0x8:
                address = (basis + (sbyte)Fetch8()) & 0xFFFF;
                Cycles += 1;
                break;
            case 0x9:
                address = (basis + (short)Fetch16()) & 0xFFFF;
                Cycles += 4;
                break;
            case 0xB:
                address = (basis + (short)D) & 0xFFFF;
                Cycles += 4;
                break;
            case 0xC:
            {
                sbyte offset = (sbyte)Fetch8();
                address = (Pc + offset) & 0xFFFF;
                Cycles += 1;
                break;
            }

            case 0xD:
            {
                short offset = (short)Fetch16();
                address = (Pc + offset) & 0xFFFF;
                Cycles += 5;
                break;
            }

            case 0xF when indirect && (postbyte & 0x60) == 0x00:
                address = Fetch16();
                Cycles += 2;
                break;
            default:
                throw new NotSupportedException($"6809: invalid indexed postbyte ${postbyte:X2}.");
        }

        if (indirect)
        {
            Cycles += 3;
            return Read16(address);
        }

        return address;
    }

    private int Relative8()
    {
        sbyte offset = (sbyte)Fetch8();
        return (ushort)(Pc + offset);
    }

    private int Relative16()
    {
        short offset = (short)Fetch16();
        return (ushort)(Pc + offset);
    }

    private void ExecuteBranch(string name, bool longForm)
    {
        int target = longForm ? Relative16() : Relative8();
        string condition = name.StartsWith("LB", StringComparison.Ordinal) ? name[1..] : name;
        bool take = condition switch
        {
            "BRA" => true,
            "BRN" => false,
            "BCC" => !Flag(Carry),
            "BCS" => Flag(Carry),
            "BHI" => !Flag(Carry) && !Flag(Zero),
            "BLS" => Flag(Carry) || Flag(Zero),
            "BNE" => !Flag(Zero),
            "BEQ" => Flag(Zero),
            "BVC" => !Flag(Overflow),
            "BVS" => Flag(Overflow),
            "BPL" => !Flag(Negative),
            "BMI" => Flag(Negative),
            "BGE" => Flag(Negative) == Flag(Overflow),
            "BLT" => Flag(Negative) != Flag(Overflow),
            "BGT" => !Flag(Zero) && Flag(Negative) == Flag(Overflow),
            "BLE" => Flag(Zero) || Flag(Negative) != Flag(Overflow),
            _ => throw new NotSupportedException($"6809: branch {name}."),
        };
        if (take)
        {
            Pc = (ushort)target;
            Cycles++;
        }
    }

    private void ExecuteRegisterPair(string mnemonic)
    {
        byte postbyte = Fetch8();
        int source = postbyte >> 4;
        int destination = postbyte & 0x0F;
        ushort from = ReadRegister(source, out int sourceWidth);
        ushort to = ReadRegister(destination, out int destinationWidth);
        if (sourceWidth != destinationWidth)
        {
            from = sourceWidth == 1 ? (ushort)(0xFF00 | (from & 0xFF)) : (byte)from;
            to = destinationWidth == 1 ? (ushort)(0xFF00 | (to & 0xFF)) : (byte)to;
        }

        if (mnemonic == "EXG")
        {
            WriteRegister(source, to);
        }

        WriteRegister(destination, from);
    }

    private ushort ReadRegister(int code, out int width)
    {
        width = code switch { 0x8 or 0x9 or 0xA or 0xB => 1, _ => 2 };
        return code switch
        {
            0x0 => D,
            0x1 => X,
            0x2 => Y,
            0x3 => U,
            0x4 => S,
            0x5 => Pc,
            0x8 => A,
            0x9 => B,
            0xA => Cc,
            0xB => Dp,
            _ => throw new NotSupportedException($"6809: register code {code:X}."),
        };
    }

    private void WriteRegister(int code, ushort value)
    {
        switch (code)
        {
            case 0x0: D = value; break;
            case 0x1: X = value; break;
            case 0x2: Y = value; break;
            case 0x3: U = value; break;
            case 0x4: S = value; break;
            case 0x5: Pc = value; break;
            case 0x8: A = (byte)value; break;
            case 0x9: B = (byte)value; break;
            case 0xA: Cc = (byte)value; break;
            case 0xB: Dp = (byte)value; break;
            default: throw new NotSupportedException($"6809: register code {code:X}.");
        }
    }

    private ushort ReadReg16(int index) => index switch { 0 => D, 1 => X, 2 => Y, 3 => U, 4 => S, _ => throw new ArgumentOutOfRangeException(nameof(index)) };

    private void WriteReg16(int index, ushort value)
    {
        switch (index)
        {
            case 0: D = value; break;
            case 1: X = value; break;
            case 2: Y = value; break;
            case 3: U = value; break;
            case 4: S = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    private ushort ReadPointer(int register) => register switch { 0 => X, 1 => Y, 2 => U, _ => S };

    private void WritePointer(int register, ushort value)
    {
        switch (register)
        {
            case 0: X = value; break;
            case 1: Y = value; break;
            case 2: U = value; break;
            default: S = value; break;
        }
    }

    private void ExecuteRegisterMask(string mnemonic)
    {
        byte mask = Fetch8();
        bool useS = mnemonic.EndsWith('S');
        Func<ushort> getStack = useS ? () => S : () => U;
        Action<ushort> setStack = useS ? value => S = value : value => U = value;
        if (mnemonic.StartsWith("PSH", StringComparison.Ordinal))
        {
            for (int bit = 7; bit >= 0; bit--)
            {
                if ((mask & (1 << bit)) != 0)
                {
                    ushort value = ReadMaskRegister(bit, useS);
                    if (bit >= 4)
                    {
                        Push16(getStack, setStack, value);
                    }
                    else
                    {
                        Push8(getStack, setStack, (byte)value);
                    }
                }
            }
        }
        else
        {
            for (int bit = 0; bit <= 7; bit++)
            {
                if ((mask & (1 << bit)) != 0)
                {
                    ushort value = bit >= 4 ? Pull16(getStack, setStack) : Pull8(getStack, setStack);
                    WriteMaskRegister(bit, useS, value);
                }
            }
        }

        Cycles += MaskCycleAdjustment(mask);
    }

    private ushort ReadMaskRegister(int bit, bool stackS) => bit switch
    {
        7 => Pc,
        6 => stackS ? U : S,
        5 => Y,
        4 => X,
        3 => Dp,
        2 => B,
        1 => A,
        _ => Cc,
    };

    private void WriteMaskRegister(int bit, bool stackS, ushort value)
    {
        switch (bit)
        {
            case 7: Pc = value; break;
            case 6:
                if (stackS)
                {
                    U = value;
                }
                else
                {
                    S = value;
                }

                break;
            case 5: Y = value; break;
            case 4: X = value; break;
            case 3: Dp = (byte)value; break;
            case 2: B = (byte)value; break;
            case 1: A = (byte)value; break;
            default: Cc = (byte)value; break;
        }
    }

    private void PushAll(Func<ushort> getStack, Action<ushort> setStack)
    {
        for (int bit = 7; bit >= 0; bit--)
        {
            ushort value = ReadMaskRegister(bit, stackS: true);
            if (bit >= 4)
            {
                Push16(getStack, setStack, value);
            }
            else
            {
                Push8(getStack, setStack, (byte)value);
            }
        }
    }

    private void ReturnFromInterrupt()
    {
        Cc = Pull8(() => S, value => S = value);
        if ((Cc & Entire) != 0)
        {
            Cycles += 9;
            A = Pull8(() => S, value => S = value);
            B = Pull8(() => S, value => S = value);
            Dp = Pull8(() => S, value => S = value);
            X = Pull16(() => S, value => S = value);
            Y = Pull16(() => S, value => S = value);
            U = Pull16(() => S, value => S = value);
        }

        Pc = Pull16(() => S, value => S = value);
    }

    private void SoftwareInterrupt(ushort vector, bool setInterruptMasks)
    {
        Cc |= Entire;
        PushAll(() => S, value => S = value);
        if (setInterruptMasks)
        {
            Cc |= IrqMask;
            Cc |= FirqMask;
        }

        Pc = Read16(vector);
    }

    private void DecimalAdjust()
    {
        int correction = 0;
        byte low = (byte)(A & 0x0F);
        if (low > 9 || Flag(HalfCarry))
        {
            correction |= 0x06;
        }

        if (A > 0x99 || Flag(Carry) || ((A & 0xF0) > 0x80 && low > 9))
        {
            correction |= 0x60;
        }

        int result = A + correction;
        A = Nz8((byte)result);
        Set(Carry, Flag(Carry) || result > 0xFF);
    }

    private void SetNz8(byte value)
    {
        Set(Negative, (value & 0x80) != 0);
        Set(Zero, value == 0);
    }

    private void SetNz16(ushort value)
    {
        Set(Negative, (value & 0x8000) != 0);
        Set(Zero, value == 0);
    }

    private byte Nz8(byte value)
    {
        SetNz8(value);
        return value;
    }

    private void Set(int flag, bool value) => Cc = (byte)(value ? Cc | flag : Cc & ~flag);

    private ushort Pull16(Func<ushort> getStack, Action<ushort> setStack)
    {
        ushort value = (ushort)((Pull8(getStack, setStack) << 8) | Pull8(getStack, setStack));
        return value;
    }

    private byte Pull8(Func<ushort> getStack, Action<ushort> setStack)
    {
        ushort stack = getStack();
        byte value = Memory[stack];
        setStack((ushort)(stack + 1));
        return value;
    }

    private void Push16(Func<ushort> getStack, Action<ushort> setStack, ushort value)
    {
        Push8(getStack, setStack, (byte)value);
        Push8(getStack, setStack, (byte)(value >> 8));
    }

    private void Push8(Func<ushort> getStack, Action<ushort> setStack, byte value)
    {
        ushort stack = (ushort)(getStack() - 1);
        setStack(stack);
        Memory[stack] = value;
    }

    private static int MaskCycleAdjustment(byte mask)
    {
        int adjustment = 0;
        for (int bit = 0; bit < 8; bit++)
        {
            if ((mask & (1 << bit)) != 0)
            {
                adjustment += bit <= 3 ? 1 : 2;
            }
        }

        return adjustment;
    }

    private sealed record Entry(string Mnemonic, string Mode, int Cycles);
}
