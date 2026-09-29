namespace CathodeRay.C;

/// <summary>Lowering: wyrażenia. <see cref="Value"/> zwraca operand z wartością: komórkę zmiennej, tymczasową
/// <c>Temp(depth)</c>, stałą albo adres; wewnętrzne tymczasowe leżą głębiej niż <c>depth</c>.</summary>
internal sealed partial class Lowering
{
    private static int Mask(int width) => width == 1 ? 0xFF : width == 2 ? 0xFFFF : -1;

    private static bool HasSideEffects(Ast.Expr expr) => expr switch
    {
        Ast.Call or Ast.CallExpr or Ast.Assign or Ast.AssignTo or Ast.AssignOpTo => true,
        Ast.Unary unary => HasSideEffects(unary.Operand),
        Ast.Cast cast => HasSideEffects(cast.Value),
        Ast.Comma => true,
        Ast.Binary binary => HasSideEffects(binary.Left) || HasSideEffects(binary.Right),
        Ast.Ternary ternary => HasSideEffects(ternary.Cond) || HasSideEffects(ternary.Then) || HasSideEffects(ternary.Else),
        Ast.Deref deref => HasSideEffects(deref.Pointer),
        Ast.Index index => HasSideEffects(index.Base) || HasSideEffects(index.Offset),
        Ast.Member member => HasSideEffects(member.Base),
        Ast.AddressOfExpr addressOf => HasSideEffects(addressOf.Target),
        _ => false,
    };

    private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;

    private static int Log2(int value) => System.Numerics.BitOperations.Log2((uint)value);

    /// <summary>Mnożenie przez potęgę dwójki to przesunięcie, dzielenie i reszta bez znaku przez potęgę dwójki to
    /// przesunięcie w prawo i maska; mnożenie i dzielenie przez 1 to kopia.</summary>
    private Ir.Op? ReduceStrength(Ir.BinOp kind, int width, Ir.Op left, Ir.Op right, int depth)
    {
        static bool Power(Ir.Op op, out int value)
        {
            value = op is Ir.Imm imm ? imm.Value : 0;
            return value > 0 && (value & (value - 1)) == 0;
        }

        Ir.Op? operand = null;
        int power = 0;
        if (kind == Ir.BinOp.Mul && Power(right, out int factor))
        {
            operand = left;
            power = factor;
        }
        else if (kind == Ir.BinOp.Mul && Power(left, out int leftFactor))
        {
            operand = right;
            power = leftFactor;
        }
        else if (kind is Ir.BinOp.Div or Ir.BinOp.Mod && Power(right, out int divisor))
        {
            operand = left;
            power = divisor;
        }

        if (operand is null)
        {
            return null;
        }

        Ir.Cell target = Temp(depth, width);
        int shift = Log2(power);
        switch (kind)
        {
            case Ir.BinOp.Mul when shift == 0:
            case Ir.BinOp.Div when shift == 0:
                Emit(new Ir.Mov(target, operand));
                return target;
            case Ir.BinOp.Mul:
                Emit(new Ir.Bin(Ir.BinOp.Shl, target, operand, new Ir.Imm(shift, 1)));
                return target;
            case Ir.BinOp.Div:
                Emit(new Ir.Bin(Ir.BinOp.Shr, target, operand, new Ir.Imm(shift, 1)));
                return target;
            default:
                Emit(new Ir.Bin(Ir.BinOp.And, target, operand, new Ir.Imm((power - 1) & Mask(width), width)));
                return target;
        }
    }

    /// <summary>Konwersja <c>int</c> → <c>long</c>/<c>ulong</c> z rozszerzeniem znakiem (pozostałe rozszerzenia to dopełnienie zerami,
    /// które robi sam IR).</summary>
    private Ir.Op Extend(Ir.Op value, CType from, bool toWide, int depth)
    {
        if (!toWide || from.Kind != "int" || (value is Ir.Cell { W: 4 } or Ir.Imm { W: 4 }))
        {
            return value;
        }

        if (value is Ir.Imm imm)
        {
            return new Ir.Imm(imm.W == 2 ? (short)imm.Value : imm.Value, 4);
        }

        Ir.Cell wide = Temp(depth, 4);
        string done = Label("sext");
        Emit(new Ir.Mov(wide, value));
        Emit(new Ir.BrCmp(Ir.Cond.Ltu, value, new Ir.Imm(0x8000, 2), done));
        Emit(new Ir.Bin(Ir.BinOp.Or, wide, wide, new Ir.Imm(unchecked((int)0xFFFF0000), 4)));
        Emit(new Ir.Label(done));
        return wide;
    }

    /// <summary>Indeks albo przesunięcie wskaźnika liczone na 16 bitach: 32-bitowa wartość jest obcinana.</summary>
    private Ir.Op To16(Ir.Op value, int depth)
    {
        switch (value)
        {
            case Ir.Imm { W: 4 } imm:
                return new Ir.Imm(imm.Value & 0xFFFF, 2);
            case Ir.Cell { W: 4 }:
                Ir.Cell narrow = Temp(depth, 2);
                Emit(new Ir.Mov(narrow, value));
                return narrow;
            default:
                return value;
        }
    }

    private Ir.Cell Dst(Ir.Cell? into, int width, int depth) =>
        into is not null && into.W == width ? into : Temp(depth, width);

    /// <summary>Wartość wyrażenia skalarnego. <paramref name="into"/> to preferowany cel wyniku (używany tylko tam,
    /// gdzie jest bezpieczny: działania bez efektów ubocznych na własnym celu, odczyty, wywołania).</summary>
    private Ir.Op Value(Ast.Expr expr, int depth, Ir.Cell? into = null)
    {
        if (expr is not Ast.Number && _constants.TryGetValue(expr, out int folded))
        {
            int width = WidthOf(expr);
            return new Ir.Imm(folded & Mask(width), width);
        }

        switch (expr)
        {
            case Ast.Number number:
            {
                if (Literal.TryParse(number.Text, out Literal literal) && literal.IsLong)
                {
                    return new Ir.Imm((int)literal.Value, 4);
                }

                TryNumber(number.Text, out int value);
                value &= 0xFFFF;
                return new Ir.Imm(value, value <= 0xFF ? 1 : 2);
            }

            case Ast.Var variable:
                return VarValue(variable);
            case Ast.Str str:
                return new Ir.AddrOf(StringLabel(str.Value), 0);
            case Ast.AddressOf addressOf:
                return AddressOfName(addressOf.Name);
            case Ast.AddressOfExpr addressOfExpr:
            {
                (Ir.Op pointer, int offset) = LValueAddr(addressOfExpr.Target, depth);
                return AddressValue(pointer, offset, depth);
            }

            case Ast.Call or Ast.CallExpr:
                return LowerCall(expr, depth, into);
            case Ast.Unary unary:
                return UnaryValue(unary, depth, into);
            case Ast.Cast cast:
                return CastValue(cast, depth);
            case Ast.Comma comma:
                _ = Value(comma.Left, depth);
                return Value(comma.Right, depth, into);
            case Ast.Binary binary:
                return BinaryValue(binary, depth, into);
            case Ast.Assign assign:
                AssignVariable(assign.Name, assign.Value, depth);
                return VariableOperand(assign.Name);
            case Ast.Ternary ternary:
                return TernaryValue(ternary, depth);
            case Ast.Deref { Pointer: var function } when KindOf(function) == "fptr":
                return Value(function, depth, into);
            case Ast.Deref or Ast.Index or Ast.Member:
                return LoadValue(expr, depth, into);
            case Ast.AssignTo assignTo:
                return AssignToValue(assignTo, depth);
            case Ast.AssignOpTo assignOp:
                return AssignOpValue(assignOp, depth);
            default:
                throw new CCodegenException($"unsupported expression {expr.GetType().Name}.");
        }
    }

    /// <summary>Rzutowanie: zawężenie do bajtu obcina, rozszerzenie do słowa uzupełnia zerem (jedyny typ 8-bitowy jest bez znaku),
    /// reszta zmienia tylko typ. Wynik ma szerokość typu docelowego.</summary>
    private Ir.Op CastValue(Ast.Cast cast, int depth)
    {
        Ir.Op value = Value(cast.Value, depth);
        int width = WidthOf(cast);
        value = Extend(value, TypeOf(cast.Value), width == 4, depth + 1);
        if (value switch { Ir.Cell c => c.W, Ir.Imm i => i.W, _ => 2 } == width)
        {
            return value;
        }

        if (value is Ir.Imm imm)
        {
            return new Ir.Imm(imm.Value & Mask(width), width);
        }

        Ir.Cell target = Temp(depth, width);
        Emit(new Ir.Mov(target, value));
        return target;
    }

    private Ir.Op VariableOperand(string name)
    {
        VarCell cell = _cells[name];
        return cell.Type.Kind is "array" or "struct" ? new Ir.AddrOf(cell.Sym, 0) : new Ir.Cell(cell.Sym, Width(cell.Type));
    }

    private Ir.Op VarValue(Ast.Var variable)
    {
        if (!_cells.ContainsKey(variable.Name) && _functions.ContainsKey(variable.Name))
        {
            return new Ir.AddrOf(variable.Name, 0);
        }

        VarCell cell = _cells[variable.Name];
        if (cell.Type.Kind == "struct")
        {
            throw new CCodegenException($"struct '{variable.Name}' used as a value.");
        }

        return VariableOperand(variable.Name);
    }

    private Ir.Op AddressOfName(string name) =>
        !_cells.TryGetValue(name, out VarCell? cell) && _functions.ContainsKey(name)
            ? new Ir.AddrOf(name, 0)
            : new Ir.AddrOf(_cells[name].Sym, 0);

    /// <summary>Przypisanie do zmiennej (także struktury i tablicy inicjalizowanej wyrażeniem).</summary>
    private void AssignVariable(string name, Ast.Expr value, int depth)
    {
        VarCell cell = _cells[name];
        if (cell.Type.Kind == "struct")
        {
            (Ir.Op sourcePointer, int sourceOffset) = LValueAddr(value, depth);
            Emit(new Ir.CopyBlock(new Ir.AddrOf(cell.Sym, 0), AddressValue(sourcePointer, sourceOffset, depth), cell.Type.Size));
            return;
        }

        var dst = new Ir.Cell(cell.Sym, Width(cell.Type));
        Ir.Op source = Extend(Value(value, depth, dst), TypeOf(value), dst.W == 4, depth + 1);
        if (source is not Ir.Cell same || same.Sym != dst.Sym)
        {
            Emit(new Ir.Mov(dst, source));
        }
    }

    private Ir.Op UnaryValue(Ast.Unary unary, int depth, Ir.Cell? into)
    {
        if (unary.Op == "!")
        {
            return BoolValue(unary, depth);
        }

        Ir.Op operand = Value(unary.Operand, depth);
        int width = WidthOf(unary);
        Ir.Cell dst = Dst(into, width, depth);
        Emit(new Ir.Un(unary.Op == "-" ? Ir.UnOp.Neg : Ir.UnOp.Cpl, dst, operand));
        return dst;
    }

    private Ir.Op BinaryValue(Ast.Binary binary, int depth, Ir.Cell? into)
    {
        if (binary.Op is "&&" or "||" or "==" or "!=" or "<" or "<=" or ">" or ">=")
        {
            return BoolValue(binary, depth);
        }

        Ir.Op left = Value(binary.Left, depth);
        if (left is Ir.Cell variable && !IsTemp(variable) && HasSideEffects(binary.Right))
        {
            Ir.Cell copy = Temp(depth, variable.W);
            Emit(new Ir.Mov(copy, left));
            left = copy;
        }

        Ir.Op right = Value(binary.Right, depth + 1);
        return Arith(binary.Op, TypeOf(binary), TypeOf(binary.Left), TypeOf(binary.Right), left, right, depth, into);
    }

    /// <summary>Działanie na dwóch operandach z arytmetyką wskaźników (skalowanie, różnica) i doborem wariantu
    /// ze znakiem / bez znaku.</summary>
    private Ir.Op Arith(string op, CType result, CType leftType, CType rightType, Ir.Op left, Ir.Op right, int depth, Ir.Cell? into)
    {
        bool leftPointer = leftType.Kind == "ptr";
        bool rightPointer = rightType.Kind == "ptr";
        if (op == "-" && leftPointer && rightPointer)
        {
            Ir.Cell difference = Dst(into, 2, depth);
            Emit(new Ir.Bin(Ir.BinOp.Sub, difference, left, right));
            int size = leftType.Base?.Size ?? 1;
            if (size > 1)
            {
                Emit(IsPowerOfTwo(size)
                    ? new Ir.Bin(Ir.BinOp.Sar, difference, difference, new Ir.Imm(Log2(size), 1))
                    : new Ir.Bin(Ir.BinOp.DivS, difference, difference, new Ir.Imm(size, size <= 0xFF ? 1 : 2)));
            }

            return difference;
        }

        if (op is "+" or "-" && (leftPointer || rightPointer))
        {
            Ir.Op pointer = leftPointer ? left : right;
            Ir.Op index = To16(leftPointer ? right : left, depth + 2);
            int size = (leftPointer ? leftType : rightType).Base?.Size ?? 1;
            Ir.Cell dst = Dst(into, 2, depth);
            Ir.Op scaled = Scale(index, size, depth + 1);
            Emit(new Ir.Bin(op == "+" ? Ir.BinOp.Add : Ir.BinOp.Sub, dst, pointer, scaled));
            return dst;
        }

        int width = Width(result);
        if (width == 4)
        {
            left = Extend(left, leftType, true, depth + 2);
            if (op is not ("<<" or ">>"))
            {
                right = Extend(right, rightType, true, depth + 3);
            }
        }

        bool unsignedOperands = leftType.Kind is "uint" or "ulong" || rightType.Kind is "uint" or "ulong" || width == 1;
        Ir.BinOp kind = op switch
        {
            "+" => Ir.BinOp.Add,
            "-" => Ir.BinOp.Sub,
            "*" => Ir.BinOp.Mul,
            "/" => unsignedOperands ? Ir.BinOp.Div : Ir.BinOp.DivS,
            "%" => unsignedOperands ? Ir.BinOp.Mod : Ir.BinOp.ModS,
            "&" => Ir.BinOp.And,
            "|" => Ir.BinOp.Or,
            "^" => Ir.BinOp.Xor,
            "<<" => Ir.BinOp.Shl,
            ">>" => width >= 2 && leftType.Kind is "int" or "long" ? Ir.BinOp.Sar : Ir.BinOp.Shr,
            _ => throw new CCodegenException($"unknown operator '{op}'."),
        };
        if (ReduceStrength(kind, width, left, right, depth) is { } reduced)
        {
            return reduced;
        }

        bool aliasSafe = kind is Ir.BinOp.Add or Ir.BinOp.Sub or Ir.BinOp.And or Ir.BinOp.Or or Ir.BinOp.Xor;
        Ir.Cell target = aliasSafe ? Dst(into, width, depth) : Temp(depth, width);
        Emit(new Ir.Bin(kind, target, left, right));
        return target;
    }

    /// <summary>Mnoży indeks przez rozmiar elementu (przesunięcie dla potęg dwójki, inaczej mnożenie).</summary>
    private Ir.Op Scale(Ir.Op index, int size, int depth)
    {
        if (size == 1)
        {
            return index;
        }

        if (index is Ir.Imm constant)
        {
            return new Ir.Imm((constant.Value * size) & 0xFFFF, 2);
        }

        Ir.Cell dst = Temp(depth, 2);
        Emit(IsPowerOfTwo(size)
            ? new Ir.Bin(Ir.BinOp.Shl, dst, index, new Ir.Imm(Log2(size), 1))
            : new Ir.Bin(Ir.BinOp.Mul, dst, index, new Ir.Imm(size, size <= 0xFF ? 1 : 2)));
        return dst;
    }

    private Ir.Op TernaryValue(Ast.Ternary ternary, int depth)
    {
        int width = WidthOf(ternary);
        Ir.Cell result = Temp(depth, width);
        string els = Label("telse");
        string done = Label("tdone");
        Branch(ternary.Cond, els, whenTrue: false, depth + 1);
        Emit(new Ir.Mov(result, Extend(Value(ternary.Then, depth + 1), TypeOf(ternary.Then), width == 4, depth + 2)));
        Emit(new Ir.Jmp(done));
        Emit(new Ir.Label(els));
        Emit(new Ir.Mov(result, Extend(Value(ternary.Else, depth + 1), TypeOf(ternary.Else), width == 4, depth + 2)));
        Emit(new Ir.Label(done));
        return result;
    }

    /// <summary>Wartość logiczna (0/1) warunku jako uchar.</summary>
    private Ir.Op BoolValue(Ast.Expr condition, int depth)
    {
        Ir.Cell result = Temp(depth, 1);
        string isFalse = Label("bfalse");
        string done = Label("bdone");
        Branch(condition, isFalse, whenTrue: false, depth + 1);
        Emit(new Ir.Mov(result, new Ir.Imm(1, 1)));
        Emit(new Ir.Jmp(done));
        Emit(new Ir.Label(isFalse));
        Emit(new Ir.Mov(result, new Ir.Imm(0, 1)));
        Emit(new Ir.Label(done));
        return result;
    }

    private Ir.Op AssignToValue(Ast.AssignTo assignTo, int depth)
    {
        CType element = TypeOf(assignTo.Target);
        if (element.Kind == "struct")
        {
            (Ir.Op targetPointer, int targetOffset) = LValueAddr(assignTo.Target, depth);
            Ir.Op destination = AddressValue(targetPointer, targetOffset, depth);
            (Ir.Op sourcePointer, int sourceOffset) = LValueAddr(assignTo.Value, depth + 1);
            Emit(new Ir.CopyBlock(destination, AddressValue(sourcePointer, sourceOffset, depth + 1), element.Size));
            return new Ir.Imm(0, 1);
        }

        Ir.Op value = Extend(Value(assignTo.Value, depth), TypeOf(assignTo.Value), element.Size == 4, depth + 2);
        (Ir.Op pointer, int offset) = LValueAddr(assignTo.Target, depth + 1);
        Emit(new Ir.Store(pointer, offset, value, Width(element)));
        return value;
    }

    /// <summary>Złożone przypisanie przez wskaźnik (<c>*p += v</c>, <c>a[i++]--</c>): adres celu liczony raz.</summary>
    private Ir.Op AssignOpValue(Ast.AssignOpTo assign, int depth)
    {
        CType element = TypeOf(assign.Target);
        int width = Width(element);
        (Ir.Op pointer, int offset) = LValueAddr(assign.Target, depth);
        Ir.Cell current = Temp(depth + 1, width);
        Emit(new Ir.Load(current, pointer, offset, width));
        Ir.Op operand = Value(assign.Value, depth + 2);
        Ir.Op result = Arith(assign.Op, TypeOf(assign.Combined), element, TypeOf(assign.Value), current, operand, depth + 1, null);
        Emit(new Ir.Store(pointer, offset, result, width));
        return result;
    }
}
