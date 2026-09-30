using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 33, krok 15: <see cref="RegisterAllocator"/> na Z80. Kontrprzykłady z pyt. 2 analizy
/// (<c>docs/z80-register-optimizer-analysis.md</c>) jako programy C z konsolą liczoną ręcznie, na każdym celu z runnerem, oraz
/// testy, że alokator faktycznie przydziela rejestry (inaczej zielone wyrocznie nic nie mówią).</summary>
public sealed class RegisterAllocatorTests
{
    private const string Io = "void putchar(uchar c);\nvoid putdec(int v);\n";

    public static TheoryData<string> Targets()
    {
        var data = new TheoryData<string>();
        foreach (string name in TargetHarness.Targets.Select(static t => t.Name))
        {
            data.Add(name);
        }

        return data;
    }

    // 1. lokalna static w liściu wołanym 3×: n żywa na wejściu (czytana przed zapisem), więc zostaje w pamięci
    [Theory]
    [MemberData(nameof(Targets))]
    public void Static_Local_Counter_Survives_Calls(string cpu)
    {
        const string Source = """
            int tick() { static int n; int k; k = n + 1; n = k; return k * 10; }
            int main() { tick(); tick(); putdec(tick()); return 0; }
            """;

        // n: 1, 2, 3 → 30
        CcRun.RunOn(Io + Source, cpu).Console.Should().Be("30");
    }

    // 2. zmienna modułu (static, nieeksportowana) używana tylko w jednej funkcji i czytana przed zapisem
    [Theory]
    [MemberData(nameof(Targets))]
    public void Module_Cell_Read_Before_Write_Keeps_Value(string cpu)
    {
        const string Source = """
            static int g;
            int step() { g = g + 5; return g; }
            int main() { step(); step(); putdec(step()); return 0; }
            """;

        // g: 5, 10, 15
        CcRun.RunOn(Io + Source, cpu).Console.Should().Be("15");
    }

    // 3. x zdefiniowane przed pętlą, czytane na początku ciała, wołanie na końcu ciała (życie przez krawędź wsteczną)
    [Theory]
    [MemberData(nameof(Targets))]
    public void Value_Live_Around_Back_Edge_Across_Call(string cpu)
    {
        const string Source = """
            int id(int v) { return v; }
            int main() {
                int x; int i; int s;
                x = 7; s = 0; i = 0;
                while (i < 3) { s = s + x; i = i + 1; x = x + id(i); }
                putdec(s); putchar(' '); putdec(x);
                return 0;
            }
            """;

        // i=1: s=7, x=8; i=2: s=15, x=10; i=3: s=25, x=13
        CcRun.RunOn(Io + Source, cpu).Console.Should().Be("25 13");
    }

    // 4a. goto w przód omijające definicję po wywołaniu (błąd Frames z analizy)
    [Theory]
    [MemberData(nameof(Targets))]
    public void Forward_Goto_Skips_Definition_After_Call(string cpu)
    {
        const string Source = """
            int g(int n) { int x; x = n * 3; if (n == 0) return 0; g(n - 1);
                           if (n & 1) goto skip; x = 100; skip: return x; }
            int main() { putdec(g(1)); putdec(g(3)); return 0; }
            """;

        // g(1): x = 3, g(0), n nieparzyste → 3; g(3): x = 9 → 9 (g(2) zwraca 100, ale jego x to inna aktywacja)
        CcRun.RunOn(Io + Source, cpu).Console.Should().Be("39");
    }

    // 4b. goto do wnętrza pętli
    [Theory]
    [MemberData(nameof(Targets))]
    public void Goto_Into_Loop_Body(string cpu)
    {
        const string Source = """
            int main() {
                int i; int s;
                s = 0; i = 5;
                goto mid;
            top:
                s = s + i;
            mid:
                i = i + 1;
                if (i < 8) goto top;
                putdec(s); putchar(' '); putdec(i);
                return 0;
            }
            """;

        // i = 6 → s = 6; i = 7 → s = 13; i = 8 → koniec
        CcRun.RunOn(Io + Source, cpu).Console.Should().Be("13 8");
    }

    // 5. rekurencja z lokalną żywą przez wywołanie (fib) i wariant z goto
    [Theory]
    [MemberData(nameof(Targets))]
    public void Recursion_Keeps_Locals_Live_Across_Calls(string cpu)
    {
        const string Source = """
            int fib(int n) { int a; if (n < 2) return n; a = fib(n - 1); return a + fib(n - 2); }
            int f(int n) { int t; int u; if (n == 0) return 1; t = n * 2; u = f(n - 1); if (t > 4) goto big; return t + u; big: return t - u; }
            int main() { putdec(fib(10)); putchar(' '); putdec(f(4)); return 0; }
            """;

        // fib(10) = 55; f(0) = 1, f(1) = 2 + 1 = 3, f(2) = 4 + 3 = 7, f(3) = 6 - 7 = -1, f(4) = 8 - (-1) = 9
        CcRun.RunOn(Io + Source, cpu).Console.Should().Be("55 9");
    }

    // 6. wołanie przez wskaźnik z wartościami żywymi przed i po wywołaniu
    [Theory]
    [MemberData(nameof(Targets))]
    public void Indirect_Call_Keeps_Live_Values(string cpu)
    {
        const string Source = """
            int twice(int v) { return v + v; }
            int apply(int (*f)(int), int v) { int w; w = v + 1; return f(v) + w; }
            int main() { int k; int r; k = 5; r = apply(twice, k); putdec(r + k); return 0; }
            """;

        // apply: w = 6, twice(5) = 10 → 16; 16 + 5 = 21
        CcRun.RunOn(Io + Source, cpu).Console.Should().Be("21");
    }

    // 7. mnożenie w pętli z licznikiem (procedura wykonawcza niszczy BC/DE)
    [Theory]
    [MemberData(nameof(Targets))]
    public void Multiply_In_Loop_Keeps_Counter(string cpu)
    {
        const string Source = """
            int scale(int a) { int i; int s; s = 0; for (i = 1; i <= 4; i = i + 1) { s = s + a * i; } return s; }
            int main() { putdec(scale(3)); return 0; }
            """;

        // 3 * (1 + 2 + 3 + 4) = 30
        CcRun.RunOn(Io + Source, cpu).Console.Should().Be("30");
    }

    // 8. pole struktury z przesunięciem ≥ 4 przez wskaźnik, gdy obie pary trzymają inne wartości
    [Theory]
    [MemberData(nameof(Targets))]
    public void Far_Field_Through_Pointer_Keeps_Registers(string cpu)
    {
        const string Source = """
            struct S { int a; int b; int c; int d; };
            struct S s;
            int sum(struct S *p) { int k; int q; k = 7; q = p->c + k; q = q + p->d; return q + k; }
            int main() { s.c = 100; s.d = 1000; putdec(sum(&s)); return 0; }
            """;

        // q = 100 + 7 = 107; q = 107 + 1000 = 1107; 1107 + 7 = 1114
        CcRun.RunOn(Io + Source, cpu).Console.Should().Be("1114");
    }

    // 9. zapis części komórki: (uchar) na 2-bajtowej, potem odczyt całej
    [Theory]
    [MemberData(nameof(Targets))]
    public void Narrowing_Then_Widening_Keeps_Value(string cpu)
    {
        const string Source = """
            int main() { int x; uchar c; x = 700; c = (uchar)x; x = c; x = x + 1; putdec(x); return 0; }
            """;

        // 700 = 0x2BC → 0xBC = 188 → 189
        CcRun.RunOn(Io + Source, cpu).Console.Should().Be("189");
    }

    // 10. funkcja wstawiona w inną i jednocześnie eksportowana (komórki w dwóch funkcjach)
    [Theory]
    [MemberData(nameof(Targets))]
    public void Inlined_And_Exported_Function_Stays_Correct(string cpu)
    {
        const string Source = """
            int bump(int x) { int y; y = x + 1; return y + y; }
            int (*fp)(int);
            int main() { int a; a = bump(3); fp = bump; putdec(a); putchar(' '); putdec(bump(a)); putchar(' '); putdec(fp(1)); return 0; }
            """;

        // bump(3) = 8; bump(8) = 18; bump(1) = 4
        CcRun.RunOn(Io + Source, cpu).Console.Should().Be("8 18 4");
    }

    // 11. lokalna volatile
    [Theory]
    [MemberData(nameof(Targets))]
    public void Volatile_Local_Stays_In_Memory(string cpu)
    {
        const string Source = """
            int main() { volatile int v; int i; v = 0; for (i = 0; i < 5; i = i + 1) { v = v + i; } putdec(v); return 0; }
            """;

        // 0 + 1 + 2 + 3 + 4 = 10
        CcRun.RunOn(Io + Source, cpu).Console.Should().Be("10");
    }

    // 12. long (połówki x, x+2) w pętli
    [Theory]
    [MemberData(nameof(Targets))]
    public void Long_In_Loop(string cpu)
    {
        const string Source = """
            int main() { long acc; int i; acc = 0; i = 0; while (i < 4) { acc = acc + 70000L; i = i + 1; } putdec((int)(acc - 279000L)); putchar(' '); putdec((int)(acc >> 16)); return 0; }
            """;

        // 4 × 70000 = 280000 = 0x445C0: 280000 - 279000 = 1000; 280000 >> 16 = 4
        CcRun.RunOn(Io + Source, cpu).Console.Should().Be("1000 4");
    }

    // 13. dwie komórki o rozłącznym życiu w jednym rejestrze i Mov y ← x (pamięć akumulatora w selektorze)
    [Theory]
    [MemberData(nameof(Targets))]
    public void Disjoint_Cells_Share_A_Register(string cpu)
    {
        const string Source = """
            int main() {
                uchar x; uchar y; int a; int b;
                x = 40; y = x; y = y + y; putdec(y); putchar(' ');
                a = 1000; b = a + 1; b = b + b; putdec(b);
                return 0;
            }
            """;

        // y = 80; b = 1001 + 1001 = 2002
        CcRun.RunOn(Io + Source, cpu).Console.Should().Be("80 2002");
    }

    // 14. p = p->next: wskaźnik i wynik w tej samej komórce (mustCopy), p w parze rejestrów
    [Theory]
    [MemberData(nameof(Targets))]
    public void Pointer_Chasing_Into_Same_Cell(string cpu)
    {
        const string Source = """
            struct N { struct N *next; int v; };
            struct N a; struct N b; struct N c;
            int main() {
                struct N *p; int s;
                a.next = &b; b.next = &c; c.next = 0; a.v = 1; b.v = 20; c.v = 300;
                s = 0; p = &a;
                while (p) { s = s + p->v; p = p->next; }
                putdec(s);
                return 0;
            }
            """;

        // 1 + 20 + 300 = 321
        CcRun.RunOn(Io + Source, cpu).Console.Should().Be("321");
    }

    // 5–7 na Z80 i 8080 (plan 33, krok 18): komórka żywa przez wołanie dostaje rejestr, a jej para idzie na stos wokół wołania
    [Theory]
    [InlineData("z80", "bc|de")]
    [InlineData("8080", "b|d")]
    public void Live_Across_Call_Cells_Are_Pushed_Around_The_Call(string cpu, string pairs)
    {
        // 5: a żywe przez drugie fib, t przez f(n - 1) (w ramce rekurencji, teraz w rejestrze); fib(10) = 55, f(4) = 9 jak wyżej
        const string Recursion = """
            int fib(int n) { int a; if (n < 2) return n; a = fib(n - 1); return a + fib(n - 2); }
            int f(int n) { int t; int u; if (n == 0) return 1; t = n * 2; u = f(n - 1); if (t > 4) goto big; return t + u; big: return t - u; }
            int main() { putdec(fib(10)); putchar(' '); putdec(f(4)); return 0; }
            """;

        // 6: w żywe przez wołanie pośrednie; w = 6, w = 12, twice(5) = 10 → 10 + 12 + 12 = 34; 34 + 5 = 39
        const string Indirect = """
            int twice(int v) { return v + v; }
            int apply(int (*f)(int), int v) { int w; w = v + 1; w = w + w; return f(v) + w + w; }
            int main() { int k; int r; k = 5; r = apply(twice, k); putdec(r + k); return 0; }
            """;

        // 7: licznik i żywy przez __cc_mul w pętli; 3 * (1 + 2 + 3 + 4) = 30
        const string Multiply = """
            int main() { int a; int i; int s; a = 3; s = 0; for (i = 1; i <= 4; i = i + 1) { s = s + a * i; } putdec(s); return 0; }
            """;

        // wołanie pośrednie ładuje adres celu do HL między push a call
        ICTarget target = CTargets.Find(cpu)!;
        foreach ((string source, string callee, string expected) in new[] { (Recursion, "fib", "55 9"), (Recursion, "f", "55 9"), (Indirect, "__callhl", "39"), (Multiply, "__cc_mul", "30") })
        {
            string code = target.Emit(Codegen.Lower(TypeChecker.Check(Parser.Parse(Io + source)), "t.c", objectMode: true), optimize: true);
            code.Should().MatchRegex($@"push ({pairs})\r?\n(?:(?:ld hl,|lhld )[^\n]*\n)?call {callee}\r?\npop \1\r?\n", $"{cpu}: para żywa przez wołanie {callee}");
            CcRun.RunOn(Io + source, cpu).Console.Should().Be(expected);
        }

        target.Emit(Codegen.Lower(TypeChecker.Check(Parser.Parse(Io + Recursion)), "t.c", objectMode: true), optimize: true)
            .Should().NotContain("fib__a", "komórka w rejestrze nie jest zapisywana w ramce");
    }

    [Fact]
    public void Z80_Puts_Loop_Cells_In_Registers_Only_When_Optimizing()
    {
        const string Source = """
            uint sum_bytes(uchar *p, uchar n) { uint s; s = 0; while (n) { s = s + *p; p++; n--; } return s; }
            """;
        Ir.Module module = Codegen.Lower(TypeChecker.Check(Parser.Parse(Source)), "t.c", objectMode: true);
        ICTarget z80 = CTargets.Find("z80")!;

        string tuned = z80.Emit(module, optimize: true);
        tuned.Should().NotContain("sum_bytes__s").And.Contain("ld bc,0").And.Contain("ld a,c").And.Contain("ld c,a")
            .And.Contain("ld a,b").And.Contain("ld b,a").And.Contain("ld (cc_ret),bc");
        z80.Emit(module, optimize: false).Should().Contain("sum_bytes__s").And.NotContain("ld a,c");
    }

    [Fact]
    public void Conformance_Cell_C_A_Gets_A_Register()
    {
        // c_a: zapis przed odczytem, jedna funkcja, bez wołań — rejestr C; wynik zgodny z wyrocznią na wszystkich celach
        var program = new IrProgram();
        string loop = program.NewLabel();
        program.Emit(
            new Ir.Mov(program.A(1), new Ir.Imm(0, 1)),
            new Ir.Mov(program.R(1), new Ir.Imm(0, 1)),
            new Ir.Label(loop),
            new Ir.Bin(Ir.BinOp.Add, program.R(1), program.R(1), program.A(1)),
            new Ir.Bin(Ir.BinOp.Add, program.A(1), program.A(1), new Ir.Imm(1, 1)),
            new Ir.BrCmp(Ir.Cond.Ltu, program.A(1), new Ir.Imm(5, 1), loop));
        program.Record(program.R(1), "0 + 1 + 2 + 3 + 4");
        string code = CTargets.Find("z80")!.Emit(program.Build(), optimize: true);

        code.Should().NotContain("c_a").And.Contain("inc c").And.Contain("ld a,c");
        TargetHarness.Run(CTargets.Find("z80")!, program.Build()).Read("c_res", 1).Should().Equal(10);
        program.AssertConforms("c_a w rejestrze");
    }

    [Fact]
    public void Partial_Write_Of_A_Register_Word_Keeps_The_Other_Byte()
    {
        // c_a = 0x1234; młodszy bajt ← 0x56 → 0x1256; c_b: najpierw zapis części (żywa na wejściu), zostaje w pamięci
        var program = new IrProgram();
        program.Emit(
            new Ir.Mov(program.A(2), new Ir.Imm(0x1234, 2)),
            new Ir.Mov(program.A(1), new Ir.Imm(0x56, 1)),
            new Ir.Bin(Ir.BinOp.Add, program.R(2), program.A(2), new Ir.Imm(1, 2)),
            new Ir.Mov(program.B(1), new Ir.Imm(0x78, 1)),
            new Ir.Bin(Ir.BinOp.Add, program.B(2), program.B(2), new Ir.Imm(0x100, 2)));
        program.Record(program.R(2), "0x1256 + 1").Record(program.B(2), "0x0078 + 0x100");
        string code = CTargets.Find("z80")!.Emit(program.Build(), optimize: true);

        code.Should().NotContain("c_a").And.Contain("c_b");

        // zapis części komórki to bajt spod jej adresu: młodszy tylko na celach little-endian (na 6800 starszy), więc bez 6800
        foreach (ICTarget target in TargetHarness.Targets.Where(static t => t.ByteOrder == TargetByteOrder.Little))
        {
            foreach (bool optimize in new[] { true, false })
            {
                byte[] res = TargetHarness.Run(target, program.Build(), optimize).Read("c_res", 6);
                res[..2].Should().Equal([0x57, 0x12], $"{target.Name}, optimize={optimize}");
                res[4..6].Should().Equal([0x78, 0x01], $"{target.Name}, optimize={optimize}");
            }
        }
    }
}
