using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 31, krok 8: ręcznie pisane mnożenie i dzielenie 16-bitowe (6502, Z80) kontra wartości wzorcowe.</summary>
public sealed class RuntimeRoutinesTests
{
    private static readonly int[] Values = [0, 1, 2, 3, 7, 100, 255, 256, 1000, 0x7FFF, 0x8000, 0xFFFF];

    public static TheoryData<string, int> Cases()
    {
        var data = new TheoryData<string, int>();
        foreach (string name in TargetHarness.Targets.Select(static t => t.Name))
        {
            foreach (int a in Values)
            {
                data.Add(name, a);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Multiplication_And_Division_Match_The_Reference(string cpu, int a)
    {
        var source = new System.Text.StringBuilder();

        // operandy w pamięci, żeby kompilator nie policzył wyniku sam
        source.AppendLine("uint in[2];\nuint mul(uint *p) { return p[0] * p[1]; }\nuint div(uint *p) { return p[0] / p[1]; }\nuint mod(uint *p) { return p[0] % p[1]; }");
        source.AppendLine("int main() {\n  uint s = 0;");
        uint sum = 0;
        foreach (int b in Values)
        {
            source.AppendLine($"  in[0] = {a}u; in[1] = {b}u; s = s + mul(in) + div(in) * 3 + mod(in) * 5;");
            sum = (ushort)(sum + (ushort)(a * b) + ((b == 0 ? 0 : (ushort)(a / b)) * 3) + ((b == 0 ? 0 : (ushort)(a % b)) * 5));
        }

        source.AppendLine("  return s;\n}");

        CcRun.RunOn(source.ToString(), cpu).Value.Should().Be((int)(sum & 0xFFFF));
    }

    /// <summary>Plan 35, krok 2: <c>__cc_mul</c>, <c>__cc_divu</c> i <c>__cc_modu</c> z <c>rt_mul.s</c>/<c>rt_div.s</c> zwracają wynik w HL,
    /// wołający czyta go z HL, nie z <c>cc_ret</c>. 1234 * 56 = 69104 = 3568 (mod 65536), 1234 / 56 = 22, 1234 % 56 = 2:
    /// 3568 + 22 * 100 + 2 = 5770.</summary>
    [Fact]
    public void Z80_Assembly_Routines_Return_In_Hl()
    {
        const string Source = "uint a = 1234; uint b = 56;\nint main() { return a * b + (a / b) * 100 + a % b; }\n";
        string code = CathodeRay.C.CTargets.Find("z80")!.Emit(
            CathodeRay.C.Codegen.Lower(CathodeRay.C.TypeChecker.Check(CathodeRay.C.Parser.Parse(Source)), "t.c", objectMode: true), optimize: true);

        code.Should().Contain("call __cc_mul").And.Contain("call __cc_divu").And.Contain("call __cc_modu").And.NotContain("(cc_ret)");
        CcRun.RunOn(Source, "z80").Value.Should().Be(5770);
    }

    /// <summary>Plan 35, krok 2: funkcja z własnego modułu <c>.s</c> zwraca <c>int</c> w HL (ABI Z80/8080); 21 * 2 + 1 = 43.</summary>
    /// <param name="cpu">Cel.</param>
    /// <param name="body">Ciało funkcji <c>twice</c> w asemblerze celu.</param>
    [Theory]
    [InlineData("z80", "ld hl,(cc_arg1)\nadd hl,hl\nret")]
    [InlineData("8080", "lhld cc_arg1\ndad h\nret")]
    public void Assembly_Module_Returns_Int_In_Hl(string cpu, string body)
    {
        string dir = Directory.CreateTempSubdirectory("cathode-asm-ret-").FullName;
        try
        {
            string module = Path.Combine(dir, "twice.s");
            File.WriteAllText(module, $"GLOBAL twice\nEXTERN cc_arg1\nSEGMENT \"CODE\"\ntwice:\n{body}\n");
            CcRun.RunOn("int twice(int x);\nint main() { return twice(21) + 1; }\n", cpu, module).Value.Should().Be(43);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
