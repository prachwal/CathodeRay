namespace CathodeRay.C;

/// <summary>Lowering: warunki i skoki.</summary>
internal sealed partial class Lowering
{
    private static Ir.Cond Invert(Ir.Cond condition) => condition switch
    {
        Ir.Cond.Eq => Ir.Cond.Ne,
        Ir.Cond.Ne => Ir.Cond.Eq,
        Ir.Cond.Lt => Ir.Cond.Ge,
        Ir.Cond.Ge => Ir.Cond.Lt,
        Ir.Cond.Le => Ir.Cond.Gt,
        Ir.Cond.Gt => Ir.Cond.Le,
        Ir.Cond.Ltu => Ir.Cond.Geu,
        Ir.Cond.Geu => Ir.Cond.Ltu,
        Ir.Cond.Leu => Ir.Cond.Gtu,
        _ => Ir.Cond.Leu,
    };

    /// <summary>Warunek porównania: bez znaku, gdy oba operandy są bajtami albo któryś jest uint/wskaźnikiem.</summary>
    private Ir.Cond ConditionFor(string op, Ast.Expr left, Ast.Expr right)
    {
        bool signedBytes = KindOf(left) == "schar" || KindOf(right) == "schar";
        bool unsignedCompare = (WidthOf(left) == 1 && WidthOf(right) == 1 && !signedBytes)
            || KindOf(left) is "uint" or "ulong" or "ullong" or "ptr" or "fptr" || KindOf(right) is "uint" or "ulong" or "ullong" or "ptr" or "fptr";
        return op switch
        {
            "==" => Ir.Cond.Eq,
            "!=" => Ir.Cond.Ne,
            "<" => unsignedCompare ? Ir.Cond.Ltu : Ir.Cond.Lt,
            "<=" => unsignedCompare ? Ir.Cond.Leu : Ir.Cond.Le,
            ">" => unsignedCompare ? Ir.Cond.Gtu : Ir.Cond.Gt,
            ">=" => unsignedCompare ? Ir.Cond.Geu : Ir.Cond.Ge,
            _ => throw new CCodegenException($"unknown comparison '{op}'."),
        };
    }

    /// <summary>Skok do <paramref name="target"/>, gdy wartość warunku jest równa <paramref name="whenTrue"/>.</summary>
    private void Branch(Ast.Expr condition, string target, bool whenTrue, int depth)
    {
        if (TryConstValue(condition, out int constant))
        {
            if ((constant != 0) == whenTrue)
            {
                Emit(new Ir.Jmp(target));
            }

            return;
        }

        switch (condition)
        {
            case Ast.Binary { Op: "&&" } and:
                if (whenTrue)
                {
                    string skip = Label("and");
                    Branch(and.Left, skip, whenTrue: false, depth);
                    Branch(and.Right, target, whenTrue: true, depth);
                    Emit(new Ir.Label(skip));
                }
                else
                {
                    Branch(and.Left, target, whenTrue: false, depth);
                    Branch(and.Right, target, whenTrue: false, depth);
                }

                return;
            case Ast.Binary { Op: "||" } or:
                if (whenTrue)
                {
                    Branch(or.Left, target, whenTrue: true, depth);
                    Branch(or.Right, target, whenTrue: true, depth);
                }
                else
                {
                    string skip = Label("or");
                    Branch(or.Left, skip, whenTrue: true, depth);
                    Branch(or.Right, target, whenTrue: false, depth);
                    Emit(new Ir.Label(skip));
                }

                return;
            case Ast.Unary { Op: "!" } not:
                Branch(not.Operand, target, !whenTrue, depth);
                return;
            case Ast.Binary { Op: "==" or "!=" or "<" or "<=" or ">" or ">=" } compare:
            {
                Ir.Op left = Value(compare.Left, depth);
                if (left is Ir.Cell variable && !IsTemp(variable) && HasSideEffects(compare.Right))
                {
                    Ir.Cell copy = Temp(depth, variable.W);
                    Emit(new Ir.Mov(copy, left));
                    left = copy;
                }

                Ir.Op right = Value(compare.Right, depth + 1);
                if (KindOf(compare.Left) == "float" || KindOf(compare.Right) == "float")
                {
                    FloatBranch(compare, left, right, target, whenTrue, depth);
                    return;
                }

                int compareWidth = Math.Max(WidthOf(compare.Left), WidthOf(compare.Right));
                if (KindOf(compare.Left) == "schar" || KindOf(compare.Right) == "schar")
                {
                    compareWidth = Math.Max(compareWidth, 2);
                }

                if (compareWidth >= 2)
                {
                    left = Widen(left, TypeOf(compare.Left), compareWidth, depth + 2);
                    right = Widen(right, TypeOf(compare.Right), compareWidth, depth + 3);
                }

                Ir.Cond cond = ConditionFor(compare.Op, compare.Left, compare.Right);
                Emit(new Ir.BrCmp(whenTrue ? cond : Invert(cond), left, right, target));
                return;
            }

            default:
            {
                Ir.Op value = Value(condition, depth);
                if (KindOf(condition) == "float")
                {
                    FloatTruth(value, target, whenTrue, depth);
                    return;
                }

                int width = value switch
                {
                    Ir.Cell cell => cell.W,
                    Ir.Imm imm => imm.W,
                    _ => 2,
                };
                Emit(new Ir.BrCmp(whenTrue ? Ir.Cond.Ne : Ir.Cond.Eq, value, new Ir.Imm(0, width), target));
                return;
            }
        }
    }
}
