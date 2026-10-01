using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 33, kroki 13–14: mapa rejestrów w <see cref="Z80Isa"/> (ręczna, bez alokatora). Ręcznie zbudowane IR, listing
/// z operandami-rejestrami, wynik z emulatora Z80 kontra <see cref="IrInterpreter"/> i wartość liczona ręcznie.</summary>
public sealed class Z80RegisterIsaTests
{
    [Fact]
    public void Byte_Cell_In_C_Uses_Register_Operands()
    {
        var x = new Ir.Cell("main__x", 1);
        var r = new Ir.Cell("main__r", 1);

        // r = 3 + 4 + 5 + 6 + 7 + 8 + 9 = 42
        List<Ir.Ins> body =
        [
            new Ir.Mov(r, new Ir.Imm(0, 1)),
            new Ir.Mov(x, new Ir.Imm(3, 1)),
            new Ir.Label("loop"),
            new Ir.Bin(Ir.BinOp.Add, r, r, x),
            new Ir.Bin(Ir.BinOp.Add, x, x, new Ir.Imm(1, 1)),
            new Ir.BrCmp(Ir.Cond.Ltu, x, new Ir.Imm(10, 1), "loop"),
            new Ir.Ret(r, 1),
        ];
        Ir.Module module = Module(body, Bss("main__x", 1), Bss("main__r", 1));

        (string code, int value) = Run(module, new Dictionary<string, string> { ["main__x"] = "c" });

        code.Should().Contain("ld a,c").And.Contain("add a,c").And.Contain("inc c").And.Contain("ld c,a");
        code.Should().NotContain("main__x", "komórka w rejestrze nie ma adresu ani miejsca w BSS");
        value.Should().Be(42).And.Be(Interpret(module));
    }

    [Fact]
    public void Word_Cell_In_Bc_Uses_Pair_Add_And_Signed_Compare()
    {
        var y = new Ir.Cell("main__y", 2);
        var s = new Ir.Cell("main__s", 2);

        // s = 1000 + 1001 + 1002 = 3003, y = 1003; y = -5 (< 2 ze znakiem, skok omija s = 1); s = 3003 - (-5) = 3008;
        // y = 3008 + 7 = 3015; s < y ze znakiem (skok omija y = 0); wynik 3015
        List<Ir.Ins> body =
        [
            new Ir.Mov(y, new Ir.Imm(1000, 2)),
            new Ir.Mov(s, new Ir.Imm(0, 2)),
            new Ir.Label("loop"),
            new Ir.Bin(Ir.BinOp.Add, s, s, y),
            new Ir.Bin(Ir.BinOp.Add, y, y, new Ir.Imm(1, 2)),
            new Ir.BrCmp(Ir.Cond.Lt, y, new Ir.Imm(1003, 2), "loop"),
            new Ir.Mov(y, new Ir.Imm(0xFFFB, 2)),
            new Ir.BrCmp(Ir.Cond.Lt, y, new Ir.Imm(2, 2), "neg"),
            new Ir.Mov(s, new Ir.Imm(1, 2)),
            new Ir.Label("neg"),
            new Ir.Bin(Ir.BinOp.Sub, s, s, y),
            new Ir.Bin(Ir.BinOp.Add, y, s, new Ir.Imm(7, 2)),
            new Ir.BrCmp(Ir.Cond.Lt, s, y, "done"),
            new Ir.Mov(y, new Ir.Imm(0, 2)),
            new Ir.Label("done"),
            new Ir.Ret(y, 2),
        ];
        Ir.Module module = Module(body, Bss("main__y", 2), Bss("main__s", 2));

        (string code, int value) = Run(module, new Dictionary<string, string> { ["main__y"] = "bc" });

        code.Should().Contain("ld bc,1000").And.Contain("add hl,bc").And.Contain("inc bc").And.Contain("sbc hl,bc")
            .And.Contain("ld de,7").And.Contain("ld c,l").And.Contain("ld b,h").And.Contain(string.Join(Environment.NewLine, "ld l,c", "ld h,b", "main__ret:"))
            .And.Contain("ld a,c").And.Contain("ld a,b").And.Contain("sub c").And.Contain("sbc a,b").And.Contain("jp po,");
        code.Should().NotContain("main__y");
        value.Should().Be(3015).And.Be(Interpret(module));
    }

    [Fact]
    public void Pointer_In_De_Is_Copied_To_Hl()
    {
        var p = new Ir.Cell("main__p", 2);
        var v = new Ir.Cell("main__v", 1);

        // p = &arr; v = arr[1] = 20; arr[2] = v; p = *(p + 2) jako słowo = 20 | (40 << 8) = 10260 (wynik w tej samej komórce co wskaźnik)
        List<Ir.Ins> body =
        [
            new Ir.Mov(p, new Ir.AddrOf("arr", 0)),
            new Ir.Load(v, p, 1, 1),
            new Ir.Store(p, 2, v, 1),
            new Ir.Load(p, p, 2, 2),
            new Ir.Ret(p, 2),
        ];
        Ir.Module module = Module(body, Bss("main__p", 2), Bss("main__v", 1), new Ir.Data("arr", "DATA", 4, [new Ir.Bytes([10, 20, 30, 40])], false));

        (string code, int value) = Run(module, new Dictionary<string, string> { ["main__p"] = "de" });

        code.Should().Contain("ld l,e").And.Contain("ld h,d").And.Contain("ld e,a").And.Contain("ld d,a");
        value.Should().Be(20 | (40 << 8)).And.Be(Interpret(module));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Value_In_E_Survives_Pointer_Load_With_Large_Offset(bool bcTaken)
    {
        var x = new Ir.Cell("main__x", 1);
        var p = new Ir.Cell("main__p", 2);
        var v = new Ir.Cell("main__v", 1);

        // x = 5; v = *(p + 6) = arr[6] = 70; wynik x + v = 75 (zniszczone E dałoby 6 + 70 = 76)
        List<Ir.Ins> body =
        [
            new Ir.Mov(x, new Ir.Imm(5, 1)),
            new Ir.Mov(p, new Ir.AddrOf("arr", 0)),
            new Ir.Load(v, p, 6, 1),
            new Ir.Bin(Ir.BinOp.Add, v, x, v),
            new Ir.Ret(v, 1),
        ];
        var arr = new Ir.Data("arr", "DATA", 8, [new Ir.Bytes([10, 20, 30, 40, 50, 60, 70, 80])], false);
        var registers = new Dictionary<string, string> { ["main__x"] = "e" };
        if (bcTaken)
        {
            // para zajęta w tej funkcji (wolne pary liczy się per funkcja, BeginFunction)
            body.Insert(0, new Ir.Mov(new Ir.Cell("main__other", 2), new Ir.Imm(0, 2)));
            registers["main__other"] = "bc";
        }

        Ir.Module module = Module(body, Bss("main__x", 1), Bss("main__p", 2), Bss("main__v", 1), Bss("main__other", 2), arr);

        (string code, int value) = Run(module, registers);

        CpuModels.For("z80").Scratch.Should().NotIntersectWith(["b", "c", "d", "e"], "prymitywy zachowują rejestry przypisane komórkom");
        if (bcTaken)
        {
            code.Should().Contain(string.Join(Environment.NewLine, "push de", "ld de,6", "add hl,de", "pop de"));
        }
        else
        {
            code.Should().Contain(string.Join(Environment.NewLine, "ld bc,6", "add hl,bc")).And.NotContain("ld de,6");
        }

        value.Should().Be(75).And.Be(Interpret(module));
    }

    private static Ir.Data Bss(string sym, int size) => new(sym, "BSS", size, null, false);

    private static Ir.Module Module(List<Ir.Ins> body, params Ir.Data[] data) =>
        new([new Ir.Function("main", false, [], body.OfType<Ir.Ret>().Single().W, [], body)], data, [], [], ObjectMode: false);

    private static int Interpret(Ir.Module module) => IrInterpreter.Load([module]).RunMain().Value;

    /// <summary>Selektor z ręczną mapą rejestrów, złożenie z crt0 i wykonanie na emulatorze Z80; wynik z <c>cc_ret</c>.</summary>
    private static (string Code, int Value) Run(Ir.Module module, Dictionary<string, string> registers)
    {
        var isa = new Z80Isa();
        isa.AssignRegisters(registers);
        string code = new ByteSelector(Legalizer.Run(module), isa).Emit();
        ICTarget target = CTargets.All.Single(static t => t.Name == "z80");
        TargetHarness.Result result = TargetHarness.RunAssembly(target, target.Crt0 + code);
        return (code, module.Functions[0].RetW == 2 ? result.Return : result.Read("cc_ret", 1)[0]);
    }
}
