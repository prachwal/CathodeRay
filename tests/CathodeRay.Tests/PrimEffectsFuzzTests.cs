using System.Globalization;
using CathodeRay.Assembler;
using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 38, zadanie 4: fuzz różnicowy efektów prymitywów — model (<see cref="CpuModels"/>) prowadzi
/// symulację, prawdziwy emiter ISA drukuje kod, prawdziwy emulator go wykonuje. Porównanie: A, flagi z maski
/// modelu i bajt pamięci; żaden śledzony bit nie może zmienić się poza maską i udokumentowanymi wyjątkami.
/// Tryb binarny (6502: D=0 jak po <c>cld</c> w crt0); tylko pierwsze bajty (bez łańcuchów carry — te kryją
/// istniejące testy <c>long</c>); H/AC/parzystość i bity nieudokumentowane poza modelem z zasady.</summary>
public sealed class PrimEffectsFuzzTests
{
    private static readonly string[] Ops = ["LoadA", "StoreA", "Add", "Sub", "And", "Or", "Xor", "Cmp", "Shl", "Shr"];

    private static readonly byte[] Corners = [0, 0, 1, 2, 5, 126, 127, 128, 129, 254, 255];

    private static readonly string[] Cpus = ["6502", "z80", "8080", "6800"];

    private static readonly byte[] MiniCorners = [0, 1, 127, 128, 255];

    [Fact]
    public void Model_Effects_Match_Emulator()
    {
        var random = new Random(38);
        var counts = new Dictionary<(string Cpu, string Op), int>();
        var seen = new Dictionary<(string Cpu, string Op, string Flag), HashSet<bool>>();
        foreach (string cpu in Cpus)
        {
            foreach (string op in Ops)
            {
                foreach (byte a in MiniCorners)
                {
                    foreach (byte imm in MiniCorners)
                    {
                        RunOps(cpu, [op], [imm], a, 0, random, counts, seen);
                    }
                }
            }

            for (int s = 0; s < 2000; s++)
            {
                RunSequence(cpu, random, counts, seen);
            }
        }

        foreach (string cpu in Cpus)
        {
            foreach (string op in Ops)
            {
                counts.GetValueOrDefault((cpu, op)).Should().BeGreaterThan(50, $"prymityw {op} ma działać na {cpu}");
                foreach (string flag in CpuModels.For(cpu).PrimEffects[op].Flags)
                {
                    seen.GetValueOrDefault((cpu, op, flag), []).Should().BeEquivalentTo(ExpectedValues(cpu, op, flag), $"flaga {flag} ma się zmieniać ({cpu} {op})");
                }
            }
        }
    }

    /// <summary>Oczekiwane wartości flagi: zwykle obie; stale z definicji (N po LSR, V po AND, N stałe Z80…) tylko jedna.</summary>
    private static bool[] ExpectedValues(string cpu, string op, string flag)
    {
        bool constantFalse =
            (cpu == "6502" && op == "Shr" && flag == "N") ||
            (cpu == "6800" && op == "Shr" && (flag == "N" || flag == "S")) ||
            (cpu == "6800" && flag == "V" && (op == "LoadA" || op == "And" || op == "Or" || op == "Xor")) ||
            (cpu == "z80" && op == "Shr" && flag == "S") ||
            (cpu == "z80" && flag == "N" && op is "Add" or "And" or "Or" or "Xor" or "Shl" or "Shr") ||
            (cpu == "z80" && flag == "C" && op is "And" or "Or" or "Xor") ||
            (cpu == "8080" && flag == "CY" && op is "And" or "Or" or "Xor") ||
            (cpu == "8080" && op == "Shr" && flag == "S");
        if (constantFalse)
        {
            return [false];
        }

        bool constantTrue = cpu == "z80" && flag == "N" && (op == "Sub" || op == "Cmp");
        return constantTrue ? [true] : [false, true];
    }

    private static void RunSequence(string cpu, Random random, Dictionary<(string Cpu, string Op), int> counts, Dictionary<(string Cpu, string Op, string Flag), HashSet<bool>> seen)
    {
        int length = 1 + random.Next(6);
        var ops = new string[length];
        var imms = new byte[length];
        for (int i = 0; i < length; i++)
        {
            ops[i] = Ops[random.Next(Ops.Length)];
            imms[i] = random.Next(2) == 0 ? Corners[random.Next(Corners.Length)] : (byte)random.Next(256);
        }

        RunOps(cpu, ops, imms, Pick(random), InitialFlags(cpu, random), random, counts, seen);
    }

    private static void RunOps(string cpu, string[] ops, byte[] imms, byte a0, int f0, Random random, Dictionary<(string Cpu, string Op), int> counts, Dictionary<(string Cpu, string Op, string Flag), HashSet<bool>> seen)
    {
        _ = random;
        ByteIsa isa = CreateIsa(cpu);
        var spans = new List<int>();
        foreach ((string op, byte imm) in ops.Zip(imms))
        {
            int before = Lines(isa);
            EmitPrim(isa, op, imm);
            spans.Add(Lines(isa) - before);
        }

        AssemblyResult image = Repo.Assemble(cpu, isa.Text + "scratch:\n");
        byte a = a0;
        int flags = f0;
        byte mem = 0;
        Emulator emu = Start(cpu, image);
        SetEmu(emu, a, flags);
        int scratch = image.Symbols["scratch"];
        for (int i = 0; i < ops.Length; i++)
        {
            int emuBefore = emu.Flags();
            (a, flags) = Sim(cpu, ops[i], a, flags, imms[i]);
            if (ops[i] == "StoreA")
            {
                mem = a;
            }

            for (int s = 0; s < spans[i]; s++)
            {
                emu.Step();
            }

            CheckStep(cpu, ops[i], emu, a, flags, emuBefore, mem, scratch, seen, imms[i], string.Join(" ", ops.Zip(imms).Select(p => $"{p.First}:{p.Second:X2}")));
            counts[(cpu, ops[i])] = counts.GetValueOrDefault((cpu, ops[i])) + 1;
        }
    }

    private static int Lines(ByteIsa isa) =>
        isa.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

    private static ByteIsa CreateIsa(string cpu) => cpu switch
    {
        "z80" => new Z80Isa(),
        "8080" => new Intel8080Isa(),
        "6502" => new Mos6502Isa(),
        "6800" => new M6800Isa(),
        _ => throw new NotSupportedException(cpu),
    };

    private static void EmitPrim(ByteIsa isa, string op, byte imm)
    {
        var value = new Octet(true, imm.ToString(CultureInfo.InvariantCulture));
        switch (op)
        {
            case "LoadA": isa.LoadA(value); break;
            case "StoreA": isa.StoreA("scratch"); break;
            case "Add": isa.Alu(ByteAlu.Add, value, true); break;
            case "Sub": isa.Alu(ByteAlu.Sub, value, true); break;
            case "And": isa.Alu(ByteAlu.And, value, true); break;
            case "Or": isa.Alu(ByteAlu.Or, value, true); break;
            case "Xor": isa.Alu(ByteAlu.Xor, value, true); break;
            case "Cmp": isa.Cmp(value); break;
            case "Shl": isa.ShlA(true); break;
            case "Shr": isa.ShrA(true); break;
            default: throw new NotSupportedException(op);
        }
    }

    private sealed record Emulator(Func<byte> A, Func<int> Flags, Func<int, byte> Mem, Action Step, Action<byte> SetA, Action<int> SetFlags);

    private static Emulator Start(string cpu, AssemblyResult image)
    {
        if (cpu == "6502")
        {
            var emu = new Mos6502();
            emu.Memory.AsSpan(0, image.Image.Length).Fill(0);
            image.Image.CopyTo(emu.Memory, image.Origin);
            emu.Pc = (ushort)image.Origin;
            return new Emulator(() => emu.A, () => emu.P, addr => emu.Memory[addr], emu.Step, v => emu.A = v, f => emu.P = (byte)f);
        }

        if (cpu == "6800")
        {
            var emu = new Mc6800Cpu();
            image.Image.CopyTo(emu.Memory, image.Origin);
            emu.Pc = (ushort)image.Origin;
            return new Emulator(() => emu.A, () => emu.Cc, addr => emu.Memory[addr], emu.Step, v => emu.A = v, f => emu.Cc = (byte)f);
        }

        var z80 = new Z80Cpu(cpu == "8080");
        image.Image.CopyTo(z80.Memory, image.Origin);
        z80.Pc = (ushort)image.Origin;
        return new Emulator(() => z80.A, () => z80.F, addr => z80.Memory[addr], z80.Step, v => z80.A = v, f => z80.F = (byte)f);
    }

    private static void SetEmu(Emulator emu, byte a, int flags)
    {
        emu.SetA(a);
        emu.SetFlags(flags);
    }

    private static byte Pick(Random random) =>
        random.Next(2) == 0 ? Corners[random.Next(Corners.Length)] : (byte)random.Next(256);

    private static int InitialFlags(string cpu, Random random)
    {
        int bits = cpu switch
        {
            "6502" => 0x20 | RandomBits(random, [0x80, 0x40, 0x02, 0x01]),
            "z80" or "8080" => random.Next(256),
            "6800" => 0xC0 | RandomBits(random, [0x08, 0x04, 0x02, 0x01]),
            _ => throw new NotSupportedException(cpu),
        };
        return bits;
    }

    private static int RandomBits(Random random, int[] bits)
    {
        int result = 0;
        foreach (int bit in bits)
        {
            if (random.Next(2) == 0)
            {
                result |= bit;
            }
        }

        return result;
    }

    private static void CheckStep(string cpu, string op, Emulator emu, byte a, int flags, int before, byte mem, int scratch, Dictionary<(string Cpu, string Op, string Flag), HashSet<bool>> seen, byte imm, string seq)
    {
        emu.A().Should().Be(a, $"{cpu} {op}: A [{seq}]");
        IReadOnlySet<string> mask = CpuModels.For(cpu).PrimEffects[op].Flags;

        // Peephole cp 0 -> or a (Z80): V/N inne niż CP z definicji; kontrakt to Z/C (komentarz w Z80Isa.Operate).
        bool peephole = cpu == "z80" && op == "Cmp" && imm == 0;
        foreach (string flag in mask)
        {
            if (peephole && (flag == "V" || flag == "N"))
            {
                continue;
            }

            bool expected = (flags & Bit(cpu, flag)) != 0;
            ((emu.Flags() & Bit(cpu, flag)) != 0).Should().Be(expected, $"{cpu} {op}: flaga {flag} (a=0x{a:X2} imm=0x{imm:X2}) [{seq}]");
            if (!seen.TryGetValue((cpu, op, flag), out HashSet<bool>? values))
            {
                values = [];
                seen[(cpu, op, flag)] = values;
            }

            values.Add(expected);
        }

        emu.Mem(scratch).Should().Be(mem, $"{cpu} {op}: komórka scratch");
        int allowed = Tolerated(cpu, op);
        foreach (int bit in Tracked(cpu))
        {
            bool was = (before & bit) != 0;
            bool now = (emu.Flags() & bit) != 0;
            if (was == now)
            {
                continue;
            }

            bool inMask = mask.Any(flag => Bit(cpu, flag) == bit);
            (inMask || (allowed & bit) != 0).Should().BeTrue($"{cpu} {op}: bit 0x{bit:X2} poza maską i wyjątkami");
        }

        foreach (int bit in Static(cpu))
        {
            (emu.Flags() & bit).Should().Be(before & bit, $"{cpu} {op}: bit statyczny 0x{bit:X2}");
        }
    }

    private static int Bit(string cpu, string flag) => (cpu, flag) switch
    {
        ("6502", "N") => 0x80,
        ("6502", "V") => 0x40,
        ("6502", "Z") => 0x02,
        ("6502", "C") => 0x01,
        ("z80", "S") => 0x80,
        ("z80", "Z") => 0x40,
        ("z80", "V") => 0x04,
        ("z80", "C") => 0x01,
        ("z80", "N") => 0x02,
        ("8080", "S") => 0x80,
        ("8080", "Z") => 0x40,
        ("8080", "CY") => 0x01,
        ("6800", "N") => 0x08,
        ("6800", "Z") => 0x04,
        ("6800", "V") => 0x02,
        ("6800", "C") => 0x01,
        _ => throw new NotSupportedException($"{cpu}/{flag}"),
    };

    private static int Tolerated(string cpu, string op) => cpu switch
    {
        // Parzystość zamiast przepełnienia po AND/OR/XOR/shiftach: optymalizator jej nie czyta (poza modelem z zasady).
        "6502" => 0,
        "z80" when op is "LoadA" or "StoreA" => 0,
        "z80" when op is "And" or "Or" or "Xor" or "Shl" or "Shr" => 0x10 | 0x04,
        "z80" => 0x10,
        "8080" when op is "LoadA" or "StoreA" => 0,
        "8080" => 0x10 | 0x04,
        "6800" when op is "LoadA" or "StoreA" => 0,
        "6800" => 0x20,
        _ => 0,
    };

    private static int[] Static(string cpu) => cpu switch
    {
        "6502" => [0x04, 0x08],
        "6800" => [0x40, 0x80, 0x10],
        _ => [],
    };

    private static int[] Tracked(string cpu) => cpu switch
    {
        "6502" => [0x80, 0x40, 0x02, 0x01, 0x04, 0x08],
        "z80" => [0x80, 0x40, 0x10, 0x04, 0x02, 0x01],
        "8080" => [0x80, 0x40, 0x10, 0x04, 0x01],
        "6800" => [0x08, 0x04, 0x02, 0x01, 0x20, 0x40, 0x80, 0x10],
        _ => [],
    };

    private static (byte A, int Flags) Sim(string cpu, string op, byte a, int f, byte imm) => cpu switch
    {
        "6502" => Sim6502(op, a, f, imm),
        "z80" => SimZ80(op, a, f, imm),
        "8080" => Sim8080(op, a, f, imm),
        "6800" => Sim6800(op, a, f, imm),
        _ => throw new NotSupportedException(cpu),
    };

    private static int Put(int flags, int mask, bool value) => value ? flags | mask : flags & ~mask;

    private static bool OvAdd(byte a, byte b, byte r) => ((a ^ r) & (b ^ r) & 0x80) != 0;

    private static bool OvSub(byte a, byte b, byte r) => ((a ^ b) & (a ^ r) & 0x80) != 0;

    private static (byte A, int Flags) Sim6502(string op, byte a, int f, byte imm)
    {
        bool N(byte v) => (v & 0x80) != 0;
        bool Z(byte v) => v == 0;
        return op switch
        {
            "LoadA" => (imm, Put(Put(f, 0x80, N(imm)), 0x02, Z(imm))),
            "StoreA" => (a, f),
            "Add" => Add6502(a, f, imm),
            "Sub" => Sub6502(a, f, imm),
            "And" => Logic6502(f, (byte)(a & imm)),
            "Or" => Logic6502(f, (byte)(a | imm)),
            "Xor" => Logic6502(f, (byte)(a ^ imm)),
            "Cmp" => Cmp6502(a, f, imm),
            "Shl" => Shift6502(a, f, true),
            "Shr" => Shift6502(a, f, false),
            _ => throw new NotSupportedException(op),
        };

        (byte A, int Flags) Add6502(byte x, int fl, byte m)
        {
            int r = x + m;
            bool c = r > 0xFF;
            byte v = (byte)r;
            return (v, Put(Put(Put(Put(fl, 0x01, c), 0x02, Z(v)), 0x80, N(v)), 0x40, OvAdd(x, m, v)));
        }

        (byte A, int Flags) Sub6502(byte x, int fl, byte m)
        {
            int r = x - m;
            bool c = x >= m;
            byte v = (byte)r;
            return (v, Put(Put(Put(Put(fl, 0x01, c), 0x02, Z(v)), 0x80, N(v)), 0x40, OvSub(x, m, v)));
        }

        (byte A, int Flags) Logic6502(int fl, byte v) => (v, Put(Put(fl, 0x80, N(v)), 0x02, Z(v)));

        (byte A, int Flags) Cmp6502(byte x, int fl, byte m)
        {
            byte t = (byte)(x - m);
            return (x, Put(Put(Put(fl, 0x01, x >= m), 0x02, Z(t)), 0x80, N(t)));
        }

        (byte A, int Flags) Shift6502(byte x, int fl, bool left)
        {
            bool c = left ? (x & 0x80) != 0 : (x & 0x01) != 0;
            byte v = left ? (byte)(x << 1) : (byte)(x >> 1);
            return (v, Put(Put(Put(fl, 0x01, c), 0x02, Z(v)), 0x80, N(v)));
        }
    }

    private static (byte A, int Flags) SimZ80(string op, byte a, int f, byte imm)
    {
        bool S(byte v) => (v & 0x80) != 0;
        bool Z(byte v) => v == 0;
        return op switch
        {
            "LoadA" => (imm, f),
            "StoreA" => (a, f),
            "Add" => ArithZ80(a, f, imm, true),
            "Sub" => ArithZ80(a, f, imm, false),
            "And" => LogicZ80(f, (byte)(a & imm)),
            "Or" => LogicZ80(f, (byte)(a | imm)),
            "Xor" => LogicZ80(f, (byte)(a ^ imm)),
            "Cmp" => CmpZ80(a, f, imm),
            "Shl" => ShiftZ80(a, f, true),
            "Shr" => ShiftZ80(a, f, false),
            _ => throw new NotSupportedException(op),
        };

        (byte A, int Flags) ArithZ80(byte x, int fl, byte m, bool add)
        {
            int r = add ? x + m : x - m;
            bool c = add ? r > 0xFF : x < m;
            byte v = (byte)r;
            fl = Put(fl, 0x01, c);
            fl = Put(fl, 0x02, !add);
            fl = Put(fl, 0x04, add ? OvAdd(x, m, v) : OvSub(x, m, v));
            fl = Put(fl, 0x40, Z(v));
            return (v, Put(fl, 0x80, S(v)));
        }

        (byte A, int Flags) LogicZ80(int fl, byte v)
        {
            fl = Put(fl, 0x01, false);
            fl = Put(fl, 0x02, false);
            fl = Put(fl, 0x40, Z(v));
            return (v, Put(fl, 0x80, S(v)));
        }

        (byte A, int Flags) CmpZ80(byte x, int fl, byte m)
        {
            int r = x - m;
            byte v = (byte)r;
            fl = Put(fl, 0x01, x < m);
            fl = Put(fl, 0x02, true);
            fl = Put(fl, 0x04, OvSub(x, m, v));
            fl = Put(fl, 0x40, Z(v));
            return (x, Put(fl, 0x80, S(v)));
        }

        (byte A, int Flags) ShiftZ80(byte x, int fl, bool left)
        {
            bool c = left ? (x & 0x80) != 0 : (x & 0x01) != 0;
            byte v = left ? (byte)(x << 1) : (byte)(x >> 1);
            fl = Put(fl, 0x01, c);
            fl = Put(fl, 0x02, false);
            fl = Put(fl, 0x40, Z(v));
            return (v, Put(fl, 0x80, left && S(v)));
        }
    }

    private static (byte A, int Flags) Sim8080(string op, byte a, int f, byte imm)
    {
        bool S(byte v) => (v & 0x80) != 0;
        bool Z(byte v) => v == 0;
        return op switch
        {
            "LoadA" => (imm, f),
            "StoreA" => (a, f),
            "Add" => Arith8080(a, f, imm, true),
            "Sub" => Arith8080(a, f, imm, false),
            "And" => Logic8080(f, (byte)(a & imm)),
            "Or" => Logic8080(f, (byte)(a | imm)),
            "Xor" => Logic8080(f, (byte)(a ^ imm)),
            "Cmp" => Cmp8080(a, f, imm),
            "Shl" => Shl8080(a, f),
            "Shr" => Shr8080(a, f),
            _ => throw new NotSupportedException(op),
        };

        (byte A, int Flags) Arith8080(byte x, int fl, byte m, bool add)
        {
            int r = add ? x + m : x - m;
            bool c = add ? r > 0xFF : x < m;
            byte v = (byte)r;
            fl = Put(fl, 0x01, c);
            fl = Put(fl, 0x40, Z(v));
            return (v, Put(fl, 0x80, S(v)));
        }

        (byte A, int Flags) Logic8080(int fl, byte v)
        {
            fl = Put(fl, 0x01, false);
            fl = Put(fl, 0x40, Z(v));
            return (v, Put(fl, 0x80, S(v)));
        }

        (byte A, int Flags) Cmp8080(byte x, int fl, byte m)
        {
            int r = x - m;
            byte v = (byte)r;
            fl = Put(fl, 0x01, x < m);
            fl = Put(fl, 0x40, Z(v));
            return (x, Put(fl, 0x80, S(v)));
        }

        (byte A, int Flags) Shl8080(byte x, int fl)
        {
            int r = x + x;
            byte v = (byte)r;
            fl = Put(fl, 0x01, r > 0xFF);
            fl = Put(fl, 0x40, Z(v));
            return (v, Put(fl, 0x80, S(v)));
        }

        (byte A, int Flags) Shr8080(byte x, int fl)
        {
            // ora a (S,Z,CY=0), potem rar z CY=0: wynik A>>1, CY=bit0
            (byte o, int of) = Logic8080(fl, x);
            bool c = (o & 0x01) != 0;
            byte v = (byte)(o >> 1);
            of = Put(of, 0x01, c);
            of = Put(of, 0x40, Z(v));
            return (v, Put(of, 0x80, S(v)));
        }
    }

    private static (byte A, int Flags) Sim6800(string op, byte a, int f, byte imm)
    {
        bool N(byte v) => (v & 0x80) != 0;
        bool Z(byte v) => v == 0;
        return op switch
        {
            "LoadA" => (imm, Put(Put(Put(f, 0x08, N(imm)), 0x04, Z(imm)), 0x02, false)),
            "StoreA" => (a, f),
            "Add" => Arith6800(a, f, imm, true),
            "Sub" => Arith6800(a, f, imm, false),
            "And" => Logic6800(f, (byte)(a & imm)),
            "Or" => Logic6800(f, (byte)(a | imm)),
            "Xor" => Logic6800(f, (byte)(a ^ imm)),
            "Cmp" => Cmp6800(a, f, imm),
            "Shl" => Shl6800(a, f),
            "Shr" => Shr6800(a, f),
            _ => throw new NotSupportedException(op),
        };

        (byte A, int Flags) Arith6800(byte x, int fl, byte m, bool add)
        {
            int r = add ? x + m : x - m;
            bool c = add ? r > 0xFF : x < m;
            byte v = (byte)r;
            fl = Put(fl, 0x01, c);
            fl = Put(fl, 0x02, add ? OvAdd(x, m, v) : OvSub(x, m, v));
            fl = Put(fl, 0x04, Z(v));
            return (v, Put(fl, 0x08, N(v)));
        }

        (byte A, int Flags) Logic6800(int fl, byte v)
        {
            fl = Put(fl, 0x02, false);
            fl = Put(fl, 0x04, Z(v));
            return (v, Put(fl, 0x08, N(v)));
        }

        (byte A, int Flags) Cmp6800(byte x, int fl, byte m)
        {
            int r = x - m;
            byte v = (byte)r;
            fl = Put(fl, 0x01, x < m);
            fl = Put(fl, 0x02, OvSub(x, m, v));
            fl = Put(fl, 0x04, Z(v));
            return (x, Put(fl, 0x08, N(v)));
        }

        (byte A, int Flags) Shl6800(byte x, int fl)
        {
            bool c = (x & 0x80) != 0;
            byte v = (byte)(x << 1);
            fl = Put(fl, 0x01, c);
            fl = Put(fl, 0x04, Z(v));
            bool n = N(v);
            fl = Put(fl, 0x02, n ^ c);
            return (v, Put(fl, 0x08, n));
        }

        (byte A, int Flags) Shr6800(byte x, int fl)
        {
            bool c = (x & 0x01) != 0;
            byte v = (byte)(x >> 1);
            fl = Put(fl, 0x01, c);
            fl = Put(fl, 0x04, Z(v));
            fl = Put(fl, 0x02, c);
            return (v, Put(fl, 0x08, false));
        }
    }
}
