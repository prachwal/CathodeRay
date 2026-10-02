using CathodeRay.Assembler;
using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 33, krok 9: porównanie ze znakiem na Z80 przez S xor P/V (bez biasu <c>xor 80h</c>). Jeden program z
/// <c>BrCmp</c> na komórkach, składany raz; każda para operandów wpisana do obrazu i wykonana na emulatorze, wynik kontra C#.</summary>
public sealed class Z80SignedCompareTests
{
    private static readonly int[] WordEdges = [0x8000, 0x8001, 0xFFFF, 0, 1, 0x7FFE, 0x7FFF, 0xFFFE, 2];

    [Theory]
    [InlineData(Ir.Cond.Lt)]
    [InlineData(Ir.Cond.Le)]
    [InlineData(Ir.Cond.Gt)]
    [InlineData(Ir.Cond.Ge)]
    public void All_Byte_Pairs_Match_CSharp(Ir.Cond cond)
    {
        var bench = new Bench(cond, 1);
        var failures = new List<string>();
        for (int a = 0; a < 256; a++)
        {
            for (int b = 0; b < 256; b++)
            {
                bool expected = Expected(cond, (sbyte)a, (sbyte)b);
                if (bench.Taken(a, b) != expected)
                {
                    failures.Add($"{cond} {(sbyte)a} {(sbyte)b}: oczekiwano {expected}");
                }
            }
        }

        failures.Should().BeEmpty();
    }

    [Theory]
    [InlineData(Ir.Cond.Lt)]
    [InlineData(Ir.Cond.Le)]
    [InlineData(Ir.Cond.Gt)]
    [InlineData(Ir.Cond.Ge)]
    public void Word_Edges_And_Random_Pairs_Match_CSharp(Ir.Cond cond)
    {
        var pairs = new List<(int A, int B)>();
        foreach (int a in WordEdges)
        {
            pairs.AddRange(WordEdges.Select(b => (a, b)));
        }

        var random = new Random(33);
        for (int i = 0; i < 2000; i++)
        {
            pairs.Add((random.Next(0x10000), random.Next(0x10000)));
        }

        var bench = new Bench(cond, 2);
        var failures = new List<string>();
        foreach ((int a, int b) in pairs)
        {
            bool expected = Expected(cond, (short)a, (short)b);
            if (bench.Taken(a, b) != expected)
            {
                failures.Add($"{cond} {(short)a} {(short)b}: oczekiwano {expected}");
            }
        }

        failures.Should().BeEmpty();
    }

    private static bool Expected(Ir.Cond cond, int a, int b) => cond switch
    {
        Ir.Cond.Lt => a < b,
        Ir.Cond.Le => a <= b,
        Ir.Cond.Gt => a > b,
        _ => a >= b,
    };

    /// <summary>Złożony raz program <c>c_r = (c_a cond c_b) ? 1 : 0</c> na Z80.</summary>
    private sealed class Bench
    {
        private readonly AssemblyResult _image;
        private readonly int _start;
        private readonly int _width;

        public Bench(Ir.Cond cond, int width)
        {
            _width = width;
            List<Ir.Ins> body =
            [
                new Ir.Mov(new Ir.Cell("c_r", 1), new Ir.Imm(0, 1)),
                new Ir.BrCmp(cond, new Ir.Cell("c_a", width), new Ir.Cell("c_b", width), "taken"),
                new Ir.Jmp("end"),
                new Ir.Label("taken"),
                new Ir.Mov(new Ir.Cell("c_r", 1), new Ir.Imm(1, 1)),
                new Ir.Label("end"),
                new Ir.Ret(null, 0),
            ];
            List<Ir.Data> data =
            [
                new("c_a", "DATA", 2, [new Ir.Bytes(new byte[2])], false),
                new("c_b", "DATA", 2, [new Ir.Bytes(new byte[2])], false),
                new("c_r", "DATA", 1, [new Ir.Bytes(new byte[1])], false),
            ];
            var module = new Ir.Module([new Ir.Function("main", false, [], 0, [], body)], data, [], [], ObjectMode: false);
            ICTarget target = CTargets.All.Single(static t => t.Name == "z80");
            string code = target.Emit(module, true);
            code.Should().NotContain("cc_t0", "Z80 porównuje ze znakiem przez P/V, bez biasu").And.Contain("jp po,");
            string asm = target.Crt0 + code;
            AssemblerTarget assemblerTarget = AssemblerTargets.Find(target.AssemblerCpu)!;
            var origins = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (TargetSegment segment in target.Layout.Segments)
            {
                origins[segment.Name] = target.Layout.Areas.Single(a => a.Name == segment.Area).Start;
            }

            _image = new TwoPassAssembler(Repo.LoadTarget(assemblerTarget), assemblerTarget.DefaultSyntax)
                .Assemble(asm, "prog", _ => null, [], null, origins);
            _start = origins["CODE"];
        }

        public bool Taken(int a, int b)
        {
            var cpu = new Z80Cpu();
            _image.Image.CopyTo(cpu.Memory, _image.Origin);
            for (int i = 0; i < _width; i++)
            {
                cpu.Memory[_image.Symbols["c_a"] + i] = (byte)(a >> (8 * i));
                cpu.Memory[_image.Symbols["c_b"] + i] = (byte)(b >> (8 * i));
            }

            cpu.Pc = (ushort)_start;
            for (int steps = 0; !cpu.Halted; steps++)
            {
                if (steps > 100_000)
                {
                    throw new InvalidOperationException("program się nie zatrzymał");
                }

                cpu.Step();
            }

            return cpu.Memory[_image.Symbols["c_r"]] switch
            {
                0 => false,
                1 => true,
                _ => throw new InvalidOperationException("c_r poza 0/1"),
            };
        }
    }

    private static readonly int[] ConstEdges = [0, 1, 2, 5, 127, 128, 255, 256, 1000, 32766, 32767, -32768, -32767, -2, -1];

    public static TheoryData<Ir.Cond, int> ConstCases()
    {
        var data = new TheoryData<Ir.Cond, int>();
        foreach (Ir.Cond cond in new[] { Ir.Cond.Lt, Ir.Cond.Le, Ir.Cond.Gt, Ir.Cond.Ge })
        {
            foreach (int c in ConstEdges)
            {
                data.Add(cond, c);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ConstCases))]
    public void Signed_Const_Edges_And_Random_Match_CSharp(Ir.Cond cond, int c)
    {
        var values = new List<int> { -32768, -32767, -2, -1, 0, 1, 2, 32766, 32767 };
        foreach (int near in new[] { c - 1, c, c + 1 })
        {
            if (near is >= -32768 and <= 32767)
            {
                values.Add(near);
            }
        }

        var random = new Random(cond.GetHashCode() + c);
        for (int i = 0; i < 300; i++)
        {
            values.Add(random.Next(-32768, 32768));
        }

        var bench = new ConstBench(cond, c);
        var failures = new List<string>();
        foreach (int a in values.Distinct())
        {
            bool expected = Expected(cond, a, c);
            if (bench.Taken(a) != expected)
            {
                failures.Add($"{cond} {a} {c}: oczekiwano {expected}");
            }
        }

        failures.Should().BeEmpty();
    }

    [Theory]
    [InlineData(Ir.Cond.Lt, 5, true)]
    [InlineData(Ir.Cond.Lt, -1, true)]
    [InlineData(Ir.Cond.Lt, 0, false)]
    [InlineData(Ir.Cond.Ge, 5, true)]
    [InlineData(Ir.Cond.Ge, -1, true)]
    [InlineData(Ir.Cond.Ge, 0, false)]
    [InlineData(Ir.Cond.Le, 5, true)]
    [InlineData(Ir.Cond.Gt, -1, false)]
    public void Signed_Const_Uses_Short_Jumps_Without_Trampoline(Ir.Cond cond, int c, bool expectPe)
    {
        string code = new ConstBench(cond, c).Code;

        code.Should().NotContain("xor 128", "stała nie wymaga trampoliny S^V");
        if (expectPe)
        {
            code.Should().Contain("jp pe,");
        }
        else
        {
            code.Should().NotContain("jp pe,");
        }
    }

    /// <summary>Jak <see cref="Bench"/>, ale prawa strona to stała W2 (ścieżka bez trampoliny).</summary>
    private sealed class ConstBench
    {
        private readonly AssemblyResult _image;
        private readonly int _start;

        public ConstBench(Ir.Cond cond, int c)
        {
            List<Ir.Ins> body =
            [
                new Ir.Mov(new Ir.Cell("c_r", 1), new Ir.Imm(0, 1)),
                new Ir.BrCmp(cond, new Ir.Cell("c_a", 2), new Ir.Imm(c, 2), "taken"),
                new Ir.Jmp("end"),
                new Ir.Label("taken"),
                new Ir.Mov(new Ir.Cell("c_r", 1), new Ir.Imm(1, 1)),
                new Ir.Label("end"),
                new Ir.Ret(null, 0),
            ];
            List<Ir.Data> data =
            [
                new("c_a", "DATA", 2, [new Ir.Bytes(new byte[2])], false),
                new("c_r", "DATA", 1, [new Ir.Bytes(new byte[1])], false),
            ];
            var module = new Ir.Module([new Ir.Function("main", false, [], 0, [], body)], data, [], [], ObjectMode: false);
            ICTarget target = CTargets.All.Single(static t => t.Name == "z80");
            Code = target.Emit(module, true);
            string asm = target.Crt0 + Code;
            AssemblerTarget assemblerTarget = AssemblerTargets.Find(target.AssemblerCpu)!;
            var origins = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (TargetSegment segment in target.Layout.Segments)
            {
                origins[segment.Name] = target.Layout.Areas.Single(a => a.Name == segment.Area).Start;
            }

            _image = new TwoPassAssembler(Repo.LoadTarget(assemblerTarget), assemblerTarget.DefaultSyntax)
                .Assemble(asm, "prog", _ => null, [], null, origins);
            _start = origins["CODE"];
        }

        public string Code { get; }

        public bool Taken(int a)
        {
            var cpu = new Z80Cpu();
            _image.Image.CopyTo(cpu.Memory, _image.Origin);
            cpu.Memory[_image.Symbols["c_a"]] = (byte)a;
            cpu.Memory[_image.Symbols["c_a"] + 1] = (byte)(a >> 8);
            cpu.Pc = (ushort)_start;
            for (int steps = 0; !cpu.Halted; steps++)
            {
                if (steps > 100_000)
                {
                    throw new InvalidOperationException("program się nie zatrzymał");
                }

                cpu.Step();
            }

            return cpu.Memory[_image.Symbols["c_r"]] switch
            {
                0 => false,
                1 => true,
                _ => throw new InvalidOperationException("c_r poza 0/1"),
            };
        }
    }
}
