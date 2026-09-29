namespace CathodeRay.C;

/// <summary>Kontrola typów: analiza przepływu i ostrzeżenia (nieużywane nazwy, brak <c>return</c>,
/// martwy kod, przypisanie w warunku).</summary>
public sealed partial class TypeChecker
{
    private static bool AlwaysReturns(Ast.Stmt? stmt) => stmt switch
    {
        Ast.Return or Ast.Goto => true,
        Ast.Block block => block.Items.Any(AlwaysReturns),
        Ast.If { Else: not null } ifStmt => AlwaysReturns(ifStmt.Then) && AlwaysReturns(ifStmt.Else),
        Ast.While loop => IsTrue(loop.Cond) && !HasBreak(loop.Body),
        Ast.For loop => (loop.Cond is null || IsTrue(loop.Cond)) && !HasBreak(loop.Body),
        Ast.DoWhile loop => AlwaysReturns(loop.Body) || (IsTrue(loop.Cond) && !HasBreak(loop.Body)),
        Ast.Switch sw => sw.Cases.Count > 0 && sw.Cases.Any(static c => c.Value is null)
            && !sw.Cases.Any(static c => c.Body.Any(HasBreak)) && sw.Cases[^1].Body.Any(AlwaysReturns),
        _ => false,
    };

    private static bool IsTrue(Ast.Expr expr) => expr is Ast.Number number && NumberValue(number.Text) != 0;

    /// <summary>Czy w instrukcji jest <c>break</c> wiążący się z otaczającą pętlą (bez zagnieżdżonych pętli i switch).</summary>
    private static bool HasBreak(Ast.Stmt? stmt) => stmt switch
    {
        Ast.Break => true,
        Ast.Block block => block.Items.Any(HasBreak),
        Ast.If ifStmt => HasBreak(ifStmt.Then) || HasBreak(ifStmt.Else),
        _ => false,
    };

    private void Warn(string message) =>
        _warnings.Add(_stmtLine > 0 ? $"line {_stmtLine}: {message}" : message);

    private void ReportUnused(IEnumerable<(string Name, int Line, bool IsParameter)> declared)
    {
        foreach ((string name, int line, bool isParameter) in declared)
        {
            if (!_usedNames.Contains(name) && !name.StartsWith('_'))
            {
                _warnings.Add($"{(line > 0 ? $"line {line}: " : string.Empty)}unused {(isParameter ? "parameter" : "variable")} '{name}'.");
            }
        }
    }
}
