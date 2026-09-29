namespace CathodeRay.C;

/// <summary>Kontrola typów mini-C: zakresy blokowe, sygnatury funkcji, promocje
/// (<c>uchar→int</c>), zawężenie <c>int→uchar</c> z ostrzeżeniem, arytmetyka
/// wskaźników (skala przez rozmiar elementu w codegen).</summary>
public sealed class TypeChecker
{
    private readonly Dictionary<string, Ast.Function> _functions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TypedSymbol> _globals = new(StringComparer.Ordinal);
    private readonly List<string> _warnings = [];
    private readonly Stack<Dictionary<string, CType>> _scopes = new();
    private readonly List<TypedSymbol> _locals = [];
    private readonly Dictionary<Ast.Expr, CType> _types = new(ReferenceEqualityComparer.Instance);
    private string _returnType = "void";

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

    private static void CheckArray(Ast.Decl decl)
    {
        if (decl.ArrayLength > 0 && decl.Init is not null)
        {
            throw new CTypeException($"array '{decl.Name}' needs no initializer (zeroed).");
        }
    }

    private static CType Declared(string type, int stars, int length = 0)
    {
        CType result = CType.FromName(type);
        for (int i = 0; i < stars; i++)
        {
            result = CType.Pointer(result);
        }

        return length > 0 ? CType.Array(result, length) : result;
    }

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

    private CheckedProgram CheckProgram(Ast.Program program)
    {
        foreach (Ast.Decl global in program.Globals)
        {
            CheckArray(global);
            if (!_globals.TryAdd(global.Name, new TypedSymbol(global.Name, Declared(global.Type, global.PointerDepth, global.ArrayLength), global.Init)))
            {
                throw new CTypeException($"redefinition of '{global.Name}'.");
            }

            if (global.Init is not null)
            {
                AssignableOrNull(Declared(global.Type, global.PointerDepth, global.ArrayLength), global.Init, $"initializer of '{global.Name}'");
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

        var checkedFunctions = new List<CheckedFunction>();
        foreach (Ast.Function function in program.Functions)
        {
            checkedFunctions.Add(function.IsExtern ? ProtoFunction(function) : CheckFunction(function));
        }

        return new CheckedProgram(
            checkedFunctions,
            [.. _globals.Values],
            _warnings,
            new Dictionary<Ast.Node, int>(program.Lines ?? new Dictionary<Ast.Node, int>(), ReferenceEqualityComparer.Instance));
    }

    private CheckedFunction ProtoFunction(Ast.Function function)
    {
        var parameters = new List<TypedSymbol>();
        foreach (Ast.Param param in function.Params)
        {
            parameters.Add(new TypedSymbol(param.Name, Declared(param.Type, param.PointerDepth)));
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
        _returnType = function.ReturnType;
        var parameters = new List<TypedSymbol>();
        foreach (Ast.Param param in function.Params)
        {
            if (!_scopes.Peek().TryAdd(param.Name, Declared(param.Type, param.PointerDepth)))
            {
                throw new CTypeException($"redefinition of '{param.Name}'.");
            }

            parameters.Add(new TypedSymbol(param.Name, Declared(param.Type, param.PointerDepth)));
        }

        CheckBlock(function.Body);
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

                CheckArray(decl);
                if (!_scopes.Peek().TryAdd(decl.Name, Declared(decl.Type, decl.PointerDepth, decl.ArrayLength)))
                {
                    throw new CTypeException($"redefinition of '{decl.Name}'.");
                }

                _locals.Add(new TypedSymbol(decl.Name, Declared(decl.Type, decl.PointerDepth, decl.ArrayLength)));
                if (decl.Init is not null)
                {
                    AssignableOrNull(Declared(decl.Type, decl.PointerDepth, decl.ArrayLength), decl.Init, $"initializer of '{decl.Name}'");
                }

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
                CheckStmt(whileStmt.Body);
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

                CheckStmt(forStmt.Body);
                _scopes.Pop();
                break;
            case Ast.Return ret:
                if (_returnType == "void")
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
                    Assignable(CType.FromName(_returnType), TypeOf(ret.Value), "return value");
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

    private void Condition(Ast.Expr cond)
    {
        CType type = TypeOf(cond);
        if (type.Kind is "void")
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

    private void AssignableOrNull(CType target, Ast.Expr value, string where)
    {
        if (value is Ast.Number { Text: "0" } && target.Kind == "ptr")
        {
            return;
        }

        Assignable(target, TypeOf(value), where);
    }

    private void Assignable(CType target, CType value, string where)
    {
        if (target == value || (target.Kind == "int" && value.Kind == "uchar"))
        {
            return;
        }

        if (target.Kind == "uchar" && value.Kind == "int")
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
        return type;
    }

    private CType TypeOfInner(Ast.Expr expr)
    {
        switch (expr)
        {
            case Ast.Number number:
                return NumberType(number.Text);
            case Ast.Var variable:
                return Lookup(variable.Name).Decay();
            case Ast.Call call:
                return CallType(call);
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
            case Ast.Deref deref:
                return DerefType(deref);
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

    private CType CallType(Ast.Call call)
    {
        if (!_functions.TryGetValue(call.Name, out Ast.Function? function))
        {
            throw new CTypeException($"undefined function '{call.Name}'.");
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

        return CType.FromName(function.ReturnType);
    }

    private CType UnaryType(Ast.Unary unary)
    {
        CType operand = TypeOf(unary.Operand);
        return unary.Op switch
        {
            "-" or "~" => operand.Kind is "void" or "ptr" or "array"
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
        if (left.Kind == "void" || right.Kind == "void")
        {
            throw new CTypeException($"operator '{binary.Op}' needs values.");
        }

        if (binary.Op is "==" or "!=" or "<" or "<=" or ">" or ">=")
        {
            if (left.Kind == "ptr" || right.Kind == "ptr")
            {
                throw new CTypeException($"operator '{binary.Op}' needs arithmetic operands.");
            }

            return CType.UChar;
        }

        if (binary.Op is "&&" or "||")
        {
            if (left.Kind == "void" || right.Kind == "void")
            {
                throw new CTypeException($"operator '{binary.Op}' needs values.");
            }

            return CType.UChar;
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

        return left.Kind == "int" || right.Kind == "int" ? CType.Int : CType.UChar;
    }

    private CType AssignToType(Ast.AssignTo assignTo)
    {
        if (assignTo.Target is not (Ast.Deref or Ast.Index))
        {
            throw new CTypeException("assignment target must be *p or p[i].");
        }

        CType target = TypeOf(assignTo.Target);
        Assignable(target, TypeOf(assignTo.Value), "assignment through pointer");
        return target;
    }

    private CType AssignType(Ast.Assign assign)
    {
        CType target = Lookup(assign.Name);
        if (target.Kind == "array")
        {
            throw new CTypeException($"array '{assign.Name}' is not assignable.");
        }

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

        return then.Kind == "int" || els.Kind == "int" ? CType.Int : CType.UChar;
    }

    private CType DerefType(Ast.Deref deref)
    {
        CType pointer = TypeOf(deref.Pointer);
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
