using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class M6809TargetTests
{
    [Fact]
    public void Compiles_And_Runs_Minimal_C_Program()
    {
        CcRun.RunOn("int main() { return 7; }", "6809").Value.Should().Be(7);
    }

    [Fact]
    public void Compiles_And_Runs_Byte_And_Word_Arithmetic()
    {
        const string source = "int main() { int x = 3; int y = 4; return x + y * 2; }";

        CcRun.RunOn(source, "6809").Value.Should().Be(11);
    }

    [Theory]
    [InlineData("int main() { uchar n = 255; n++; return n; }", 0)]
    [InlineData("int main() { int n = 255; n++; return n; }", 256)]
    [InlineData("int main() { int n = 0; n--; return n; }", 65535)]
    public void Direct_Page_Step_Preserves_Increment_And_Decrement_Results(string source, int expected) =>
        CcRun.RunOn(source, "6809").Value.Should().Be(expected);

    [Fact]
    public void Two_Pointer_Copy_Loop_Uses_X_And_Y_And_Handles_Zero_Count()
    {
        const string source = """
            uchar src[12] = {10,20,30,40,50,60,70,80,90,100,110,120};
            uchar dst[12] = {0};
            void copy(uchar *d, uchar *s, uint n) { while (n) { *d = *s; d++; s++; n--; } }
            int main() { copy(dst, src, 9); copy(dst, src, 0); return dst[0] + dst[3] + dst[8]; }
            """;

        CcRun.RunOn(source, "6809").Value.Should().Be(140);
        CcRun.RunOn(source, "6809", "--ir", "vreg").Value.Should().Be(140);
    }

    [Fact]
    public void Struct_Copy_Through_Runtime_Preserves_Bytes()
    {
        const string source = "struct V { int x; int y; }; int main() { struct V a; struct V b; a.x=3; a.y=4; b=a; return b.x*10+b.y; }";

        CcRun.RunOn(source, "6809").Value.Should().Be(34);
    }

    [Fact]
    public void Recursive_Struct_Parameter_Uses_Stacked_Copy_Correctly()
    {
        const string source = "struct V { int x; int y; }; struct V make(int x,int y) { struct V v; v.x=x; v.y=y; return v; } struct V add(struct V a,struct V b) { a.x=a.x+b.x; a.y=a.y+b.y; return a; } struct V scale(struct V v,int n) { if(n==0) return make(0,0); return add(v,scale(v,n-1)); } int main() { struct V p=make(3,4); struct V s=scale(p,5); return s.x*10+s.y; }";

        CcRun.RunOn(source, "6809").Value.Should().Be(170);
    }

    [Fact]
    public void Nine_Byte_Struct_Copy_Through_Runtime_Preserves_Bytes()
    {
        const string source = "struct V { int x; int y; }; struct Box { struct V lo; struct V hi; uchar tag; }; int main() { struct Box a; struct Box b; a.lo.x=1; a.lo.y=2; a.hi.x=10; a.hi.y=20; a.tag=7; b=a; return b.hi.x*10+b.hi.y+b.tag; }";

        CcRun.RunOn(source, "6809").Value.Should().Be(127);
    }

    [Fact]
    public void Abi_V2_Passes_Word_Arguments_In_X_Y_U_And_Falls_Back_To_Memory()
    {
        const string source = "int combine(int a, int b, int c, int d) { return a + b * 10 + c * 100 + d * 1000; } int main() { return combine(1, 2, 3, 4); }";

        CcRun.RunOn(source, "6809", "--abi", "v2").Value.Should().Be(4321);
        CcRun.RunOn(source, "6809", "--abi", "v2", "--ir", "vreg").Value.Should().Be(4321);
    }

    [Fact]
    public void Abi_V2_Indirect_Call_Preserves_Register_Arguments()
    {
        const string source = "int combine(int a, int b, int c) { return a + b * 10 + c * 100; } int main() { int (*fn)(int, int, int) = combine; return fn(1, 2, 3); }";

        CcRun.RunOn(source, "6809", "--abi", "v2").Value.Should().Be(321);
    }

    [Fact]
    public void Abi_V2_Preserves_Struct_By_Value_And_Recursion()
    {
        string source = File.ReadAllText(Repo.Path("samples", "minic", "19_struct_value.c"));

        CcRun.RunOn(source, "6809", "--incdir", Repo.Path("samples", "minic")).Value.Should().Be(936);
        CcRun.RunOn(source, "6809", "--abi", "v2", "--incdir", Repo.Path("samples", "minic")).Value.Should().Be(936);
    }

    [Fact]
    public void Abi_V2_Recursive_Aggregate_Parameter_Preserves_Value()
    {
        const string source = "struct V { int x; int y; }; struct V make(int x, int y) { struct V v; v.x=x; v.y=y; return v; } struct V add(struct V a, struct V b) { a.x=a.x+b.x; a.y=a.y+b.y; return a; } struct V scale(struct V v, int n) { if (n == 0) return make(0,0); return add(v,scale(v,n-1)); } int main() { struct V p=make(3,4); struct V s=scale(p,3); return s.x*10+s.y; }";

        CcRun.RunOn(source, "6809", "--abi", "v2").Value.Should().Be(102);
    }

    [Fact]
    public void Abi_V2_Large_Aggregate_Parameter_Preserves_Value()
    {
        const string source = "struct V { int x; int y; }; struct Box { struct V lo; struct V hi; uchar tag; }; struct V make(int x, int y) { struct V v; v.x=x; v.y=y; return v; } struct V add(struct V a, struct V b) { a.x=a.x+b.x; a.y=a.y+b.y; return a; } int area(struct Box b) { return (b.hi.x-b.lo.x)*(b.hi.y-b.lo.y); } int main() { struct Box b; b.lo=make(1,2); b.hi=add(b.lo,make(11,22)); return area(b); }";

        CcRun.RunOn(source, "6809", "--abi", "v2").Value.Should().Be(242);
    }

    [Fact]
    public void Abi_V2_Struct_Return_Uses_Memory_Return_Buffer()
    {
        const string source = "struct V { int x; int y; }; struct V make(int x, int y) { struct V v; v.x=x; v.y=y; return v; } int main() { struct V p=make(3,4); return p.x*10+p.y; }";

        CcRun.RunOn(source, "6809", "--abi", "v2").Value.Should().Be(34);
    }

    [Fact]
    public void Abi_V2_Aggregate_Arguments_Remain_Memory_Arguments()
    {
        const string source = "struct V { int x; int y; }; struct V make(int x, int y) { struct V v; v.x=x; v.y=y; return v; } struct V add(struct V a, struct V b) { a.x=a.x+b.x; a.y=a.y+b.y; return a; } int main() { struct V a=make(3,4); struct V b=make(10,20); struct V c=add(a,b); return c.x*10+c.y; }";

        CcRun.RunOn(source, "6809", "--abi", "v2").Value.Should().Be(154);
    }
}
