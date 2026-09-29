using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 31, krok 15: pola bitowe.</summary>
public sealed class CBitfieldTests
{
    [Theory]
    [InlineData("stub")]
    [InlineData("6502")]
    [InlineData("z80")]
    [InlineData("6800")]
    public void Bitfields_Read_Write_And_Pack(string cpu)
    {
        const string Source = """
            struct Flags { uchar a : 3; uchar b : 5; uchar c : 4; int s : 4; uint w : 12; };
            struct Flags g;
            int main() {
                struct Flags f;
                struct Flags *p = &f;
                f.a = 5; f.b = 17; f.c = 9; f.s = -3; f.w = 4000;
                g.a = 7; g.s = 7;
                int r = 0;
                if (f.a == 5) r = r + 1;
                if (f.b == 17) r = r + 2;
                if (f.c == 9) r = r + 4;
                if (f.s == -3) r = r + 8;
                if (f.w == 4000) r = r + 16;
                if (g.a == 7 && g.s == 7 && g.c == 0) r = r + 32;
                p->a = p->a + 2;
                p->b += 3;
                p->s--;
                f.c++;
                if (f.a == 7 && f.b == 20 && f.s == -4 && f.c == 10 && f.w == 4000) r = r + 64;
                f.a = 9;
                if (f.a == 1 && f.b == 20) r = r + 128;
                return r + sizeof(struct Flags) * 256;
            }
            """;

        CcRun.RunOn(Source, cpu, "-Werror").Value.Should().Be(255 + (4 * 256));
    }

    [Fact]
    public void Bitfield_Initializer_Address_And_Errors()
    {
        CcRun.RunOn("struct B { uchar x : 2; uchar y : 3; uchar z; }; struct B g = { 3, 5, 9 }; int main() { struct B l = { 1, 2, 4 }; return g.x + (int)g.y * 10 + (int)g.z * 100 + l.x + l.y + l.z; }", "6502", "-Werror").Value.Should().Be(3 + 50 + 900 + 1 + 2 + 4);
        Action address = () => TypeChecker.Check(Parser.Parse("struct B { uchar x : 2; }; int main() { struct B b; uchar *p = &b.x; return 0; }"));
        address.Should().Throw<CTypeException>().WithMessage("*bit-field*");
        Action wide = () => TypeChecker.Check(Parser.Parse("struct B { uchar x : 9; }; int main() { return 0; }"));
        wide.Should().Throw<CTypeException>().WithMessage("*wider*");
    }
}
