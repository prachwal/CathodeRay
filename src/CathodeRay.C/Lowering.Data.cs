namespace CathodeRay.C;

/// <summary>Lowering: dane globalne i <c>static</c>, inicjalizatory stałe oraz adresowe.</summary>
internal sealed partial class Lowering
{
    /// <summary>Spłaszcza inicjalizator tablicy/struktury do zapisów skalarów (przesunięcie, typ, wartość).</summary>
    private static void CollectInit(CType type, Ast.Expr init, int offset, List<(int Offset, CType Type, Ast.Expr Value)> entries)
    {
        switch (type.Kind)
        {
            case "array" when init is Ast.Str str:
                string text = str.Value + '\0';
                for (int i = 0; i < text.Length; i++)
                {
                    entries.Add((offset + i, CType.UChar, new Ast.Number(((int)text[i]).ToString(System.Globalization.CultureInfo.InvariantCulture))));
                }

                break;
            case "array":
                var items = ((Ast.InitList)init).Items;
                for (int i = 0; i < items.Count; i++)
                {
                    CollectInit(type.Base!, items[i], offset + (i * type.Base!.Size), entries);
                }

                break;
            case "struct":
                var values = ((Ast.InitList)init).Items;
                for (int i = 0; i < values.Count; i++)
                {
                    StructField field = type.Info!.Fields[i];
                    CollectInit(field.Type, values[i], offset + field.Offset, entries);
                }

                break;
            default:
                entries.Add((offset, type, init));
                break;
        }
    }

    private static int StorageSize(CType type) => type.Kind is "array" or "struct" ? type.Size : Width(type);

    /// <summary>Miejsce na zmienną globalną lub <c>static</c>: dane z wartością, adres jako słowo z symbolem albo
    /// (tylko globale) kod startowy dla inicjalizatora niestałego.</summary>
    private void AddStorage(string label, TypedSymbol symbol, bool global)
    {
        bool exported = label.StartsWith("cc_g_", StringComparison.Ordinal) && !_localSymbols.Contains(label);
        int size = StorageSize(symbol.Type);
        bool aggregate = symbol.Type.Kind is "array" or "struct";
        bool tableInit = aggregate && symbol.Init is Ast.InitList or Ast.Str;
        if (symbol.Init is not null && !tableInit && !TryDataConstant(symbol.Init, symbol.Type, out _))
        {
            if (symbol.Type.Kind is "ptr" or "fptr" && SymbolInit(symbol.Init) is var (address, offset))
            {
                _dataOut.Add(new Ir.Data(label, "DATA", 2, [new Ir.SymWord(address, offset)], exported));
            }
            else if (global)
            {
                AddBss(label, size, exported);
                _runtimeInits.Add(symbol);
            }
            else
            {
                throw new CCodegenException($"static '{symbol.Name}' needs a constant initializer.");
            }

            return;
        }

        if (symbol.Init is null)
        {
            AddBss(label, size, exported);
            return;
        }

        IReadOnlyList<Ir.Piece> pieces = aggregate ? AggregatePieces(symbol) : [new Ir.Bytes(ScalarBytes(symbol))];
        _dataOut.Add(new Ir.Data(label, "DATA", size, pieces, exported));
    }

    private byte[] ScalarBytes(TypedSymbol symbol)
    {
        if (TryDataConstant(symbol.Init, symbol.Type, out long value))
        {
            return NumberBytes(value, symbol.Type.Size);
        }

        throw new CCodegenException($"initializer of '{symbol.Name}' must be a constant.");
    }

    /// <summary>Stała do danych początkowych: literał 32-bitowy albo stała 16-bitowa (dla celu 32-bitowego rozszerzana znakiem, gdy
    /// ma ustawiony bit 15, bo stała powyżej 255 bez przyrostka jest <c>int</c>).</summary>
    private bool TryDataConstant(Ast.Expr? expr, CType target, out long value)
    {
        value = 0;
        if (expr is Ast.Number number && Literal.TryParse(number.Text, out Literal literal) && literal.IsLong)
        {
            value = literal.Value;
            return true;
        }

        if (expr is null || !TryConstValue(expr, out int constant))
        {
            return false;
        }

        value = target.Size == 4 && constant >= 0x8000 ? constant | 0xFFFF0000L : constant;
        return true;
    }

    /// <summary>Liczba w kolejności bajtów celu.</summary>
    private byte[] NumberBytes(long value, int size)
    {
        byte[] bytes = size == 1 ? [(byte)value] : size == 2 ? [(byte)value, (byte)(value >> 8)] : [(byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)];
        if (_byteOrder == TargetByteOrder.Big)
        {
            Array.Reverse(bytes);
        }

        return bytes;
    }

    /// <summary>Dane tablicy/struktury: bajty z wpisanymi stałymi i słowami z adresami symboli.</summary>
    private List<Ir.Piece> AggregatePieces(TypedSymbol symbol)
    {
        var bytes = new byte[symbol.Type.Size];
        var symbols = new SortedDictionary<int, (string Sym, int Off)>();
        var entries = new List<(int Offset, CType Type, Ast.Expr Value)>();
        CollectInit(symbol.Type, symbol.Init!, 0, entries);
        foreach ((int offset, CType type, Ast.Expr expr) in entries)
        {
            if (!TryDataConstant(expr, type, out long value))
            {
                if (type.Kind is "ptr" or "fptr" && SymbolInit(expr) is var (address, addressOffset))
                {
                    symbols[offset] = (address, addressOffset);
                    continue;
                }

                throw new CCodegenException($"initializer of '{symbol.Name}' must be constant.");
            }

            NumberBytes(value, type.Size).CopyTo(bytes, offset);
        }

        var pieces = new List<Ir.Piece>();
        var run = new List<byte>();
        for (int i = 0; i < bytes.Length; i++)
        {
            if (symbols.TryGetValue(i, out (string Sym, int Off) target))
            {
                if (run.Count > 0)
                {
                    pieces.Add(new Ir.Bytes([.. run]));
                    run.Clear();
                }

                pieces.Add(new Ir.SymWord(target.Sym, target.Off));
                i++;
                continue;
            }

            run.Add(bytes[i]);
        }

        if (run.Count > 0)
        {
            pieces.Add(new Ir.Bytes([.. run]));
        }

        return pieces;
    }

    /// <summary>Adres jako inicjalizator globalnego wskaźnika: napis, <c>&amp;g</c>, <c>&amp;g.f</c>, <c>&amp;a[2]</c>, nazwa
    /// tablicy lub funkcji, pole-tablica, ewentualnie z przesunięciem stałą (<c>tab + 2</c>, skalowaną rozmiarem elementu).</summary>
    private (string Sym, int Off)? SymbolInit(Ast.Expr? init)
    {
        switch (init)
        {
            case Ast.Str str:
                return (StringLabel(str.Value), 0);
            case Ast.Var function when !_globalsByName.ContainsKey(function.Name) && _functions.ContainsKey(function.Name):
                return (function.Name, 0);
            case Ast.AddressOf function when !_globalsByName.ContainsKey(function.Name) && _functions.ContainsKey(function.Name):
                return (function.Name, 0);
            case Ast.AddressOf address:
                return (GlobalLabel(address.Name), 0);
            case Ast.AddressOfExpr addressOf when GlobalLvalue(addressOf.Target) is var (symbol, offset):
                return (symbol, offset);
            case Ast.Var variable when _globalsByName.TryGetValue(variable.Name, out CType? type) && type.Kind == "array":
                return (GlobalLabel(variable.Name), 0);
            case Ast.Member member when _types.ContainsKey(member.Base) && FieldOf(member).Type.Kind == "array" && GlobalLvalue(member) is var (fieldSymbol, fieldOffset):
                return (fieldSymbol, fieldOffset);
            case Ast.Binary { Op: "+" or "-" } binary when SymbolInit(binary.Left) is var (baseSymbol, baseOffset) && TryConstValue(binary.Right, out int k):
                int delta = (short)k * PointeeSize(binary.Left) * (binary.Op == "-" ? -1 : 1);
                return (baseSymbol, baseOffset + delta);
            default:
                return null;
        }
    }

    /// <summary>Symbol i przesunięcie lwartości opartej o globalną zmienną i stałe indeksy/pola.</summary>
    private (string Symbol, int Offset)? GlobalLvalue(Ast.Expr expr)
    {
        switch (expr)
        {
            case Ast.Var variable when _globalsByName.ContainsKey(variable.Name):
                return (GlobalLabel(variable.Name), 0);
            case Ast.Index index when GlobalLvalue(index.Base) is var (symbol, offset) && TryConstValue(index.Offset, out int position) && _types.TryGetValue(index, out CType? elem):
                return (symbol, offset + ((short)position * elem.Size));
            case Ast.Member { Arrow: false } member when GlobalLvalue(member.Base) is var (baseSymbol, baseOffset) && _types.ContainsKey(member.Base):
                return (baseSymbol, baseOffset + FieldOf(member).Offset);
            default:
                return null;
        }
    }

    private int PointeeSize(Ast.Expr pointer) =>
        _types.TryGetValue(pointer, out CType? type) && type.Base is not null ? type.Base.Size : 1;

    private StructField FieldOf(Ast.Member member)
    {
        CType baseType = _types[member.Base];
        StructInfo info = member.Arrow ? baseType.Base!.Info! : baseType.Info!;
        return info.Find(member.Name)!;
    }
}
