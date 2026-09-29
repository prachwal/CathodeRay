using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 31, krok 9: adresowanie indeksowane tablic o znanym adresie (6502: <c>LDA tab,X</c>).</summary>
public sealed class CIndexedTests
{
    public static TheoryData<string> Targets()
    {
        var data = new TheoryData<string>();
        foreach (string name in TargetHarness.Targets.Select(static t => t.Name))
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void Loops_Over_Arrays_Of_Every_Element_Size(string cpu)
    {
        const string Source = """
            uchar bytes[10];
            int words[10];
            long longs[6];
            struct Rec { uchar tag; int vals[4]; };
            struct Rec rec;
            int main() {
                int i;
                uchar local[8];
                for (i = 0; i < 10; i++) { bytes[i] = (uchar)(i * 3); words[i] = i * 300; }
                for (i = 0; i < 6; i++) longs[i] = i * 100000L;
                for (i = 0; i < 4; i++) rec.vals[i] = 1000 + i;
                for (i = 0; i < 8; i++) local[i] = (uchar)(200 + i);
                int sum = 0;
                for (i = 0; i < 10; i++) sum = sum + bytes[i] + words[i];
                for (i = 0; i < 4; i++) sum = sum + rec.vals[i] + local[i];
                return sum + (int)(longs[5] >> 8);
            }
            """;

        int sum = 0;
        for (int i = 0; i < 10; i++)
        {
            sum += (i * 3) + (i * 300);
        }

        for (int i = 0; i < 4; i++)
        {
            sum += 1000 + i + 200 + i;
        }

        CcRun.RunOn(Source, cpu, "-Werror").Value.Should().Be((sum + (500000 >> 8)) & 0xFFFF);
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void Index_Cell_Can_Be_The_Destination(string cpu)
    {
        const string Source = """
            uchar chain[6] = {3, 5, 1, 4, 2, 0};
            int wide[4] = {2, 3, 1, 0};
            int main() {
                uchar b = 0;
                int w = 0;
                b = chain[b];
                b = chain[b];
                w = wide[w];
                w = wide[w];
                w = wide[w];
                return b * 100 + w;
            }
            """;

        CcRun.RunOn(Source, cpu).Value.Should().Be(((4 * 100) & 255) + 3);
    }

    [Fact]
    public void Six_Five_O_Two_Uses_Indexed_Addressing_For_Known_Arrays()
    {
        const string Source = "uchar tab[16];\nint main() { int i; int s = 0; for (i = 0; i < 16; i++) { tab[i] = i; s = s + tab[i]; } return s; }";
        string dir = Directory.CreateTempSubdirectory("cathode-idx-").FullName;
        try
        {
            string src = Path.Combine(dir, "a.c");
            File.WriteAllText(src, Source);
            string lst = Path.Combine(dir, "a.lst");
            var error = new StringWriter();
            int exit = CathodeRay.Cli.CliApp.CreateRoot().Parse(["cc", src, "-o", Path.Combine(dir, "a.bin"), "-l", lst, "--cpu", "6502"]).Invoke(new System.CommandLine.InvocationConfiguration { Output = new StringWriter(), Error = error });
            exit.Should().Be(0, error.ToString());

            File.ReadAllText(lst).Should().Contain("cc_g_tab,x");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
