namespace CathodeRay.C;

/// <summary>Lowering: <c>float</c> (IEEE-754 pojedynczej precyzji jako 32 bity) liczony programowo procedurami <c>__cc_f*</c>
/// z <c>stdlib/portable/rt_float.c</c>; konwersje z i na typy całkowite, stałe, porównania i negacja.</summary>
internal sealed partial class Lowering
{
    private static CType ReturnCType(Ast.Function def)
    {
        string bare = TypeQualifiers.Split(def.ReturnType, out _, out _);
        return def.ReturnStars > 0 || bare.StartsWith("struct ", StringComparison.Ordinal) || bare.StartsWith("fptr", StringComparison.Ordinal) || bare == "void"
            ? CType.UInt
            : CType.FromName(bare);
    }

    /// <summary>Wołanie procedury wykonawczej: wynik w <c>Temp(depth)</c>.</summary>
    private Ir.Cell CallRt(string name, int resultWidth, int depth, params (Ir.Op Value, int Width)[] args)
    {
        Ir.Cell result = Temp(depth, resultWidth);
        Emit(new Ir.Call(name, null, [.. args.Select(static a => a.Value)], [.. args.Select(static a => a.Width)], result));
        return result;
    }

    /// <summary>Konwersja wartości między typami skalarnymi: <c>float</c> przez procedury wykonawcze, reszta przez rozszerzenie znakiem/zerem.</summary>
    private Ir.Op Convert(Ir.Op value, CType from, CType to, int depth) =>
        from.IsFloat || to.IsFloat ? FloatConvert(value, from, to, depth) : Widen(value, from, Width(to), depth);

    private Ir.Op FloatConvert(Ir.Op value, CType from, CType to, int depth)
    {
        if (from.IsFloat && to.IsFloat)
        {
            return value;
        }

        if (to.IsFloat)
        {
            if (value is Ir.Imm constant)
            {
                float folded = from.Kind switch
                {
                    "schar" => (sbyte)constant.Value,
                    "int" => (short)constant.Value,
                    "long" => constant.Value,
                    "ulong" => (uint)constant.Value,
                    "uchar" => (byte)constant.Value,
                    _ => (ushort)constant.Value,
                };
                return new Ir.Imm(BitConverter.SingleToInt32Bits(folded), 4);
            }

            switch (from.Kind)
            {
                case "long" or "ulong":
                    return CallRt(from.Kind == "long" ? "__cc_ltof" : "__cc_ultof", 4, depth, (value, 4));
                case "int" or "schar":
                    return CallRt("__cc_itof", 4, depth, (Widen(value, from, 2, depth), 2));
                default:
                    Ir.Op word = value;
                    if (value is Ir.Cell { W: 1 })
                    {
                        Ir.Cell wide = Temp(depth, 2);
                        Emit(new Ir.Mov(wide, value));
                        word = wide;
                    }

                    return CallRt("__cc_utof", 4, depth, (word, 2));
            }
        }

        int width = Width(to);
        if (value is Ir.Imm bits)
        {
            float real = BitConverter.Int32BitsToSingle(bits.Value);
            long truncated = float.IsNaN(real) ? 0 : (long)Math.Clamp(Math.Truncate(real), -2147483648.0, 4294967295.0);
            return new Ir.Imm(unchecked((int)truncated) & Mask(width), width);
        }

        Ir.Cell whole = CallRt(to.Kind == "ulong" ? "__cc_ftoul" : "__cc_ftol", 4, width == 4 ? depth : depth + 1, (value, 4));
        if (width == 4)
        {
            return whole;
        }

        Ir.Cell narrow = Temp(depth, width);
        Emit(new Ir.Mov(narrow, whole));
        return narrow;
    }

    /// <summary>Arytmetyka na <c>float</c>: oba operandy konwertowane, wynik z procedury <c>__cc_fadd/fsub/fmul/fdiv</c>.</summary>
    private Ir.Op FloatArith(string op, CType leftType, CType rightType, Ir.Op left, Ir.Op right, int depth)
    {
        Ir.Op a = Convert(left, leftType, CType.Float, depth + 2);
        Ir.Op b = Convert(right, rightType, CType.Float, depth + 3);
        string name = op switch
        {
            "+" => "__cc_fadd",
            "-" => "__cc_fsub",
            "*" => "__cc_fmul",
            "/" => "__cc_fdiv",
            _ => throw new CCodegenException($"operator '{op}' is not supported for float."),
        };
        return CallRt(name, 4, depth, (a, 4), (b, 4));
    }

    /// <summary>Skok po porównaniu, w którym występuje <c>float</c> (<c>__cc_flt/fle/feq</c>, argumenty zamieniane dla <c>&gt;</c>, <c>&gt;=</c>).</summary>
    private void FloatBranch(Ast.Binary compare, Ir.Op left, Ir.Op right, string target, bool whenTrue, int depth)
    {
        Ir.Op a = Convert(left, TypeOf(compare.Left), CType.Float, depth + 2);
        Ir.Op b = Convert(right, TypeOf(compare.Right), CType.Float, depth + 3);
        (string name, bool swap) = compare.Op switch
        {
            "<" => ("__cc_flt", false),
            "<=" => ("__cc_fle", false),
            ">" => ("__cc_flt", true),
            ">=" => ("__cc_fle", true),
            _ => ("__cc_feq", false),
        };
        Ir.Cell result = CallRt(name, 1, depth + 4, swap ? (b, 4) : (a, 4), swap ? (a, 4) : (b, 4));
        Ir.Cond truth = compare.Op == "!=" ? Ir.Cond.Eq : Ir.Cond.Ne;
        Emit(new Ir.BrCmp(whenTrue ? truth : Invert(truth), result, new Ir.Imm(0, 1), target));
    }

    /// <summary>Warunek <c>if (f)</c>: różne od zera (także <c>-0.0</c> to zero).</summary>
    private void FloatTruth(Ir.Op value, string target, bool whenTrue, int depth)
    {
        Ir.Cell result = CallRt("__cc_fnz", 1, depth + 1, (value, 4));
        Emit(new Ir.BrCmp(whenTrue ? Ir.Cond.Ne : Ir.Cond.Eq, result, new Ir.Imm(0, 1), target));
    }

    /// <summary>Stała <c>float</c> do danych początkowych: literał zmiennoprzecinkowy albo całkowity, ewentualnie ze znakiem minus.</summary>
    private bool TryFloatConstant(Ast.Expr? expr, out uint bits)
    {
        bits = 0;
        switch (expr)
        {
            case Ast.Number number when Literal.TryParse(number.Text, out Literal literal):
                bits = literal.Type.IsFloat ? (uint)literal.Value : (uint)BitConverter.SingleToInt32Bits(literal.Type.Kind == "int" ? (short)literal.Value : literal.Value);
                return true;
            case Ast.Unary { Op: "-" } negate when TryFloatConstant(negate.Operand, out uint inner):
                bits = inner ^ 0x80000000u;
                return true;
            default:
                if (expr is not null && TryConstValue(expr, out int constant))
                {
                    bits = (uint)BitConverter.SingleToInt32Bits((short)constant);
                    return true;
                }

                return false;
        }
    }
}
