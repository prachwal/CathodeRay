using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 43, poz. 6: emisja <c>djnz</c> na Z80 (licznik lokalny z pre-koloru w B).</summary>
public sealed class DjnzTests
{
    private static string EmitZ80(string source)
    {
        Ir.Module module = Codegen.Lower(TypeChecker.Check(Parser.Parse(source)), "t.c", objectMode: true);
        return CTargets.Find("z80")!.Emit(module, optimize: true);
    }

    private const string Down = """
        int down_from(uchar start) {
            uchar n = start;
            int s = 0;
            while (n > 0) { s = s + n; n = n - 1; }
            return s;
        }
        int main() { return down_from(10); }
        """;

    [Fact]
    public void Down_Loop_Uses_Djnz()
    {
        EmitZ80(Down).Should().Contain("djnz", "licznik w B sklada sie w djnz");
        CcRun.RunOn(Down, "z80").Value.Should().Be(55);
    }

    [Fact]
    public void Down_Loop_Boundaries()
    {
        const string Source = """
            int down_from(uchar start) {
                uchar n = start;
                int s = 0;
                while (n > 0) { s = s + n; n = n - 1; }
                return s;
            }
            int main() { return down_from(0) + down_from(1) * 100 + down_from(255) % 1000; }
            """;

        // down(0) = 0, down(1) = 1, down(255) = 32640 (mod 1000 = 640)
        CcRun.RunOn(Source, "z80").Value.Should().Be(0 + 100 + 640);
    }

    [Fact]
    public void Call_In_Body_Preserves_Counter()
    {
        const string Source = """
            int twice(int x) { return x + x; }
            int down_from(uchar start) {
                uchar n = start;
                int s = 0;
                while (n > 0) { s = s + twice(n); n = n - 1; }
                return s;
            }
            int main() { return down_from(10); }
            """;

        CcRun.RunOn(Source, "z80").Value.Should().Be(110);
    }

    [Fact]
    public void Long_Body_Falls_Back_To_Dec_Jp()
    {
        var body = new System.Text.StringBuilder();
        body.Append("int big_from(uchar start) { uchar n = start; int s = 0; while (n > 0) { s = s + n;");
        for (int i = 0; i < 60; i++)
        {
            body.Append($" s = s + {i};");
        }

        body.Append(" n = n - 1; } return s; } int main() { return big_from(3); }");

        string z80 = EmitZ80(body.ToString());
        z80.Should().Contain("dec b", "dlugie cialo: zachowawcza forma");
        CcRun.RunOn(body.ToString(), "z80").Value.Should().Be(5316);
    }

    [Fact]
    public void Up_Loop_Still_Works()
    {
        const string Source = """
            int up(uchar n) {
                int s = 0;
                for (uchar i = 0; i < n; i++) { s = s + i; }
                return s;
            }
            int main() { return up(10); }
            """;

        CcRun.RunOn(Source, "z80").Value.Should().Be(45);
    }
}
