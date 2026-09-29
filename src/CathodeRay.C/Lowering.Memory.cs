namespace CathodeRay.C;

/// <summary>Lowering: adresy lwartości (wskaźniki, tablice, pola) i odczyty spod nich.</summary>
internal sealed partial class Lowering
{
    /// <summary>Adres lwartości jako para (wskaźnik bazowy, stałe przesunięcie). Wskaźnik to komórka 16-bitowa
    /// (zwykle <c>Temp(depth)</c> albo zmienna) albo <see cref="Ir.AddrOf"/>, czyli adres znany w czasie linkowania.</summary>
    private (Ir.Op Pointer, int Offset) LValueAddr(Ast.Expr expr, int depth)
    {
        switch (expr)
        {
            case Ast.Var variable:
                return (new Ir.AddrOf(_cells[variable.Name].Sym, 0), 0);
            case Ast.Deref deref:
                return (Value(deref.Pointer, depth), 0);
            case Ast.Call or Ast.CallExpr when TypeOf(expr).Kind == "struct":
                return (LowerCall(expr, depth, null), 0);
            case Ast.Index index:
            {
                CType element = TypeOf(index.Base).Base ?? TypeOf(index);
                int size = element.Size;
                Ir.Op basePointer = Value(index.Base, depth);
                if (TryConstValue(index.Offset, out int position))
                {
                    return (basePointer, (short)position * size);
                }

                Ir.Op offset = To16(Value(index.Offset, depth + 1), depth + 2);
                Ir.Op scaled = Scale(offset, size, depth + 1);
                Ir.Cell sum = Temp(depth, 2);
                Emit(new Ir.Bin(Ir.BinOp.Add, sum, basePointer, scaled));
                return (sum, 0);
            }

            case Ast.Member member:
            {
                int fieldOffset = FieldOf(member).Offset;
                if (member.Arrow)
                {
                    return (Value(member.Base, depth), fieldOffset);
                }

                (Ir.Op pointer, int offset) = LValueAddr(member.Base, depth);
                return (pointer, offset + fieldOffset);
            }

            default:
                throw new CCodegenException($"'{expr.GetType().Name}' is not an lvalue.");
        }
    }

    /// <summary>Adres <c>pointer + offset</c> jako wartość (stała, gdy wskaźnik jest adresem symbolu).</summary>
    private Ir.Op AddressValue(Ir.Op pointer, int offset, int depth)
    {
        if (offset == 0)
        {
            return pointer;
        }

        switch (pointer)
        {
            case Ir.AddrOf address:
                return new Ir.AddrOf(address.Sym, address.Off + offset);
            case Ir.Imm constant:
                return new Ir.Imm((constant.Value + offset) & 0xFFFF, 2);
            default:
                Ir.Cell sum = Temp(depth, 2);
                Emit(new Ir.Bin(Ir.BinOp.Add, sum, pointer, new Ir.Imm(offset & 0xFFFF, 2)));
                return sum;
        }
    }

    /// <summary>Odczyt spod adresu lwartości (element tablicy, pole, <c>*p</c>); tablica-pole rozpada się na adres.</summary>
    private Ir.Op LoadValue(Ast.Expr expr, int depth, Ir.Cell? into)
    {
        CType type = TypeOf(expr);
        if (expr is Ast.Member member && FieldOf(member).Type.Kind == "array")
        {
            (Ir.Op fieldPointer, int fieldOffset) = LValueAddr(member, depth);
            return AddressValue(fieldPointer, fieldOffset, depth);
        }

        if (type.Kind == "struct")
        {
            throw new CCodegenException("struct used as a value.");
        }

        if (expr is Ast.Index { Base: var indexed } && TypeOf(indexed).Base is { Kind: "array" })
        {
            // element tablicy tablic rozpada się na adres wiersza
            (Ir.Op rowPointer, int rowOffset) = LValueAddr(expr, depth);
            return AddressValue(rowPointer, rowOffset, depth);
        }

        if (expr is Ast.Deref { Pointer: var dereferenced } && TypeOf(dereferenced).Base is { Kind: "array" })
        {
            return Value(dereferenced, depth);
        }

        (Ir.Op pointer, int offset) = LValueAddr(expr, depth);
        if (expr is Ast.Member { } bitMember && FieldOf(bitMember) is { BitWidth: > 0 } bitField)
        {
            return ReadBits(pointer, offset, bitField, depth, into);
        }

        int width = Width(type);
        Ir.Cell dst = Dst(into, width, depth);
        Emit(new Ir.Load(dst, pointer, offset, width, type.IsVolatile));
        return dst;
    }

    /// <summary>Odczyt pola bitowego: jednostka pamięci, przesunięcie i maska (z rozszerzeniem znaku dla typów ze znakiem).
    /// Obliczenia na 16 bitach, wynik zwężony do szerokości typu pola.</summary>
    private Ir.Cell ReadBits(Ir.Op pointer, int offset, StructField field, int depth, Ir.Cell? into = null)
    {
        int width = Width(field.Type);
        Ir.Cell unit = Temp(depth + 1, width);
        Emit(new Ir.Load(unit, pointer, offset, width, field.Type.IsVolatile));
        Ir.Cell wide = Temp(depth + 2, 2);
        Emit(new Ir.Mov(wide, unit));
        if (field.Type.Kind is "int" or "schar")
        {
            int left = 16 - field.BitShift - field.BitWidth;
            if (left > 0)
            {
                Emit(new Ir.Bin(Ir.BinOp.Shl, wide, wide, new Ir.Imm(left, 1)));
            }

            Emit(new Ir.Bin(Ir.BinOp.Sar, wide, wide, new Ir.Imm(16 - field.BitWidth, 1)));
        }
        else
        {
            if (field.BitShift > 0)
            {
                Emit(new Ir.Bin(Ir.BinOp.Shr, wide, wide, new Ir.Imm(field.BitShift, 1)));
            }

            Emit(new Ir.Bin(Ir.BinOp.And, wide, wide, new Ir.Imm((1 << field.BitWidth) - 1, 2)));
        }

        Ir.Cell result = Dst(into, width, depth);
        Emit(new Ir.Mov(result, wide));
        return result;
    }

    /// <summary>Zapis pola bitowego: odczyt jednostki, wyczyszczenie pola, wstawienie wartości, zapis.</summary>
    private void WriteBits(Ir.Op pointer, int offset, Ir.Op value, StructField field, int depth)
    {
        int width = Width(field.Type);
        int mask = (1 << field.BitWidth) - 1;
        Ir.Cell unit = Temp(depth + 1, width);
        Emit(new Ir.Load(unit, pointer, offset, width, field.Type.IsVolatile));
        Emit(new Ir.Bin(Ir.BinOp.And, unit, unit, new Ir.Imm(~(mask << field.BitShift) & Mask(width), width)));
        Ir.Cell part = Temp(depth + 2, width);
        Emit(new Ir.Bin(Ir.BinOp.And, part, value, new Ir.Imm(mask, width)));
        if (field.BitShift > 0)
        {
            Emit(new Ir.Bin(Ir.BinOp.Shl, part, part, new Ir.Imm(field.BitShift, 1)));
        }

        Emit(new Ir.Bin(Ir.BinOp.Or, unit, unit, part));
        Emit(new Ir.Store(pointer, offset, unit, width, field.Type.IsVolatile));
    }
}
