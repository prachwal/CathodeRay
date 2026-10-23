using System.Globalization;
using CathodeRay.C;

namespace CathodeRay.Cli;

/// <summary>Diagnostyka komendy <c>cc</c>: zrzut IR (<c>--emit-ir</c>), przydział rejestrów (<c>--dump-regalloc</c>)
/// i komunikaty o przebiegu (<c>--verbose</c>). Zrzuty dotyczą tylko modułów użytkownika, nie biblioteki standardowej.</summary>
internal sealed class CcDiagnostics : ICompilerTrace
{
    private readonly TextWriter? _ir;
    private readonly TextWriter? _regalloc;
    private readonly TextWriter? _verbose;
    private string? _unit;

    public CcDiagnostics(TextWriter? ir, TextWriter? regalloc, TextWriter? verbose)
    {
        _ir = ir;
        _regalloc = regalloc;
        _verbose = verbose;
    }

    /// <summary>Czy cokolwiek trzeba zbierać z kompilatora (zrzuty IR lub alokatora).</summary>
    public bool NeedsTrace => _ir is not null || _regalloc is not null;

    /// <summary>Nazwa modułu, którego dotyczą kolejne zrzuty; null = pomijaj (biblioteka standardowa).</summary>
    public string? Unit
    {
        get => _unit;
        set => _unit = value is null ? null : new string([.. value.Select(static c => char.IsControl(c) ? '?' : c)]);
    }

    /// <summary>Komunikat o przebiegu (tylko z <c>--verbose</c>).</summary>
    /// <param name="message">Tekst.</param>
    public void Verbose(string message) => _verbose?.WriteLine("cc: " + message);

    /// <inheritdoc/>
    public void Ir(string stage, Ir.Module module)
    {
        if (_ir is null || Unit is null)
        {
            return;
        }

        _ir.WriteLine(string.Create(CultureInfo.InvariantCulture, $"; ===== {Unit}: IR after '{stage}' ====="));
        _ir.Write(IrFormatter.Format(module));
        _ir.WriteLine();
    }

    /// <inheritdoc/>
    public void Allocation(Ir.Module module, IReadOnlyDictionary<string, string> assignment, IReadOnlyDictionary<string, List<(string Sym, string Cause)>> causes)
    {
        if (_regalloc is null || Unit is null)
        {
            return;
        }

        _regalloc.WriteLine($"; ===== {Unit}: register allocation =====");
        foreach (Ir.Function function in module.Functions)
        {
            _regalloc.WriteLine($"func {function.Name}");
            if (!causes.TryGetValue(function.Name, out List<(string Sym, string Cause)>? rows) || rows.Count == 0)
            {
                _regalloc.WriteLine("  (no cells)");
                continue;
            }

            foreach ((string sym, string cause) in rows)
            {
                string where = assignment.TryGetValue(sym, out string? reg) ? reg : "memory";
                _regalloc.WriteLine($"  {sym,-28} {where,-8} {cause}");
            }
        }

        _regalloc.WriteLine();
    }
}
