using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 30, krok 19: <c>union</c>, operator przecinka, sklejanie napisów, <c>\xHH</c>, <c>#</c> i <c>##</c> w makrach,
/// <c>enum</c> z <c>sizeof(struct)</c>.</summary>
public sealed class CMiscFeaturesTests
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
    public void Union_Overlaps_Its_Members(string cpu)
    {
        const string Source = """
            union Word { int whole; uchar bytes[2]; };
            struct Packet { uchar kind; union Word w; };
            union Word global = {0x0102};
            int main() {
                union Word u;
                struct Packet p;
                u.whole = 0x1234;
                p.kind = 3;
                p.w.bytes[0] = 7;
                p.w.bytes[1] = 9;
                return (u.bytes[0] == 0x34 || u.bytes[0] == 0x12) * 1000 + sizeof(union Word) * 100 + sizeof(struct Packet) * 10 + (global.whole == 0x0102) + p.w.bytes[0] * 0;
            }
            """;

        CcRun.RunOn(Source, cpu).Value.Should().Be(1000 + 200 + 30 + 1);
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void Comma_Operator_Evaluates_Left_To_Right(string cpu)
    {
        const string Source = """
            int main() {
                int i;
                int j;
                int total = 0;
                for (i = 0, j = 10; i < j; i++, j--) {
                    total = total + 1;
                }
                int k = (total = total * 2, total + 1);
                return total * 100 + k;
            }
            """;

        CcRun.RunOn(Source, cpu, "-Werror").Value.Should().Be((10 * 100) + 11);
    }

    [Fact]
    public void Adjacent_Strings_Are_Concatenated_And_Hex_Escapes_Work()
    {
        const string Source = """
            int main() {
                uchar *s = "ab" "\x41" "c";
                return (int)s[0] + s[1] + s[2] + s[3] + s[4] + '\x7f';
            }
            """;

        CcRun.Run(Source).Value.Should().Be(97 + 98 + 65 + 99 + 0 + 127);
    }

    [Fact]
    public void Macros_Stringize_And_Paste()
    {
        const string Source = """
            #define STR(x) #x
            #define CAT(a, b) a##b
            #define VAR(n) CAT(value, n)
            int main() {
                int value1 = 40;
                int value2 = 2;
                uchar *name = STR(hello   world);
                return VAR(1) + VAR(2) + name[0] + name[5] + name[6] - 104 - 32 - 119;
            }
            """;

        // "hello world": zwinięte spacje
        CcRun.Run(Source).Value.Should().Be(42);
    }

    [Fact]
    public void Enum_Values_May_Use_Sizeof_Struct_And_Offsetof()
    {
        const string Source = """
            #include <stddef.h>
            struct Rec { uchar tag; int value; uchar pad[3]; };
            enum { REC_SIZE = sizeof(struct Rec), NEXT, VALUE_AT = offsetof(struct Rec, value), LAST };
            uchar buffer[REC_SIZE * 2];
            int main() {
                return REC_SIZE * 1000 + NEXT * 100 + VALUE_AT * 10 + LAST + sizeof(buffer) * 0;
            }
            """;

        CcRun.Run(Source).Value.Should().Be((6 * 1000) + (7 * 100) + (1 * 10) + 2);
    }

    [Fact]
    public void Union_Initializer_Sets_Only_The_First_Member()
    {
        (int exit, string error) = CcRun.Compile("union U { int a; uchar b; };\nunion U u = {1, 2};\nint main() { return 0; }");

        exit.Should().NotBe(0);
        error.Should().Contain("union");
    }
}
