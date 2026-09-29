using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 30, krok 17: tablice wielowymiarowe <c>int m[3][4]</c>, wskaźnik do wiersza, parametr <c>m[][4]</c>.</summary>
public sealed class CMatrixTests
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
    public void Sample_Runs_On_Every_Target(string cpu)
    {
        string source = File.ReadAllText(Repo.Path("samples", "minic", "20_matrix.c"));

        CcRun.RunOn(source, cpu, "-Werror").Value.Should().Be(591);
    }

    [Fact]
    public void Three_Dimensions_And_Global_Zero_Init()
    {
        const string Source = """
            uchar cube[2][3][4];
            int main() {
                int i; int j; int k;
                for (i = 0; i < 2; i++)
                    for (j = 0; j < 3; j++)
                        for (k = 0; k < 4; k++)
                            cube[i][j][k] = (uchar)(i * 100 + j * 10 + k);
                return cube[1][2][3] + cube[0][1][2] + sizeof(cube) + sizeof(cube[0]) + sizeof(cube[0][0]);
            }
            """;

        CcRun.Run(Source, "-Werror").Value.Should().Be(123 + 12 + 24 + 12 + 4);
    }

    [Fact]
    public void Struct_Field_And_Unsized_First_Dimension()
    {
        const string Source = """
            struct Board { uchar cells[3][3]; uchar turn; };
            int main() {
                int m[][2] = {{1, 2}, {3, 4}, {5, 6}};
                struct Board b;
                b.cells[1][2] = 7;
                b.cells[2][0] = 9;
                b.turn = 2;
                return sizeof(m) * 100 + m[2][1] * 10 + b.cells[1][2] + b.cells[2][0] + sizeof(struct Board);
            }
            """;

        CcRun.Run(Source).Value.Should().Be((12 * 100) + 60 + 7 + 9 + 10);
    }

    [Fact]
    public void Row_Pointer_Arithmetic_Scales_By_Row_Size()
    {
        const string Source = """
            int a[3][2] = {{1, 2}, {3, 4}, {5, 6}};
            int main() {
                int (*p)[2] = a;
                int (*q)[2] = a + 2;
                return (q - p) * 100 + (*(p + 1))[1] * 10 + p[2][0];
            }
            """;

        CcRun.Run(Source).Value.Should().Be(200 + 40 + 5);
    }

    [Theory]
    [InlineData("int main() { int m[2][3]; m[1] = 0; return 0; }", "not assignable")]
    [InlineData("int main() { int m[2][n]; return 0; }", "literal")]
    [InlineData("int f(int (*p)[3]) { return p[0][0]; }\nint main() { int m[2][4]; return f(m); }", "cannot convert")]
    public void Misuse_Is_Rejected(string source, string message)
    {
        (int exit, string error) = CcRun.Compile(source);

        exit.Should().NotBe(0);
        error.Should().Contain(message);
    }
}
