using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 33, krok 3: żywość komórek na grafie przepływu. Oczekiwane zbiory policzone ręcznie z równań
/// <c>live_in = use ∪ (live_out − kill)</c>, <c>live_out = ∪ live_in(następnik)</c>.</summary>
public sealed class IrLivenessTests
{
    private static readonly Ir.Cell X = new("x", 2);
    private static readonly Ir.Cell Y = new("y", 2);
    private static readonly Ir.Cell N = new("n", 2);
    private static readonly Ir.Cell C = new("c", 2);

    [Fact]
    public void Loop_Keeps_Counter_Live_Across_Call_And_Back_Edge()
    {
        Ir.Ins[] body =
        [
            new Ir.Mov(X, Imm(0)),                              // 0: x = 0
            new Ir.Label("top"),                                // 1
            Call(),                                             // 2: f()
            new Ir.Bin(Ir.BinOp.Add, Y, X, Imm(1)),             // 3: y = x + 1
            new Ir.Mov(X, Y),                                   // 4: x = y
            new Ir.BrCmp(Ir.Cond.Lt, X, Imm(10), "top"),        // 5: if x < 10 goto top
            new Ir.Ret(X, 2),                                   // 6: return x
        ];

        IrLiveness live = Analyze(body);

        live.LiveOut(2).Should().BeEquivalentTo(["x"]);         // y zapisane w 3 przed odczytem
        live.LiveOut(3).Should().BeEquivalentTo(["y"]);
        live.LiveOut(5).Should().BeEquivalentTo(["x"]);         // obie gałęzie czytają x
        live.LiveIn(1).Should().BeEquivalentTo(["x"]);
        live.LiveIn(0).Should().BeEmpty();                      // x zabite w 0
    }

    [Fact]
    public void Forward_Goto_Keeps_Value_Written_Before_Call_Live()
    {
        Ir.Ins[] body =
        [
            new Ir.Mov(X, N),                                   // 0: x = n
            Call(),                                             // 1: g()
            new Ir.BrCmp(Ir.Cond.Ne, N, Imm(0), "skip"),        // 2: if n != 0 goto skip
            new Ir.Mov(X, Imm(100)),                            // 3: x = 100
            new Ir.Label("skip"),                               // 4
            new Ir.Ret(X, 2),                                   // 5: return x
        ];

        IrLiveness live = Analyze(body);

        live.LiveOut(1).Should().BeEquivalentTo(["n", "x"]);    // skok z 2 omija zapis w 3
        live.LiveIn(3).Should().BeEmpty();
        live.LiveOut(3).Should().BeEquivalentTo(["x"]);
        live.LiveIn(0).Should().BeEquivalentTo(["n"]);
    }

    [Fact]
    public void Backward_Goto_Makes_Value_Live_At_Loop_Head_But_Not_Overwritten_One()
    {
        Ir.Ins[] body =
        [
            new Ir.Label("top"),                                // 0
            new Ir.Mov(Y, X),                                   // 1: y = x
            Call(),                                             // 2: f()
            new Ir.Mov(X, Y),                                   // 3: x = y
            new Ir.BrCmp(Ir.Cond.Ne, X, Imm(0), "top"),         // 4: if x != 0 goto top
            new Ir.Ret(Y, 2),                                   // 5: return y
        ];

        IrLiveness live = Analyze(body);

        live.LiveOut(2).Should().BeEquivalentTo(["y"]);         // x nadpisane w 3 przed odczytem
        live.LiveOut(4).Should().BeEquivalentTo(["x", "y"]);    // x przez krawędź wsteczną, y przez powrót
        live.LiveIn(0).Should().BeEquivalentTo(["x"]);
        live.LiveOut(1).Should().BeEquivalentTo(["y"]);
    }

    [Fact]
    public void Partial_Write_Does_Not_Kill_The_Cell()
    {
        Ir.Ins[] body =
        [
            new Ir.Mov(X, Imm(5)),                              // 0: x = 5 (cały obiekt)
            Call(),                                             // 1: f()
            new Ir.Mov(new Ir.Cell("x", 1), Imm(7, 1)),         // 2: (uchar)x = 7
            new Ir.Mov(new Ir.Cell("w+2", 2), Imm(1)),          // 3: górna połowa 4-bajtowego w
            new Ir.Bin(Ir.BinOp.Add, X, X, new Ir.Cell("w", 2)), // 4: x = x + w (dolna połowa)
            new Ir.Ret(X, 2),                                   // 5
        ];

        IrLiveness live = Analyze(body);

        live.LiveOut(1).Should().BeEquivalentTo(["x", "w"]);    // zapisy w 2 i 3 są częściowe
        live.LiveIn(3).Should().BeEquivalentTo(["x", "w"]);
        live.LiveIn(0).Should().BeEquivalentTo(["w"]);          // x zabite w 0, w nigdy w całości
        IrLiveness.Killed(body[0], Size).Should().Be("x");
        IrLiveness.Killed(body[2], Size).Should().BeNull();
        IrLiveness.Killed(body[3], Size).Should().BeNull();
    }

    [Fact]
    public void Irreducible_Loop_With_Two_Entries_Converges()
    {
        Ir.Ins[] body =
        [
            new Ir.BrCmp(Ir.Cond.Eq, C, Imm(0), "b"),           // 0: wejście do pętli przez b albo a
            new Ir.Label("a"),                                  // 1
            Call(),                                             // 2
            new Ir.BrCmp(Ir.Cond.Eq, X, Imm(0), "end"),         // 3: czyta x
            new Ir.Label("b"),                                  // 4
            Call(),                                             // 5
            new Ir.Jmp("a"),                                    // 6
            new Ir.Label("end"),                                // 7
            new Ir.Ret(null, 0),                                // 8
        ];

        IrLiveness live = Analyze(body);

        live.LiveOut(2).Should().BeEquivalentTo(["x"]);
        live.LiveOut(5).Should().BeEquivalentTo(["x"]);         // b -> a -> odczyt x w 3
        live.LiveIn(4).Should().BeEquivalentTo(["x"]);
        live.LiveIn(0).Should().BeEquivalentTo(["c", "x"]);
        live.LiveOut(0).Should().BeEquivalentTo(["x"]);
        live.LiveIn(8).Should().BeEmpty();
    }

    [Fact]
    public void Call_Result_Is_Killed_And_Address_Taken_Is_A_Use()
    {
        Ir.Ins[] body =
        [
            new Ir.Mov(Y, new Ir.AddrOf("x", 0)),               // 0: y = &x
            new Ir.Call("f", null, [N], [2], X),                // 1: x = f(n)
            new Ir.Ret(X, 2),                                   // 2
        ];

        IrLiveness live = Analyze(body);

        IrLiveness.Killed(body[1], Size).Should().Be("x");
        live.LiveIn(1).Should().BeEquivalentTo(["n"]);
        live.LiveIn(0).Should().BeEquivalentTo(["n", "x"]);     // &x czyta obiekt x
    }

    [Fact]
    public void Unknown_Label_Throws()
    {
        Action act = () => IrLiveness.Of([new Ir.Jmp("nowhere")], Size);

        act.Should().Throw<InvalidOperationException>();
    }

    private static int Size(string symbol) => (symbol == "w") ? 4 : 2;

    private static IrLiveness Analyze(Ir.Ins[] body) => IrLiveness.Of(body, Size);

    private static Ir.Imm Imm(int value, int width = 2) => new(value, width);

    private static Ir.Call Call() => new("f", null, [], [], null);
}
