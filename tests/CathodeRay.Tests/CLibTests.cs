using CathodeRay.Assembler;
using CathodeRay.C;
using CathodeRay.Stub;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Testy biblioteki stub (samples/stub/lib): sterowniki asm + wołania z C.</summary>
public sealed class CLibTests
{
    private static string LibDir() => Repo.Path("samples", "stub", "lib");

    private static (StubCpu Cpu, StubBus Bus, AssemblyResult Result) RunAsm(string source, string name)
    {
        AssemblerTarget target = AssemblerTargets.Find("stub")!;
        string entry = Path.Combine(LibDir(), name);
        static string? Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
        var origins = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["CODE"] = 0x1000,
            ["DATA"] = 0x2000,
        };
        AssemblyResult result = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax)
            .Assemble(source, entry, Read, [], null, origins);
        var bus = new StubBus();
        for (int i = 0; i < result.Image.Length; i++)
        {
            bus.Write((ushort)(result.Origin + i), result.Image[i]);
        }

        StubIsa isa = StubIsa.FromJsonFile(Repo.IsaFile("mcp_stub_instructions.json"));
        var cpu = new StubCpu(isa, bus);
        cpu.State.ProgramCounter = 0x1000;
        for (int steps = 0; !cpu.State.Halted; steps++)
        {
            steps.Should().BeLessThan(10_000, "program ma się zatrzymać");
            cpu.Step();
        }

        return (cpu, bus, result);
    }

    private static (StubCpu Cpu, StubBus Bus, AssemblyResult Result) RunCWithLib(string source, string extraC = "", string extraS = "")
    {
        string io = File.ReadAllText(Path.Combine(LibDir(), "io.s"));
        CheckedProgram checkedProgram = TypeChecker.Check(Parser.Parse(source + extraC));
        string asm = Crt0.Source + io + extraS + Codegen.Emit(checkedProgram);
        AssemblerTarget target = AssemblerTargets.Find("stub")!;
        var origins = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["CODE"] = 0x1000,
            ["INIT"] = 0x6F00,
            ["BSS"] = 0x7000,
            ["DATA"] = 0x8000,
        };
        AssemblyResult result = new TwoPassAssembler(Repo.LoadTarget(target), target.DefaultSyntax)
            .Assemble(asm, "prog.c", _ => null, [], null, origins);
        var bus = new StubBus();
        for (int i = 0; i < result.Image.Length; i++)
        {
            bus.Write((ushort)(result.Origin + i), result.Image[i]);
        }

        StubIsa isa = StubIsa.FromJsonFile(Repo.IsaFile("mcp_stub_instructions.json"));
        var cpu = new StubCpu(isa, bus);
        cpu.State.ProgramCounter = (ushort)origins["CODE"];
        for (int steps = 0; !cpu.State.Halted; steps++)
        {
            steps.Should().BeLessThan(100_000, "program ma się zatrzymać");
            cpu.Step();
        }

        return (cpu, bus, result);
    }

    [Fact]
    public void Memcpy_Copies_Bytes()
    {
        const string Driver = """
            .segment "CODE"
            LDI (src & 255)
            STA cc_mm_src
            LDI (src >> 8)
            STA cc_mm_src_h
            LDI (dst & 255)
            STA cc_mm_dst
            LDI (dst >> 8)
            STA cc_mm_dst_h
            LDX 4
            CALL mm_memcpy
            LDX 0
            CALL mm_memcpy
            HLT
            .segment "DATA"
            src: .byte 10, 20, 30, 40
            dst: .res 4
            .include "mem.s"
            """;
        var (_, bus, result) = RunAsm(Driver, "memtest.asm");
        int dst = result.Symbols["dst"];

        bus.Read((ushort)dst).Should().Be(10);
        bus.Read((ushort)(dst + 1)).Should().Be(20);
        bus.Read((ushort)(dst + 2)).Should().Be(30);
        bus.Read((ushort)(dst + 3)).Should().Be(40);
    }

    [Fact]
    public void Memset_Fills_Bytes()
    {
        const string Driver = """
            .segment "CODE"
            LDI (dst & 255)
            STA cc_mm_dst
            LDI (dst >> 8)
            STA cc_mm_dst_h
            LDI 90
            LDX 4
            CALL mm_memset
            HLT
            .segment "DATA"
            dst: .res 4
            .include "mem.s"
            """;
        var (_, bus, result) = RunAsm(Driver, "memtest.asm");
        int dst = result.Symbols["dst"];

        for (int i = 0; i < 4; i++)
        {
            bus.Read((ushort)(dst + i)).Should().Be(90);
        }
    }

    [Fact]
    public void Strlen_Measures_Without_Nul()
    {
        const string Driver = """
            .segment "CODE"
            LDI (text & 255)
            STA cc_mm_src
            LDI (text >> 8)
            STA cc_mm_src_h
            CALL mm_strlen
            STA got
            LDI (empty & 255)
            STA cc_mm_src
            LDI (empty >> 8)
            STA cc_mm_src_h
            CALL mm_strlen
            STA got_empty
            HLT
            .segment "DATA"
            text: .byte 72, 105, 33, 0
            empty: .byte 0
            got: .byte 0
            got_empty: .byte 99
            .include "mem.s"
            """;
        var (_, bus, result) = RunAsm(Driver, "memtest.asm");

        bus.Read((ushort)result.Symbols["got"]).Should().Be(3);
        bus.Read((ushort)result.Symbols["got_empty"]).Should().Be(0);
    }

    [Fact]
    public void Strcpy_Copies_With_Nul()
    {
        const string Driver = """
            .segment "CODE"
            LDI (text & 255)
            STA cc_mm_src
            LDI (text >> 8)
            STA cc_mm_src_h
            LDI (dst & 255)
            STA cc_mm_dst
            LDI (dst >> 8)
            STA cc_mm_dst_h
            CALL mm_strcpy
            HLT
            .segment "DATA"
            text: .byte 65, 66, 0
            dst: .res 4
            .include "mem.s"
            """;
        var (_, bus, result) = RunAsm(Driver, "memtest.asm");
        int dst = result.Symbols["dst"];

        bus.Read((ushort)dst).Should().Be(65);
        bus.Read((ushort)(dst + 1)).Should().Be(66);
        bus.Read((ushort)(dst + 2)).Should().Be(0);
    }

    [Fact]
    public void Io_Putchar_And_Puthex_Fill_Buffer()
    {
        const string Driver = """
            .segment "CODE"
            LDI 65
            CALL putchar
            LDI 66
            CALL putchar
            LDI 171
            CALL puthex
            HLT
            .include "io.s"
            """;
        var (_, bus, result) = RunAsm(Driver, "iotest.asm");
        int buf = result.Symbols["__io_buf"];

        bus.Read((ushort)buf).Should().Be(65);
        bus.Read((ushort)(buf + 1)).Should().Be(66);
        bus.Read((ushort)(buf + 2)).Should().Be(65);
        bus.Read((ushort)(buf + 3)).Should().Be(66);
        bus.Read((ushort)result.Symbols["__io_cur"]).Should().Be(4);
    }

    [Fact]
    public void C_Calls_Putchar_And_Puthex()
    {
        const string Source = """
            void putchar(uchar c);
            void puthex(uchar c);
            int main() {
                putchar(65);
                puthex(171);
                return 0;
            }
            """;
        var (_, bus, result) = RunCWithLib(Source);
        int buf = result.Symbols["__io_buf"];

        bus.Read((ushort)buf).Should().Be(65);
        bus.Read((ushort)(buf + 1)).Should().Be(65);
        bus.Read((ushort)(buf + 2)).Should().Be(66);
        bus.Read((ushort)result.Symbols["__io_cur"]).Should().Be(3);
    }

    [Fact]
    public void C_Calls_Putdec_With_Sign_And_Zero()
    {
        const string Source = """
            void putchar(uchar c);
            void putdec(int v);
            int main() {
                putdec(-1234);
                putchar(32);
                putdec(0);
                putchar(32);
                putdec(9876);
                putchar(32);
                putdec(-32768);
                return 0;
            }
            """;
        var (_, bus, result) = RunCWithLib(Source);
        int buf = result.Symbols["__io_buf"];
        string text = string.Concat(Enumerable.Range(0, 19).Select(i => (char)bus.Read((ushort)(buf + i))));

        text.Should().Be("-1234 0 9876 -32768");
    }

    [Fact]
    public void C_Calls_Puts_On_Buffer_And_Screen()
    {
        const string Source = """
            void putchar(uchar c);
            void puts(uchar *s);
            void scr_goto(uchar x, uchar y);
            void scr_putc(uchar c);
            void scr_clear();
            int main() {
                uchar msg[4];
                msg[0] = 72;
                msg[1] = 105;
                msg[2] = 33;
                msg[3] = 0;
                puts(msg);
                scr_clear();
                scr_goto(10, 5);
                scr_putc(72);
                scr_putc(105);
                scr_putc(33);
                return 0;
            }
            """;
        string puts = File.ReadAllText(Path.Combine(Repo.Path("samples", "minic", "lib"), "puts.c"));
        string screen = File.ReadAllText(Path.Combine(LibDir(), "screen.s"));
        var (_, bus, result) = RunCWithLib(Source, puts, screen);
        int buf = result.Symbols["__io_buf"];
        int scr = result.Symbols["__scr_buf"];

        bus.Read((ushort)buf).Should().Be(72);
        bus.Read((ushort)(buf + 1)).Should().Be(105);
        bus.Read((ushort)(buf + 2)).Should().Be(33);
        bus.Read((ushort)(scr + (5 * 40) + 10)).Should().Be(72);
        bus.Read((ushort)(scr + (5 * 40) + 12)).Should().Be(33);
    }
}
