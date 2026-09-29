using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 30, krok 8: zgodność operacji IR — każda operacja × szerokość × wartości brzegowe liczona na
/// interpreterze IR (wyrocznia) i na każdym celu (dziś stub, potem 6502 itd.).</summary>
public sealed class IrConformanceTests
{
    private static readonly int[] Bytes = [0, 1, 2, 7, 127, 128, 200, 254, 255];
    private static readonly int[] Words = [0, 1, 255, 256, 1000, 0x7FFF, 0x8000, 0xC001, 0xFFFF];
    private static readonly int[] ShiftCounts = [0, 1, 2, 3, 4, 8, 9, 15, 16, 17, 31];

    public static TheoryData<Ir.BinOp, int> ArithmeticOps()
    {
        var data = new TheoryData<Ir.BinOp, int>();
        foreach (Ir.BinOp op in new[] { Ir.BinOp.Add, Ir.BinOp.Sub, Ir.BinOp.And, Ir.BinOp.Or, Ir.BinOp.Xor, Ir.BinOp.Mul, Ir.BinOp.Div, Ir.BinOp.Mod, Ir.BinOp.Shl, Ir.BinOp.Shr })
        {
            data.Add(op, 1);
            data.Add(op, 2);
        }

        data.Add(Ir.BinOp.DivS, 2);
        data.Add(Ir.BinOp.ModS, 2);
        data.Add(Ir.BinOp.Sar, 2);
        return data;
    }

    private static int[] ValuesFor(int width) => width == 1 ? Bytes : Words;

    [Theory]
    [MemberData(nameof(ArithmeticOps))]
    public void Binary_Operations_Match_The_Oracle(Ir.BinOp op, int width)
    {
        var program = new IrProgram();
        Ir.Cell result = program.R(width);
        int[] rights = op is Ir.BinOp.Shl or Ir.BinOp.Shr or Ir.BinOp.Sar ? ShiftCounts : ValuesFor(width);
        foreach (int a in ValuesFor(width))
        {
            foreach (int b in rights)
            {
                int rightWidth = op is Ir.BinOp.Shl or Ir.BinOp.Shr or Ir.BinOp.Sar ? 1 : width;
                program.Emit(new Ir.Bin(op, result, new Ir.Imm(a, width), new Ir.Imm(b, rightWidth))).Record(result, $"{op} W{width} #{a:X}, #{b:X}");
                program.Emit(new Ir.Mov(program.A(width), new Ir.Imm(a, width)), new Ir.Mov(program.B(rightWidth), new Ir.Imm(b, rightWidth)))
                    .Emit(new Ir.Bin(op, result, program.A(width), program.B(rightWidth)))
                    .Record(result, $"{op} W{width} cells {a:X}, {b:X}");
            }
        }

        program.AssertConforms($"{op} W{width}");
    }

    [Theory]
    [InlineData(Ir.BinOp.Add)]
    [InlineData(Ir.BinOp.Sub)]
    [InlineData(Ir.BinOp.And)]
    [InlineData(Ir.BinOp.Or)]
    [InlineData(Ir.BinOp.Xor)]
    [InlineData(Ir.BinOp.Mul)]
    [InlineData(Ir.BinOp.Div)]
    [InlineData(Ir.BinOp.Mod)]
    public void Narrow_Operands_Are_Zero_Extended_Into_A_Wide_Result(Ir.BinOp op)
    {
        var program = new IrProgram();
        Ir.Cell result = program.R(2);
        foreach (int a in Bytes)
        {
            foreach (int b in new[] { 0, 1, 3, 255 })
            {
                program.Emit(new Ir.Bin(op, result, new Ir.Imm(a, 1), new Ir.Imm(b, 1))).Record(result, $"{op} bytes -> word #{a:X}, #{b:X}");
                program.Emit(new Ir.Mov(program.A(1), new Ir.Imm(a, 1)), new Ir.Mov(program.B(2), new Ir.Imm(b * 257, 2)))
                    .Emit(new Ir.Bin(op, result, program.A(1), program.B(2)))
                    .Record(result, $"{op} byte cell, word cell {a:X}, {b * 257:X}");
            }
        }

        program.AssertConforms($"{op} narrow");
    }

    [Theory]
    [InlineData(Ir.UnOp.Neg, 1)]
    [InlineData(Ir.UnOp.Neg, 2)]
    [InlineData(Ir.UnOp.Cpl, 1)]
    [InlineData(Ir.UnOp.Cpl, 2)]
    public void Unary_Operations_Match_The_Oracle(Ir.UnOp op, int width)
    {
        var program = new IrProgram();
        Ir.Cell result = program.R(width);
        foreach (int a in ValuesFor(width))
        {
            program.Emit(new Ir.Un(op, result, new Ir.Imm(a, width))).Record(result, $"{op} W{width} #{a:X}");
            program.Emit(new Ir.Mov(program.A(width), new Ir.Imm(a, width)), new Ir.Un(op, program.A(width), program.A(width))).Record(program.A(width), $"{op} W{width} in place {a:X}");
        }

        program.AssertConforms($"{op} W{width}");
    }

    [Fact]
    public void Moves_Convert_Widths()
    {
        var program = new IrProgram();
        foreach (int dstWidth in new[] { 1, 2 })
        {
            Ir.Cell result = program.R(dstWidth);
            foreach (int value in new[] { 0, 5, 200, 0x1234, 0xFFFF })
            {
                foreach (int srcWidth in new[] { 1, 2 })
                {
                    program.Emit(new Ir.Mov(result, new Ir.Imm(value, srcWidth))).Record(result, $"Mov W{dstWidth} <- imm W{srcWidth} {value:X}");
                    program.Emit(new Ir.Mov(program.A(srcWidth), new Ir.Imm(value, srcWidth)), new Ir.Mov(result, program.A(srcWidth)))
                        .Record(result, $"Mov W{dstWidth} <- cell W{srcWidth} {value:X}");
                }
            }
        }

        program.AssertConforms("Mov");
    }

    [Theory]
    [InlineData(Ir.Cond.Eq)]
    [InlineData(Ir.Cond.Ne)]
    [InlineData(Ir.Cond.Ltu)]
    [InlineData(Ir.Cond.Leu)]
    [InlineData(Ir.Cond.Gtu)]
    [InlineData(Ir.Cond.Geu)]
    [InlineData(Ir.Cond.Lt)]
    [InlineData(Ir.Cond.Le)]
    [InlineData(Ir.Cond.Gt)]
    [InlineData(Ir.Cond.Ge)]
    public void Conditional_Branches_Match_The_Oracle(Ir.Cond cond)
    {
        foreach ((int wa, int wb) in new[] { (1, 1), (2, 2), (1, 2), (2, 1) })
        {
            Branches(cond, wa, wb);
        }
    }

    private static void Branches(Ir.Cond cond, int wa, int wb)
    {
        bool signed = cond is Ir.Cond.Lt or Ir.Cond.Le or Ir.Cond.Gt or Ir.Cond.Ge;
        var program = new IrProgram();
        Ir.Cell result = program.R(1);
        {
            if (signed && wa == 1 && wb == 1)
            {
                return;
            }

            foreach (int a in wa == 1 ? Bytes : Words)
            {
                foreach (int b in wb == 1 ? Bytes : Words)
                {
                    EmitBranchCase(program, result, cond, new Ir.Imm(a, wa), new Ir.Imm(b, wb), $"{cond} #{a:X}(W{wa}) #{b:X}(W{wb})");
                    program.Emit(new Ir.Mov(program.A(wa), new Ir.Imm(a, wa)), new Ir.Mov(program.B(wb), new Ir.Imm(b, wb)));
                    EmitBranchCase(program, result, cond, program.A(wa), program.B(wb), $"{cond} cells {a:X}(W{wa}) {b:X}(W{wb})");
                }
            }
        }

        program.AssertConforms($"{cond} W{wa}/W{wb}");
    }

    private static void EmitBranchCase(IrProgram program, Ir.Cell result, Ir.Cond cond, Ir.Op a, Ir.Op b, string description)
    {
        string taken = program.NewLabel();
        string end = program.NewLabel();
        program.Emit(
            new Ir.Mov(result, new Ir.Imm(0, 1)),
            new Ir.BrCmp(cond, a, b, taken),
            new Ir.Jmp(end),
            new Ir.Label(taken),
            new Ir.Mov(result, new Ir.Imm(1, 1)),
            new Ir.Label(end));
        program.Record(result, description);
    }
}
