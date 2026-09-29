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
}
