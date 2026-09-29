namespace CathodeRay.C;

/// <summary>Kontrola typów mini-C: zakresy blokowe, sygnatury funkcji, promocje
/// (<c>uchar→int</c>), zawężenie <c>int→uchar</c> z ostrzeżeniem, arytmetyka
/// wskaźników (skala przez rozmiar elementu w codegen).</summary>
public sealed class TypeChecker
{
    /// <summary>Maks. liczba argumentów (A, X, potem komórki <c>cc_arg2</c>..<c>cc_arg6</c>).</summary>
    public const int MaxArgs = 6;

    private readonly Dictionary<string, Ast.Function> _functions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TypedSymbol> _globals = new(StringComparer.Ordinal);
    private readonly List<string> _warnings = [];
    private readonly Dictionary<Ast.Expr, int> _constants = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, StructInfo> _structs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _labels = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Ast.Goto> _gotos = [];
    private readonly Stack<Dictionary<string, CType>> _scopes = new();
    private readonly List<TypedSymbol> _locals = [];
    private readonly Dictionary<Ast.Expr, CType> _types = new(ReferenceEqualityComparer.Instance);
    private CType _returnType = CType.Void;
    private int _loops;
    private int _switches;
    private IReadOnlyDictionary<Ast.Node, int> _lineMap = new Dictionary<Ast.Node, int>();

    private TypeChecker()
    {
    }

    /// <summary>Sprawdza program.</summary>
    /// <param name="program">Drzewo z parsera.</param>
    /// <returns>Program z typami symboli.</returns>
    /// <exception cref="CTypeException">Błąd typów.</exception>
    public static CheckedProgram Check(Ast.Program program)
    {
        ArgumentNullException.ThrowIfNull(program);
        var checker = new TypeChecker();
        return checker.CheckProgram(program);
    }

    private static void RejectStructByValue(CType type, string what)
    {
        if (type.Kind == "struct")
        {
            throw new CTypeException($"{what}: a struct cannot be passed or returned by value (use a pointer).");
        }
    }

    /// <summary>Ten sam kształt typu, pomijając <c>const</c> (tablice, wskaźniki i struktury rekurencyjnie).</summary>
    private static bool SameShape(CType a, CType b) =>
        a.Kind == b.Kind && a.Length == b.Length && a.Info == b.Info
        && (a.Base is null ? b.Base is null : b.Base is not null && SameShape(a.Base, b.Base))
        && (a.Sig is null ? b.Sig is null : b.Sig is not null && SameShape(a.Sig.Return, b.Sig.Return)
            && a.Sig.Params.Count == b.Sig.Params.Count && a.Sig.Params.Zip(b.Sig.Params).All(static pair => SameShape(pair.First, pair.Second)));

    private static void RequireWritable(CType target, string what)
    {
        if (target.IsConst)
        {
            throw new CTypeException($"assignment to const {what}.");
        }
    }

    private static int NumberValue(string text) =>
        text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? Convert.ToInt32(text[2..], 16) : int.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

    private static CType NumberType(string text)
    {
        string digits = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
        int radix = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? 16 : 10;
        long value = 0;
        foreach (char c in digits)
        {
            bool ok = radix == 16 ? char.IsAsciiHexDigit(c) : char.IsAsciiDigit(c);
            if (!ok)
            {
                throw new CTypeException($"invalid number '{text}'.");
            }

            value = (value * radix) + Convert.ToInt32(c.ToString(), radix);
            if (value > ushort.MaxValue)
            {
                throw new CTypeException($"number '{text}' out of range 0..65535.");
            }
        }

        return value <= byte.MaxValue ? CType.UChar : CType.Int;
    }

    private void CheckInit(Ast.Decl decl)
    {
        CType type = Declared(decl.Type, decl.PointerDepth, LengthOf(decl));
        if (decl.Init is null)
        {
            return;
        }

        if (type.Kind is "array" or "struct")
        {
            CheckAggregate(type, decl.Init, decl.Name);
            return;
        }

        if (decl.Init is Ast.InitList)
        {
            throw new CTypeException($"'{decl.Name}' is not an array or struct.");
        }

        AssignableOrNull(type, decl.Init, $"initializer of '{decl.Name}'");
    }

    /// <summary>Inicjalizator tablicy/struktury: <c>{…}</c> (zagnieżdżone dla pól-tablic i pól-struktur),
    /// napis dla tablicy uchar albo (struktura) wyrażenie tego samego typu.</summary>
    private void CheckAggregate(CType type, Ast.Expr init, string name)
    {
        string where = $"initializer of '{name}'";
        if (type.Kind == "struct")
        {
            if (init is not Ast.InitList structList)
            {
                Assignable(type, TypeOf(init), where);
                return;
            }

            IReadOnlyList<StructField> fields = type.Info!.Fields;
            if (structList.Items.Count > fields.Count)
            {
                throw new CTypeException($"too many initializers for struct '{type.Info.Name}'.");
            }

            for (int i = 0; i < structList.Items.Count; i++)
            {
                CheckAggregateItem(fields[i].Type, structList.Items[i], name);
            }

            return;
        }

        CType elem = type.Base!;
        switch (init)
        {
            case Ast.Str str when elem.Kind == "uchar":
                if (str.Value.Length + 1 > type.Length)
                {
                    throw new CTypeException($"string too long for '{name}[{type.Length}]'.");
                }

                break;
            case Ast.InitList list:
                if (list.Items.Count > type.Length)
                {
                    throw new CTypeException($"too many initializers for '{name}[{type.Length}]'.");
                }

                foreach (Ast.Expr item in list.Items)
                {
                    CheckAggregateItem(elem, item, name);
                }

                break;
            default:
                throw new CTypeException($"array '{name}' needs an initializer list or a string.");
        }
    }

    private void CheckAggregateItem(CType type, Ast.Expr item, string name)
    {
        if (type.Kind is "array" or "struct")
        {
            CheckAggregate(type, item, name);
            return;
        }

        if (item is Ast.InitList)
        {
            throw new CTypeException($"unexpected braces in initializer of '{name}'.");
        }

        AssignableOrNull(type, item, $"initializer of '{name}'");
    }

    private CType Declared(string type, int stars, int length = 0)
    {
        bool isConst = type.StartsWith("const ", StringComparison.Ordinal);
        string bare = isConst ? type[6..] : type;
        CType result = bare.StartsWith("fptr<", StringComparison.Ordinal)
            ? DeclaredFuncPtr(bare)
            : bare.StartsWith("struct ", StringComparison.Ordinal)
            ? CType.Struct(_structs.TryGetValue(bare[7..], out StructInfo? info) ? info : throw new CTypeException($"unknown {bare}."))
            : CType.FromName(bare);
        if (isConst)
        {
            result = result with { IsConst = true };
        }

        for (int i = 0; i < stars; i++)
        {
            result = CType.Pointer(result);
        }

        return length > 0 ? CType.Array(result, length) : result;
    }

    /// <summary>Rozkłada zakodowany typ <c>fptr&lt;wynik;par1;par2&gt;</c> na sygnaturę.</summary>
    private CType DeclaredFuncPtr(string encoded)
    {
        string inner = encoded["fptr<".Length..^1];
        var parts = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < inner.Length; i++)
        {
            if (inner[i] == '<')
            {
                depth++;
            }
            else if (inner[i] == '>')
            {
                depth--;
            }
            else if (inner[i] == ';' && depth == 0)
            {
                parts.Add(inner[start..i]);
                start = i + 1;
            }
        }

        parts.Add(inner[start..]);
        CType Item(string text)
        {
            int stars = text.Length - text.TrimEnd('*').Length;
            return Declared(text.TrimEnd('*'), stars);
        }

        return CType.FuncPtr(new FuncSig(Item(parts[0]), [.. parts.Skip(1).Where(static p => p.Length > 0).Select(Item)]));
    }

    private void BuildStructs(Ast.Program program)
    {
        var defs = new Dictionary<string, Ast.StructDef>(StringComparer.Ordinal);
        foreach (Ast.StructDef def in program.Structs ?? [])
        {
            if (!defs.TryAdd(def.Name, def))
            {
                throw new CTypeException($"redefinition of struct '{def.Name}'.");
            }

            _structs[def.Name] = new StructInfo(def.Name);
        }

        foreach (string name in defs.Keys)
        {
            LayoutStruct(defs, name, []);
        }
    }

    private void LayoutStruct(Dictionary<string, Ast.StructDef> defs, string name, HashSet<string> visiting)
    {
        StructInfo info = _structs[name];
        if (info.Complete)
        {
            return;
        }

        if (!visiting.Add(name))
        {
            throw new CTypeException($"struct '{name}' contains itself.");
        }

        int offset = 0;
        foreach (Ast.FieldDecl field in defs[name].Fields)
        {
            string bareField = field.Type.StartsWith("const ", StringComparison.Ordinal) ? field.Type[6..] : field.Type;
            if (bareField.StartsWith("struct ", StringComparison.Ordinal) && field.Stars == 0 && defs.ContainsKey(bareField[7..]))
            {
                LayoutStruct(defs, bareField[7..], visiting);
            }

            CType type = Declared(field.Type, field.Stars, field.ArrayLength);
            if (type.Kind == "void")
            {
                throw new CTypeException($"field '{field.Name}' has void type.");
            }

            if (info.Find(field.Name) is not null)
            {
                throw new CTypeException($"duplicate field '{field.Name}' in struct '{name}'.");
            }

            info.Fields.Add(new StructField(field.Name, type, offset));
            offset += type.Size;
        }

        info.Size = offset;
        info.Complete = true;
        visiting.Remove(name);
    }

    private CheckedProgram CheckProgram(Ast.Program program)
    {
        BuildStructs(program);
        foreach (Ast.Decl global in program.Globals)
        {
            CType globalType = Declared(global.Type, global.PointerDepth, LengthOf(global));
            var symbol = new TypedSymbol(global.Name, globalType, global.Init, global.Flags);
            bool isExtern = global.Flags.HasFlag(DeclFlags.Extern);
            if (isExtern && global.Init is not null)
            {
                throw new CTypeException($"extern '{global.Name}' cannot be initialized.");
            }

            if (_globals.TryGetValue(global.Name, out TypedSymbol? previous))
            {
                bool previousExtern = previous.Flags.HasFlag(DeclFlags.Extern);
                if ((!previousExtern && !isExtern) || !SameShape(previous.Type, globalType))
                {
                    throw new CTypeException($"redefinition of '{global.Name}'.");
                }

                if (previousExtern)
                {
                    _globals[global.Name] = symbol;
                }
            }
            else
            {
                _globals[global.Name] = symbol;
            }
        }

        foreach (Ast.Function function in program.Functions)
        {
            if (_globals.ContainsKey(function.Name))
            {
                throw new CTypeException($"redefinition of '{function.Name}'.");
            }

            if (_functions.TryGetValue(function.Name, out Ast.Function? existing))
            {
                if (!function.IsExtern && !existing.IsExtern)
                {
                    throw new CTypeException($"redefinition of '{function.Name}'.");
                }

                if (!function.IsExtern)
                {
                    _functions[function.Name] = function;
                }

                continue;
            }

            _functions[function.Name] = function;
        }

        _lineMap = program.Lines ?? _lineMap;
        foreach (Ast.Decl global in program.Globals)
        {
            CheckInit(global);
        }

        var globalTypes = new Dictionary<Ast.Expr, CType>(_types, ReferenceEqualityComparer.Instance);
        var checkedFunctions = new List<CheckedFunction>();
        foreach (Ast.Function function in program.Functions)
        {
            checkedFunctions.Add(function.IsExtern ? ProtoFunction(function) : CheckFunction(function));
        }

        return new CheckedProgram(
            checkedFunctions,
            [.. _globals.Values],
            _warnings,
            new Dictionary<Ast.Node, int>(program.Lines ?? new Dictionary<Ast.Node, int>(), ReferenceEqualityComparer.Instance),
            globalTypes,
            _constants);
    }

    private CheckedFunction ProtoFunction(Ast.Function function)
    {
        RejectStructByValue(Declared(function.ReturnType, function.ReturnStars), $"return type of '{function.Name}'");
        var parameters = new List<TypedSymbol>();
        foreach (Ast.Param param in function.Params)
        {
            CType paramType = Declared(param.Type, param.PointerDepth);
            RejectStructByValue(paramType, $"parameter '{param.Name}'");
            parameters.Add(new TypedSymbol(param.Name, paramType));
        }

        return new CheckedFunction(
            function,
            parameters,
            [],
            new Dictionary<Ast.Expr, CType>(ReferenceEqualityComparer.Instance));
    }

    private CheckedFunction CheckFunction(Ast.Function function)
    {
        _locals.Clear();
        _types.Clear();
        _scopes.Clear();
        _scopes.Push(new Dictionary<string, CType>(StringComparer.Ordinal));
        _returnType = Declared(function.ReturnType, function.ReturnStars);
        RejectStructByValue(_returnType, $"return type of '{function.Name}'");
        _loops = 0;
        _switches = 0;
        _labels.Clear();
        _gotos.Clear();
        if (function.Params.Count > MaxArgs)
        {
            throw new CTypeException($"'{function.Name}' takes at most {MaxArgs} parameters.");
        }

        var parameters = new List<TypedSymbol>();
        foreach (Ast.Param param in function.Params)
        {
            RejectStructByValue(Declared(param.Type, param.PointerDepth), $"parameter '{param.Name}'");
            if (!_scopes.Peek().TryAdd(param.Name, Declared(param.Type, param.PointerDepth)))
            {
                throw new CTypeException($"redefinition of '{param.Name}'.");
            }

            parameters.Add(new TypedSymbol(param.Name, Declared(param.Type, param.PointerDepth)));
        }

        CheckBlock(function.Body);
        foreach (Ast.Goto jump in _gotos.Where(j => !_labels.Contains(j.Name)))
        {
            throw new CTypeException($"undefined label '{jump.Name}'.") { Line = _lineMap.GetValueOrDefault(jump) };
        }

        return new CheckedFunction(function, parameters, [.. _locals], new Dictionary<Ast.Expr, CType>(_types, ReferenceEqualityComparer.Instance));
    }

    private void CheckBlock(Ast.Block block)
    {
        _scopes.Push(new Dictionary<string, CType>(StringComparer.Ordinal));
        foreach (Ast.Stmt item in block.Items)
        {
            CheckStmt(item);
        }

        _scopes.Pop();
    }

    private void CheckStmt(Ast.Stmt stmt)
    {
        try
        {
            CheckStmtCore(stmt);
        }
        catch (CTypeException e) when (e.Line == 0 && _lineMap.TryGetValue(stmt, out int line))
        {
            e.Line = line;
            throw;
        }
    }

    private void CheckStmtCore(Ast.Stmt stmt)
    {
        switch (stmt)
        {
            case Ast.Block block:
                CheckBlock(block);
                break;
            case Ast.Decl decl:
                if (decl.Type == "void")
                {
                    throw new CTypeException($"variable '{decl.Name}' has void type.");
                }

                if (!_scopes.Peek().TryAdd(decl.Name, Declared(decl.Type, decl.PointerDepth, LengthOf(decl))))
                {
                    throw new CTypeException($"redefinition of '{decl.Name}'.");
                }

                if (decl.Flags.HasFlag(DeclFlags.Extern))
                {
                    throw new CTypeException("extern is only allowed at file scope.");
                }

                _locals.Add(new TypedSymbol(decl.Name, Declared(decl.Type, decl.PointerDepth, LengthOf(decl)), decl.Flags.HasFlag(DeclFlags.Static) ? decl.Init : null, decl.Flags));
                CheckInit(decl);
                break;
            case Ast.If ifStmt:
                Condition(ifStmt.Cond);
                CheckStmt(ifStmt.Then);
                if (ifStmt.Else is not null)
                {
                    CheckStmt(ifStmt.Else);
                }

                break;
            case Ast.While whileStmt:
                Condition(whileStmt.Cond);
                _loops++;
                CheckStmt(whileStmt.Body);
                _loops--;
                break;
            case Ast.Label label:
                if (!_labels.Add(label.Name))
                {
                    throw new CTypeException($"duplicate label '{label.Name}'.");
                }

                break;
            case Ast.Goto jump:
                _gotos.Add(jump);
                break;
            case Ast.DoWhile doStmt:
                _loops++;
                CheckStmt(doStmt.Body);
                _loops--;
                Condition(doStmt.Cond);
                break;
            case Ast.Switch switchStmt:
                CheckSwitch(switchStmt);
                break;
            case Ast.Break when _loops + _switches == 0:
                throw new CTypeException("'break' outside a loop or switch.");
            case Ast.Continue when _loops == 0:
                throw new CTypeException("'continue' outside a loop.");
            case Ast.Break:
            case Ast.Continue:
                break;
            case Ast.For forStmt:
                _scopes.Push(new Dictionary<string, CType>(StringComparer.Ordinal));
                if (forStmt.Init is not null)
                {
                    CheckStmt(forStmt.Init);
                }

                if (forStmt.Cond is not null)
                {
                    Condition(forStmt.Cond);
                }

                if (forStmt.Step is not null)
                {
                    TypeOf(forStmt.Step);
                }

                _loops++;
                CheckStmt(forStmt.Body);
                _loops--;
                _scopes.Pop();
                break;
            case Ast.Return ret:
                if (_returnType.Kind == "void")
                {
                    if (ret.Value is not null)
                    {
                        throw new CTypeException("void function returns a value.");
                    }
                }
                else if (ret.Value is null)
                {
                    throw new CTypeException($"function returns no value (expected {_returnType}).");
                }
                else
                {
                    AssignableOrNull(_returnType, ret.Value, "return value");
                }

                break;
            case Ast.ExprStmt exprStmt:
                TypeOf(exprStmt.Value);
                break;
            case Ast.Nop:
                break;
            default:
                throw new CTypeException($"unsupported statement {stmt.GetType().Name}.");
        }
    }

    private void CheckSwitch(Ast.Switch stmt)
    {
        CType type = TypeOf(stmt.Value);
        if (type.Kind is not ("uchar" or "int" or "uint"))
        {
            throw new CTypeException("switch needs an integer value.");
        }

        var seen = new HashSet<int>();
        bool hasDefault = false;
        _switches++;
        _scopes.Push(new Dictionary<string, CType>(StringComparer.Ordinal));
        foreach (Ast.SwitchCase item in stmt.Cases)
        {
            if (item.Value is null)
            {
                hasDefault = hasDefault ? throw new CTypeException("duplicate 'default'.") : true;
            }
            else if (!seen.Add((short)ConstantOf(item.Value, "'case'")))
            {
                throw new CTypeException("duplicate 'case'.");
            }

            foreach (Ast.Stmt body in item.Body)
            {
                CheckStmt(body);
            }
        }

        _scopes.Pop();
        _switches--;
    }

    private void Condition(Ast.Expr cond)
    {
        CType type = TypeOf(cond);
        if (type.Kind is "void" or "struct")
        {
            throw new CTypeException("condition has void type.");
        }
    }

    private CType Lookup(string name)
    {
        foreach (Dictionary<string, CType> scope in _scopes)
        {
            if (scope.TryGetValue(name, out CType? type))
            {
                return type;
            }
        }

        if (_globals.TryGetValue(name, out TypedSymbol? global))
        {
            return global.Type;
        }

        throw new CTypeException($"undeclared '{name}'.");
    }

    /// <summary>Zmienna albo funkcja (jako wskaźnik do funkcji); <see langword="false"/>, gdy nazwy nie ma.</summary>
    private bool TryLookup(string name, out CType type)
    {
        foreach (Dictionary<string, CType> scope in _scopes)
        {
            if (scope.TryGetValue(name, out CType? found))
            {
                type = found;
                return true;
            }
        }

        if (_globals.TryGetValue(name, out TypedSymbol? global))
        {
            type = global.Type;
            return true;
        }

        type = CType.Void;
        return false;
    }

    private CType FunctionDesignator(Ast.Function function) =>
        CType.FuncPtr(new FuncSig(
            Declared(function.ReturnType, function.ReturnStars),
            [.. function.Params.Select(p => Declared(p.Type, p.PointerDepth))]));

    private void AssignableOrNull(CType target, Ast.Expr value, string where)
    {
        if (value is Ast.Number { Text: "0" } && target.Kind is "ptr" or "fptr")
        {
            return;
        }

        Assignable(target, TypeOf(value), where);
    }

    private void Assignable(CType target, CType value, string where)
    {
        if (SameShape(target, value) || (target.Kind is "int" or "uint" && value.Kind is "uchar" or "int" or "uint"))
        {
            if (target.Kind == "ptr" && value.Base is { IsConst: true } && target.Base is { IsConst: false })
            {
                throw new CTypeException($"{where}: discards const ({value} to {target}).");
            }

            return;
        }

        if (target.Kind == "uchar" && value.Kind is "int" or "uint")
        {
            _warnings.Add($"{where}: narrowing int to uchar.");
            return;
        }

        throw new CTypeException($"{where}: cannot convert {value} to {target}.");
    }

    private CType TypeOf(Ast.Expr expr)
    {
        if (_types.TryGetValue(expr, out CType? cached))
        {
            return cached;
        }

        CType type = TypeOfInner(expr);
        _types[expr] = type;
        if (expr is Ast.Unary or Ast.Binary or Ast.Ternary or Ast.SizeOf or Ast.SizeOfType or Ast.SizeOfExpr && TryConst(expr, out int folded))
        {
            // stała: typ jak litery (<= 255 to uchar, wyżej int), kod zna wartość z _constants
            _constants[expr] = folded;
            type = folded <= byte.MaxValue ? CType.UChar : CType.Int;
            _types[expr] = type;
        }

        return type;
    }

    /// <summary>Wartość stałego wyrażenia (16-bit z zawijaniem; <c>/ % &gt;&gt;</c> ze znakiem) albo <see langword="false"/>.</summary>
    private bool TryConst(Ast.Expr expr, out int value)
    {
        value = 0;
        switch (expr)
        {
            case Ast.Number number:
                value = NumberValue(number.Text) & 0xFFFF;
                return true;
            case Ast.SizeOf sizeOf:
                value = Lookup(sizeOf.Name).Size;
                return true;
            case Ast.SizeOfType sizeOfType:
                value = Declared(sizeOfType.Type, sizeOfType.Stars).Size;
                return true;
            case Ast.SizeOfExpr sizeOfExpr:
                value = RawType(sizeOfExpr.Operand).Size;
                return true;
            case Ast.Unary unary when TryConst(unary.Operand, out int operand):
                value = unary.Op switch { "-" => -operand, "~" => ~operand, _ => operand == 0 ? 1 : 0 } & 0xFFFF;
                return true;
            case Ast.Ternary ternary when TryConst(ternary.Cond, out int cond) && TryConst(ternary.Then, out int then) && TryConst(ternary.Else, out int otherwise):
                value = cond != 0 ? then : otherwise;
                return true;
            case Ast.Binary binary when TryConst(binary.Left, out int a) && TryConst(binary.Right, out int b):
                short sa = (short)a;
                short sb = (short)b;
                int? result = binary.Op switch
                {
                    "+" => a + b,
                    "-" => a - b,
                    "*" => a * b,
                    "/" when sb != 0 => sa / sb,
                    "%" when sb != 0 => sa % sb,
                    "<<" when b < 16 => a << b,
                    ">>" when b < 16 => sa >> b,
                    "&" => a & b,
                    "|" => a | b,
                    "^" => a ^ b,
                    "==" => a == b ? 1 : 0,
                    "!=" => a != b ? 1 : 0,
                    "<" => sa < sb ? 1 : 0,
                    "<=" => sa <= sb ? 1 : 0,
                    ">" => sa > sb ? 1 : 0,
                    ">=" => sa >= sb ? 1 : 0,
                    "&&" => a != 0 && b != 0 ? 1 : 0,
                    "||" => a != 0 || b != 0 ? 1 : 0,
                    _ => null,
                };
                value = (result ?? 0) & 0xFFFF;
                return result is not null;
            default:
                return false;
        }
    }

    /// <summary>Typ operandu <c>sizeof</c> bez rozpadu tablicy.</summary>
    private CType RawType(Ast.Expr operand) => operand switch
    {
        Ast.Member member => MemberType(member),
        Ast.Var variable => Lookup(variable.Name),
        _ => TypeOf(operand),
    };

    /// <summary>Stała całkowita (liczba lub wyrażenie stałe) albo błąd.</summary>
    private int ConstantOf(Ast.Expr expr, string what)
    {
        _ = TypeOf(expr);
        return expr is Ast.Number number
            ? NumberValue(number.Text) & 0xFFFF
            : _constants.TryGetValue(expr, out int value) ? value : throw new CTypeException($"{what} needs a constant.");
    }

    private int LengthOf(Ast.Decl decl) =>
        decl.LengthExpr is null
            ? decl.ArrayLength
            : ConstantOf(decl.LengthExpr, "array length") is var length and > 0 ? length : throw new CTypeException($"array '{decl.Name}' needs a positive length.");

    private CType TypeOfInner(Ast.Expr expr)
    {
        switch (expr)
        {
            case Ast.Number number:
                return NumberType(number.Text);
            case Ast.Str:
                return CType.Pointer(CType.UChar);
            case Ast.Member member:
                return MemberType(member).Decay();
            case Ast.AddressOfExpr addressOf:
            {
                CType target = addressOf.Target is Ast.Member m ? MemberType(m) : TypeOf(addressOf.Target);
                _ = TypeOf(addressOf.Target);
                return CType.Pointer(target.Kind == "array" && target.Base is not null ? target.Base : target);
            }

            case Ast.SizeOfType sizeOfType:
            {
                int size = Declared(sizeOfType.Type, sizeOfType.Stars).Size;
                return size <= byte.MaxValue ? CType.UChar : CType.Int;
            }

            case Ast.SizeOfExpr sizeOfExpr:
            {
                int size = RawType(sizeOfExpr.Operand).Size;
                return size <= byte.MaxValue ? CType.UChar : CType.Int;
            }

            case Ast.SizeOf sizeOf:
            {
                int size = Lookup(sizeOf.Name).Size;
                return size <= byte.MaxValue ? CType.UChar : CType.Int;
            }

            case Ast.Var variable:
                return !TryLookup(variable.Name, out _) && _functions.TryGetValue(variable.Name, out Ast.Function? designated)
                    ? FunctionDesignator(designated)
                    : Lookup(variable.Name).Decay();
            case Ast.Call call:
                return CallType(call);
            case Ast.CallExpr callExpr:
                return IndirectCallType(callExpr);
            case Ast.Unary unary:
                return UnaryType(unary);
            case Ast.Binary binary:
                return BinaryType(binary);
            case Ast.Assign assign:
                return AssignType(assign);
            case Ast.Ternary ternary:
                return TernaryType(ternary);
            case Ast.AssignTo assignTo:
                return AssignToType(assignTo);
            case Ast.AssignOpTo assignOp:
            {
                CType target = TypeOf(assignOp.Target);
                RequireWritable(target, "object");
                Assignable(target, TypeOf(assignOp.Combined), "compound assignment through pointer");
                return target;
            }

            case Ast.Deref deref:
                return DerefType(deref);
            case Ast.AddressOf addressOf when !TryLookup(addressOf.Name, out _) && _functions.TryGetValue(addressOf.Name, out Ast.Function? addressed):
                return FunctionDesignator(addressed);
            case Ast.AddressOf addressOf:
            {
                CType raw = Lookup(addressOf.Name);
                CType target = raw.Kind == "array" && raw.Base is not null ? raw.Base : raw;
                return CType.Pointer(target);
            }

            case Ast.Index index:
                return IndexType(index);
            default:
                throw new CTypeException($"unsupported expression {expr.GetType().Name}.");
        }
    }

    private CType IndirectCallType(Ast.CallExpr call)
    {
        CType callee = TypeOf(call.Callee);
        return callee.Kind == "fptr"
            ? CheckIndirect(callee.Sig!, call.Args, "function pointer")
            : throw new CTypeException("called object is not a function pointer.");
    }

    private CType CheckIndirect(FuncSig sig, IReadOnlyList<Ast.Expr> args, string name)
    {
        if (args.Count > MaxArgs || args.Count != sig.Params.Count)
        {
            throw new CTypeException($"{name} takes {sig.Params.Count} arguments, got {args.Count}.");
        }

        for (int i = 0; i < args.Count; i++)
        {
            AssignableOrNull(sig.Params[i], args[i], $"argument {i + 1} of {name}");
        }

        return sig.Return;
    }

    private CType CallType(Ast.Call call)
    {
        if (TryLookup(call.Name, out CType variable))
        {
            return variable.Kind == "fptr"
                ? CheckIndirect(variable.Sig!, call.Args, $"'{call.Name}'")
                : throw new CTypeException($"'{call.Name}' is not a function.");
        }

        if (!_functions.TryGetValue(call.Name, out Ast.Function? function))
        {
            throw new CTypeException($"undefined function '{call.Name}'.");
        }

        if (call.Args.Count > MaxArgs)
        {
            throw new CTypeException($"'{call.Name}' takes at most {MaxArgs} arguments.");
        }

        if (call.Args.Count != function.Params.Count)
        {
            throw new CTypeException($"'{call.Name}' takes {function.Params.Count} arguments, got {call.Args.Count}.");
        }

        for (int i = 0; i < call.Args.Count; i++)
        {
            AssignableOrNull(
                Declared(function.Params[i].Type, function.Params[i].PointerDepth),
                call.Args[i],
                $"argument {i + 1} of '{call.Name}'");
        }

        return Declared(function.ReturnType, function.ReturnStars);
    }

    private CType UnaryType(Ast.Unary unary)
    {
        CType operand = TypeOf(unary.Operand);
        if (operand.Kind == "struct")
        {
            throw new CTypeException($"operator '{unary.Op}' needs a value, not a struct.");
        }

        return unary.Op switch
        {
            "-" or "~" => operand.Kind is "void" or "ptr" or "array" or "fptr"
                ? throw new CTypeException($"operator '{unary.Op}' needs arithmetic operands.")
                : operand,
            "!" => operand.Kind == "void"
                ? throw new CTypeException("operator '!' needs a value.")
                : CType.UChar,
            _ => throw new CTypeException($"unknown operator '{unary.Op}'."),
        };
    }

    private CType BinaryType(Ast.Binary binary)
    {
        CType left = TypeOf(binary.Left).Decay();
        CType right = TypeOf(binary.Right).Decay();
        if (left.Kind is "void" or "struct" || right.Kind is "void" or "struct")
        {
            throw new CTypeException($"operator '{binary.Op}' needs values.");
        }

        if (binary.Op is "==" or "!=" or "<" or "<=" or ">" or ">=")
        {
            bool mixed = (left.Kind == "int" && right.Kind == "uint") || (left.Kind == "uint" && right.Kind == "int");
            if (mixed && binary.Left is not Ast.Number && binary.Right is not Ast.Number && !_constants.ContainsKey(binary.Left) && !_constants.ContainsKey(binary.Right))
            {
                _warnings.Add($"comparison '{binary.Op}' of signed and unsigned values.");
            }

            return CType.UChar;
        }

        if (left.Kind == "fptr" || right.Kind == "fptr")
        {
            if (binary.Op is "&&" or "||")
            {
                return CType.UChar;
            }

            throw new CTypeException($"operator '{binary.Op}' is not supported for function pointers.");
        }

        if (binary.Op is "&&" or "||")
        {
            if (left.Kind == "void" || right.Kind == "void")
            {
                throw new CTypeException($"operator '{binary.Op}' needs values.");
            }

            return CType.UChar;
        }

        if (binary.Op == "-" && left.Kind == "ptr" && right.Kind == "ptr")
        {
            return SameShape(left.Base!, right.Base!) ? CType.Int : throw new CTypeException("pointer difference needs pointers to the same type.");
        }

        if (binary.Op is "+" or "-")
        {
            if (left.Kind == "ptr" && right.Kind != "ptr" && right.Kind != "void")
            {
                return left;
            }

            if (right.Kind == "ptr" && binary.Op == "+" && left.Kind != "ptr" && left.Kind != "void")
            {
                return right;
            }
        }

        if (left.Kind == "ptr" || right.Kind == "ptr")
        {
            throw new CTypeException($"operator '{binary.Op}' is not supported for pointers.");
        }

        return left.Kind == "uint" || right.Kind == "uint" ? CType.UInt : left.Kind == "int" || right.Kind == "int" ? CType.Int : CType.UChar;
    }

    private CType AssignToType(Ast.AssignTo assignTo)
    {
        if (assignTo.Target is not (Ast.Deref or Ast.Index or Ast.Member))
        {
            throw new CTypeException("assignment target must be *p, p[i] or a field.");
        }

        CType target = TypeOf(assignTo.Target);
        RequireWritable(target, "object");
        if (target.Kind == "ptr" && assignTo.Target is Ast.Member field && MemberType(field).Kind == "array")
        {
            throw new CTypeException("array field is not assignable.");
        }

        AssignableOrNull(target, assignTo.Value, "assignment through pointer");
        return target;
    }

    private CType AssignType(Ast.Assign assign)
    {
        CType target = Lookup(assign.Name);
        if (target.Kind == "array")
        {
            throw new CTypeException($"array '{assign.Name}' is not assignable.");
        }

        RequireWritable(target, $"variable '{assign.Name}'");

        AssignableOrNull(target, assign.Value, $"assignment to '{assign.Name}'");
        return target;
    }

    private CType TernaryType(Ast.Ternary ternary)
    {
        Condition(ternary.Cond);
        CType then = TypeOf(ternary.Then);
        CType els = TypeOf(ternary.Else);
        if (then.Kind == "void" || els.Kind == "void")
        {
            throw new CTypeException("ternary branches need values.");
        }

        if (then.Kind == "ptr" || els.Kind == "ptr")
        {
            if (then == els && then.Kind == "ptr")
            {
                return then;
            }

            throw new CTypeException("ternary branches need arithmetic values.");
        }

        return then.Kind == "uint" || els.Kind == "uint" ? CType.UInt : then.Kind == "int" || els.Kind == "int" ? CType.Int : CType.UChar;
    }

    /// <summary>Typ pola bez rozpadu tablicy (struct przez <c>.</c> albo wskaźnik przez <c>-&gt;</c>).</summary>
    private CType MemberType(Ast.Member member)
    {
        CType baseType = TypeOf(member.Base);
        StructInfo info;
        if (member.Arrow)
        {
            info = baseType is { Kind: "ptr", Base: { Kind: "struct" } target }
                ? target.Info!
                : throw new CTypeException("'->' needs a pointer to a struct.");
        }
        else
        {
            info = baseType.Kind == "struct" ? baseType.Info! : throw new CTypeException("'.' needs a struct.");
        }

        CType fieldType = info.Find(member.Name)?.Type ?? throw new CTypeException($"struct '{info.Name}' has no field '{member.Name}'.");
        bool constBase = member.Arrow ? baseType.Base!.IsConst : baseType.IsConst;
        return constBase && !fieldType.IsConst ? fieldType with { IsConst = true } : fieldType;
    }

    private CType DerefType(Ast.Deref deref)
    {
        CType pointer = TypeOf(deref.Pointer);
        if (pointer.Kind == "fptr")
        {
            return pointer;
        }

        return pointer.Kind == "ptr" && pointer.Base is not null
            ? pointer.Base
            : throw new CTypeException("dereference needs a pointer.");
    }

    private CType IndexType(Ast.Index index)
    {
        CType @base = TypeOf(index.Base);
        CType offset = TypeOf(index.Offset);
        if (@base.Kind != "ptr" || @base.Base is null)
        {
            throw new CTypeException("indexing needs a pointer.");
        }

        if (offset.Kind == "void" || offset.Kind == "ptr")
        {
            throw new CTypeException("index needs an arithmetic offset.");
        }

        return @base.Base;
    }
}
