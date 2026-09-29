using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class PeepholeTests
{
    [Fact]
    public void Store_Then_Reload_Of_The_Same_Cell_Keeps_Only_The_Store()
    {
        Peephole.Optimize("STA x\nLDA x\nRET").Should().Be("STA x\nRET");
        Peephole.Optimize("STA x,X\nLDA x,X\nRET").Should().Be("STA x,X\nRET");
        Peephole.Optimize("STA x\n;c:3\nLDA x\nRET").Should().Be("STA x\n;c:3\nRET");
    }

    [Fact]
    public void Reload_Behind_A_Label_Or_Of_Another_Cell_Stays()
    {
        Peephole.Optimize("STA x\nL: LDA x\nRET").Should().Be("STA x\nL: LDA x\nRET");
        Peephole.Optimize("STA x\nL:\nLDA x\nRET").Should().Be("STA x\nL:\nLDA x\nRET");
        Peephole.Optimize("STA x\nLDA y\nRET").Should().Be("STA x\nLDA y\nRET");
    }

    [Fact]
    public void Dead_Load_Before_Another_Load_Is_Dropped_Unless_Labeled()
    {
        Peephole.Optimize("LDA a\nLDA b\nTAX").Should().Be("LDA b\nTAX");
        Peephole.Optimize("LDI 3\nTXA\nSTA c").Should().Be("TXA\nSTA c");
        Peephole.Optimize("LDA a\nLDA b\nLDA c").Should().Be("LDA c");
        Peephole.Optimize("L: LDA a\nLDA b").Should().Be("L: LDA a\nLDA b");
        Peephole.Optimize("LDA a\nADD 1\nLDA b").Should().Be("LDA a\nADD 1\nLDA b");
    }

    [Fact]
    public void Jump_To_The_Next_Label_Is_Removed()
    {
        Peephole.Optimize("JMP done\ndone:\nRET").Should().Be("done:\nRET");
        Peephole.Optimize("JMP done\n;c:1\ndone: RET").Should().Be(";c:1\ndone: RET");
        Peephole.Optimize("JMP other\ndone:\nRET").Should().Be("JMP other\ndone:\nRET");
        Peephole.Optimize("start: JMP done\ndone:\nRET").Should().Be("start:\ndone:\nRET");
    }

    [Fact]
    public void Optimized_Code_Is_Smaller_And_Behaves_Identically()
    {
        const string Source = """
            int fib(int n) { if (n < 2) return n; return fib(n - 1) + fib(n - 2); }
            int main() {
                int s = 0;
                for (int i = 0; i < 6; i++) s += fib(i);
                return s;
            }
            """;
        CheckedProgram program = TypeChecker.Check(Parser.Parse(Source));
        string plain = Codegen.Emit(program, optimize: false);
        string tuned = Codegen.Emit(program, optimize: true);

        tuned.Length.Should().BeLessThan(plain.Length);
        tuned.Split('\n').Length.Should().BeLessThan(plain.Split('\n').Length);
        CCodegenTests.RunC(Source).Cpu.State.A.Should().Be(0 + 1 + 1 + 2 + 3 + 5);
    }

    [Fact]
    public void Cc_No_Opt_Produces_A_Larger_But_Equivalent_Program()
    {
        const string Source = "int main() { int s = 0; for (int i = 1; i < 10; i++) s += i * 2; return s; }";
        CcRun.Result optimized = CcRun.Run(Source);
        CcRun.Result plain = CcRun.Run(Source, "--no-opt");

        optimized.Value.Should().Be(90);
        plain.Value.Should().Be(90);
        optimized.Steps.Should().BeLessThan(plain.Steps);
    }

    [Fact]
    public void Redundant_Ldy_Same_Index_Is_Removed()
    {
        Peephole.Optimize("LDY #3\nLDA (ZP),Y\nSTA X\nLDY #3\nLDA (ZP),Y\nRET").Should()
            .Be("LDY #3\nLDA (ZP),Y\nSTA X\nLDA (ZP),Y\nRET");
        Peephole.Optimize("LDY #5\nSTX (ZP),Y\nLDY #5\nLDA (ZP),Y\nRET").Should()
            .Be("LDY #5\nSTX (ZP),Y\nLDA (ZP),Y\nRET");
    }

    [Fact]
    public void Redundant_Ldy_Stops_At_Block_Boundary()
    {
        Peephole.Optimize("LDY #3\nL: LDY #3\nRET").Should().Be("LDY #3\nL: LDY #3\nRET");
        Peephole.Optimize("LDY #3\nJMP L\nLDY #3\nL: RET").Should().Be("LDY #3\nL: RET");
    }

    [Fact]
    public void Redundant_Ldy_Stops_When_Y_Modified()
    {
        Peephole.Optimize("LDY #3\nINY\nLDY #3\nRET").Should().Be("LDY #3\nINY\nLDY #3\nRET");
        Peephole.Optimize("LDY #3\nDEY\nLDY #3\nRET").Should().Be("LDY #3\nDEY\nLDY #3\nRET");
        Peephole.Optimize("LDY #3\nTAY\nLDY #3\nRET").Should().Be("LDY #3\nTAY\nLDY #3\nRET");
        Peephole.Optimize("LDY #3\nLDY #4\nLDY #3\nRET").Should().Be("LDY #3\nLDY #4\nLDY #3\nRET");
        Peephole.Optimize("LDY #3\nJSR F\nLDY #3\nRET").Should().Be("LDY #3\nJSR F\nLDY #3\nRET");
    }

    [Fact]
    public void Dead_Code_After_Jmp_Is_Removed()
    {
        Peephole.Optimize("JMP a\nLDA #1\na: RET").Should().Be("a: RET");
        Peephole.Optimize("JMP a\nLDA #1\nSTA x\na: RET").Should().Be("a: RET");
    }

    [Fact]
    public void Dead_Code_After_Rts_Is_Removed()
    {
        Peephole.Optimize("RTS\nLDA #1\nL: RET").Should().Be("RTS\nL: RET");
    }

    [Fact]
    public void Redundant_Lda_Zero_After_Store_Sequence_Is_Removed()
    {
        Peephole.Optimize("LDA #0\nSTA M\nLDA #0\nSTA N\nRET").Should()
            .Be("LDA #0\nSTA M\nSTA N\nRET");
        Peephole.Optimize("LDA #0\nSTA M\nSTX X\nLDA #0\nSTA N\nRET").Should()
            .Be("LDA #0\nSTA M\nSTX X\nSTA N\nRET");
        Peephole.Optimize("LDA #0\nSTA M\nSTY Y\nLDA #0\nSTA N\nRET").Should()
            .Be("LDA #0\nSTA M\nSTY Y\nSTA N\nRET");
    }

    [Fact]
    public void Redundant_Lda_Zero_Stops_At_Non_Store_Instruction()
    {
        Peephole.Optimize("LDA #0\nSTA M\nLDA #1\nSTA N\nRET").Should()
            .Be("LDA #0\nSTA M\nLDA #1\nSTA N\nRET");
        Peephole.Optimize("LDA #0\nSTA M\nADD 1\nLDA #0\nSTA N\nRET").Should()
            .Be("LDA #0\nSTA M\nADD 1\nLDA #0\nSTA N\nRET");
    }

    [Fact]
    public void Redundant_Lda_Zero_Stops_At_Label()
    {
        Peephole.Optimize("LDA #0\nSTA M\nL: LDA #0\nSTA N\nRET").Should()
            .Be("LDA #0\nSTA M\nL: LDA #0\nSTA N\nRET");
        Peephole.Optimize("LDA #0\nL:\nLDA #0\nSTA N\nRET").Should()
            .Be("LDA #0\nL:\nLDA #0\nSTA N\nRET");
    }
}
