using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class CTypesTests
{
    private static CheckedProgram Check(string source) =>
        TypeChecker.Check(Parser.Parse(source));

    [Fact]
    public void Globals_Params_And_Locals_Get_Types()
    {
        CheckedProgram program = Check("uchar g; int h = 3; int f(uchar a, int *p) { int x = a; uchar y; return x; }");

        program.Globals.Should().Equal(new TypedSymbol("g", CType.UChar), new TypedSymbol("h", CType.Int, new Ast.Number("3")));
        CheckedFunction function = program.Functions.Should().ContainSingle().Subject;
        function.Params.Should().Equal(new TypedSymbol("a", CType.UChar), new TypedSymbol("p", CType.Pointer(CType.Int)));
        function.Locals.Should().Equal(new TypedSymbol("x", CType.Int), new TypedSymbol("y", CType.UChar));
        program.Warnings.Where(static w => !w.Contains("unused ") && !w.Contains("without returning")).Should().BeEmpty();
    }

    [Fact]
    public void Narrowing_Warns_But_Passes()
    {
        CheckedProgram program = Check("int f() { uchar x; x = 300; return x; }");

        program.Warnings.Should().ContainSingle().Which.Should().Contain("narrowing int to uchar");
    }

    [Fact]
    public void Pointers_Deref_Index_And_Address()
    {
        CheckedProgram program = Check("int f(int *p, uchar *q) { int x = *p; uchar y = q[1]; int *r = p; uchar *s = &y; return x; }");

        program.Warnings.Where(static w => !w.Contains("unused ") && !w.Contains("without returning")).Should().BeEmpty();
    }

    [Fact]
    public void Arrays_Decay_To_Pointers_And_Index()
    {
        CheckedProgram program = Check("uchar g[4]; int f() { int l[2]; uchar *p = g; int *q = l + 1; l[0] = 1; g[3] = p[0]; q[0] = 2; return l[1] + q[0]; }");

        program.Warnings.Where(static w => !w.Contains("unused ") && !w.Contains("without returning")).Should().BeEmpty();
    }

    [Fact]
    public void AddressOf_Keeps_Variable_Type()
    {
        CheckedProgram program = Check("int f() { int x = 1; int *p = &x; uchar y = 2; uchar *q = &y; return *p + *q; }");

        program.Warnings.Where(static w => !w.Contains("unused ") && !w.Contains("without returning")).Should().BeEmpty();
    }

    [Fact]
    public void Pointer_Arithmetic_Keeps_Base_Type()
    {
        CheckedProgram program = Check("int f() { uchar b[4]; uchar *p = b; p = p + 2; int g[2]; int *q = g + 1; q = 1 + q; return *p + *q; }");

        program.Warnings.Where(static w => !w.Contains("unused ") && !w.Contains("without returning")).Should().BeEmpty();
    }

    [Fact]
    public void Null_Pointer_Assigns_To_Ptr()
    {
        CheckedProgram program = Check("int f() { int *p = 0; p = 0; return 0; }");

        program.Warnings.Where(static w => !w.Contains("unused ") && !w.Contains("without returning")).Should().BeEmpty();
    }

    [Fact]
    public void Shadowing_In_Nested_Block_Is_Allowed()
    {
        CheckedProgram program = Check("int f() { int x = 1; { int x = 2; x = 3; } return x; }");

        program.Functions[0].Locals.Should().HaveCount(2);
    }

    public static TheoryData<string> TypeErrors()
    {
        return new TheoryData<string>
        {
            { "int f() { return 1; } int f() { return 2; }" },
            { "int x; int x;" },
            { "int f(int a, int a) { return a; }" },
            { "int f() { int x; int x; }" },
            { "int f() { return nope; }" },
            { "int f() { int *p; int x = p; return x; }" },
            { "int f() { int x; int *p = x; return 0; }" },
            { "void g() { } int f() { int x = g(); return x; }" },
            { "int f() { if (g()) return 1; return 0; }" },
            { "int g(int a) { return a; } int f() { return g(1, 2); }" },
            { "int g(int *p) { return 0; } int f() { int x; return g(x); }" },
            { "void f() { return 1; }" },
            { "int f() { return; }" },
            { "int f() { int x = &y; return 0; }" },
            { "int f() { void x; return 0; }" },
        };
    }

    [Theory]
    [MemberData(nameof(TypeErrors))]
    public void Checker_Rejects(string source)
    {
        FluentActions.Invoking(() => Check(source))
            .Should().Throw<CTypeException>();
    }

    [Fact]
    public void Checker_Message_Names_Symbol()
    {
        FluentActions.Invoking(() => Check("int f() { return nope; }"))
            .Should().Throw<CTypeException>().WithMessage("*undeclared 'nope'*");
    }
}
