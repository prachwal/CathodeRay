using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 43, poz. 5: matcher pętli licznikowej <c>uchar</c> (wykrywanie bez emisji).</summary>
public sealed class CountLoopTests
{
    private static Ir.Cell C(string sym, int w = 1) => new(sym, w);

    private static Ir.Function Func(params Ir.Ins[] body) =>
        new("f", false, [], 0, [], body.ToList());

    [Fact]
    public void While_Shape_Matches()
    {
        var body = new Ir.Ins[]
        {
            new Ir.Label("top"),
            new Ir.BrCmp(Ir.Cond.Ne, C("n"), new Ir.Imm(0, 1), "end"),
            new Ir.Bin(Ir.BinOp.Add, C("s", 2), C("s", 2), new Ir.Imm(1, 2)),
            new Ir.Bin(Ir.BinOp.Sub, C("n"), C("n"), new Ir.Imm(1, 1)),
            new Ir.Jmp("top"),
            new Ir.Label("end"),
            new Ir.Ret(new Ir.Imm(0, 2), 2),
        };

        CountLoopMatch? match = ByteSelector.MatchCountLoop(Func(body), 0);
        match.Should().NotBeNull();
        match!.Counter.Should().Be("n");
        match.Top.Should().Be("top");
        match.End.Should().Be("end");
    }

    [Fact]
    public void DoWhile_Shape_Matches()
    {
        var body = new Ir.Ins[]
        {
            new Ir.Label("top"),
            new Ir.Bin(Ir.BinOp.Add, C("s", 2), C("s", 2), new Ir.Imm(1, 2)),
            new Ir.Bin(Ir.BinOp.Sub, C("n"), C("n"), new Ir.Imm(1, 1)),
            new Ir.BrCmp(Ir.Cond.Ne, C("n"), new Ir.Imm(0, 1), "top"),
            new Ir.Label("end"),
            new Ir.Ret(new Ir.Imm(0, 2), 2),
        };

        CountLoopMatch? match = ByteSelector.MatchCountLoop(Func(body), 0);
        match.Should().NotBeNull();
        match!.Counter.Should().Be("n");
    }

    [Fact]
    public void Counter_Used_After_Loop_Does_Not_Match()
    {
        var body = new Ir.Ins[]
        {
            new Ir.Label("top"),
            new Ir.BrCmp(Ir.Cond.Ne, C("n"), new Ir.Imm(0, 1), "end"),
            new Ir.Bin(Ir.BinOp.Sub, C("n"), C("n"), new Ir.Imm(1, 1)),
            new Ir.Jmp("top"),
            new Ir.Label("end"),
            new Ir.Bin(Ir.BinOp.Add, C("r", 2), C("n"), new Ir.Imm(0, 2)),
            new Ir.Ret(C("r", 2), 2),
        };

        ByteSelector.MatchCountLoop(Func(body), 0).Should().BeNull("licznik po pętli wymaga writebacku");
    }

    [Fact]
    public void Two_Decs_Do_Not_Match()
    {
        var body = new Ir.Ins[]
        {
            new Ir.Label("top"),
            new Ir.BrCmp(Ir.Cond.Ne, C("n"), new Ir.Imm(0, 1), "end"),
            new Ir.Bin(Ir.BinOp.Sub, C("n"), C("n"), new Ir.Imm(1, 1)),
            new Ir.Bin(Ir.BinOp.Sub, C("n"), C("n"), new Ir.Imm(1, 1)),
            new Ir.Jmp("top"),
            new Ir.Label("end"),
            new Ir.Ret(new Ir.Imm(0, 2), 2),
        };

        ByteSelector.MatchCountLoop(Func(body), 0).Should().BeNull("dwa dekrementy to nie djnz");
    }

    [Fact]
    public void Int_Counter_Does_Not_Match()
    {
        var body = new Ir.Ins[]
        {
            new Ir.Label("top"),
            new Ir.BrCmp(Ir.Cond.Ne, C("n", 2), new Ir.Imm(0, 2), "end"),
            new Ir.Bin(Ir.BinOp.Sub, C("n", 2), C("n", 2), new Ir.Imm(1, 2)),
            new Ir.Jmp("top"),
            new Ir.Label("end"),
            new Ir.Ret(new Ir.Imm(0, 2), 2),
        };

        ByteSelector.MatchCountLoop(Func(body), 0).Should().BeNull("djnz liczy tylko bajty");
    }

    [Fact]
    public void Header_Testing_Other_Cell_Does_Not_Match()
    {
        var body = new Ir.Ins[]
        {
            new Ir.Label("top"),
            new Ir.BrCmp(Ir.Cond.Ne, C("m"), new Ir.Imm(0, 1), "end"),
            new Ir.Bin(Ir.BinOp.Sub, C("n"), C("n"), new Ir.Imm(1, 1)),
            new Ir.Jmp("top"),
            new Ir.Label("end"),
            new Ir.Ret(new Ir.Imm(0, 2), 2),
        };

        ByteSelector.MatchCountLoop(Func(body), 0).Should().BeNull("test nie dotyczy licznika");
    }

    [Fact]
    public void Foreign_Jump_Into_Loop_Does_Not_Match()
    {
        var body = new Ir.Ins[]
        {
            new Ir.BrCmp(Ir.Cond.Ne, C("q"), new Ir.Imm(0, 1), "mid"),
            new Ir.Label("top"),
            new Ir.BrCmp(Ir.Cond.Ne, C("n"), new Ir.Imm(0, 1), "end"),
            new Ir.Label("mid"),
            new Ir.Bin(Ir.BinOp.Sub, C("n"), C("n"), new Ir.Imm(1, 1)),
            new Ir.Jmp("top"),
            new Ir.Label("end"),
            new Ir.Ret(new Ir.Imm(0, 2), 2),
        };

        ByteSelector.MatchCountLoop(Func(body), 1).Should().BeNull("obcy skok do środka pętli");
    }

    [Fact]
    public void Only_Z80_Implements_CountLoop()
    {
        static bool Implements<T>(object isa) => isa is T;

        Implements<ICountLoop>(new Z80Isa()).Should().BeTrue();
        Implements<ICountLoop>(new Intel8080Isa()).Should().BeFalse("8080 nie ma djnz");
    }
}
