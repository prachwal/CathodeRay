namespace CathodeRay.C;

/// <summary>Generator kodu: wykrywanie funkcji rekurencyjnych (lokalne tablice i struktury takich funkcji
/// trzeba zapisywać na stosie razem z ramką).</summary>
public sealed partial class Codegen
{
    private static HashSet<string> RecursiveFunctions(CheckedProgram program)
    {
        var edges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (CheckedFunction function in program.Functions)
        {
            var calls = new HashSet<string>(StringComparer.Ordinal);
            CallsIn(function.Def.Body, calls);
            edges[function.Def.Name] = calls;
        }

        var recursive = new HashSet<string>(StringComparer.Ordinal);
        foreach (string name in edges.Keys)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>(edges[name]);
            while (pending.Count > 0)
            {
                string next = pending.Pop();
                if (next == name)
                {
                    recursive.Add(name);
                    break;
                }

                if (seen.Add(next) && edges.TryGetValue(next, out HashSet<string>? more))
                {
                    foreach (string callee in more)
                    {
                        pending.Push(callee);
                    }
                }
            }
        }

        return recursive;
    }

    private static void CallsIn(Ast.Stmt? stmt, HashSet<string> calls)
    {
        switch (stmt)
        {
            case Ast.Block block:
                foreach (Ast.Stmt item in block.Items)
                {
                    CallsIn(item, calls);
                }

                break;
            case Ast.Decl decl:
                CallsIn(decl.Init, calls);
                break;
            case Ast.If ifStmt:
                CallsIn(ifStmt.Cond, calls);
                CallsIn(ifStmt.Then, calls);
                CallsIn(ifStmt.Else, calls);
                break;
            case Ast.While loop:
                CallsIn(loop.Cond, calls);
                CallsIn(loop.Body, calls);
                break;
            case Ast.DoWhile doLoop:
                CallsIn(doLoop.Body, calls);
                CallsIn(doLoop.Cond, calls);
                break;
            case Ast.For forLoop:
                CallsIn(forLoop.Init, calls);
                CallsIn(forLoop.Cond, calls);
                CallsIn(forLoop.Step, calls);
                CallsIn(forLoop.Body, calls);
                break;
            case Ast.Return ret:
                CallsIn(ret.Value, calls);
                break;
            case Ast.ExprStmt exprStmt:
                CallsIn(exprStmt.Value, calls);
                break;
            case Ast.Switch switchStmt:
                CallsIn(switchStmt.Value, calls);
                foreach (Ast.SwitchCase item in switchStmt.Cases)
                {
                    foreach (Ast.Stmt body in item.Body)
                    {
                        CallsIn(body, calls);
                    }
                }

                break;
        }
    }

    private static void CallsIn(Ast.Expr? expr, HashSet<string> calls)
    {
        switch (expr)
        {
            case Ast.Call call:
                calls.Add(call.Name);
                foreach (Ast.Expr arg in call.Args)
                {
                    CallsIn(arg, calls);
                }

                break;
            case Ast.CallExpr callExpr:
                CallsIn(callExpr.Callee, calls);
                foreach (Ast.Expr arg in callExpr.Args)
                {
                    CallsIn(arg, calls);
                }

                break;
            case Ast.Unary unary:
                CallsIn(unary.Operand, calls);
                break;
            case Ast.Binary binary:
                CallsIn(binary.Left, calls);
                CallsIn(binary.Right, calls);
                break;
            case Ast.Assign assign:
                CallsIn(assign.Value, calls);
                break;
            case Ast.Ternary ternary:
                CallsIn(ternary.Cond, calls);
                CallsIn(ternary.Then, calls);
                CallsIn(ternary.Else, calls);
                break;
            case Ast.Deref deref:
                CallsIn(deref.Pointer, calls);
                break;
            case Ast.AssignTo assignTo:
                CallsIn(assignTo.Target, calls);
                CallsIn(assignTo.Value, calls);
                break;
            case Ast.AssignOpTo assignOp:
                CallsIn(assignOp.Target, calls);
                CallsIn(assignOp.Value, calls);
                break;
            case Ast.Index index:
                CallsIn(index.Base, calls);
                CallsIn(index.Offset, calls);
                break;
            case Ast.Member member:
                CallsIn(member.Base, calls);
                break;
            case Ast.AddressOfExpr addressOf:
                CallsIn(addressOf.Target, calls);
                break;
            case Ast.InitList list:
                foreach (Ast.Expr item in list.Items)
                {
                    CallsIn(item, calls);
                }

                break;
        }
    }
}
