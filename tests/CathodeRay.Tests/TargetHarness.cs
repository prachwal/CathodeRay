using CathodeRay.Assembler;
using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Uruchamia moduł IR na wybranym celu: druk asemblera celu, złożenie razem z crt0 (jeden plik, układ pamięci celu),
/// wykonanie na runnerze, odczyt wyniku i symboli z pamięci.</summary>
public static class TargetHarness
{
    /// <summary>Wynik uruchomienia.</summary>
    /// <param name="Runner">Runner po zatrzymaniu procesora.</param>
    /// <param name="Image">Złożony program.</param>
    /// <param name="Steps">Liczba wykonanych instrukcji.</param>
    public sealed record Result(ICpuRunner Runner, AssemblyResult Image, long Steps)
    {
        public int Return => Runner.Read(Image.Symbols["cc_ret"]) | (Runner.Read(Image.Symbols["cc_ret_h"]) << 8);

        public byte[] Read(string symbol, int size) =>
            [.. Enumerable.Range(0, size).Select(i => (byte)Runner.Read(Image.Symbols[symbol] + i))];
    }

    public static IEnumerable<ICTarget> Targets => CTargets.All.Where(static t => Runners.Has(t.Name));

    public static ICpuRunner CreateRunner(ICTarget target) => Runners.Create(target.Name);

    public static Result Run(ICTarget target, Ir.Module module, bool optimize = true, long maxSteps = 5_000_000)
    {
        string asm = target.Crt0 + target.Emit(module, optimize);
        return RunAssembly(target, asm, maxSteps);
    }

    public static Result RunAssembly(ICTarget target, string asm, long maxSteps = 5_000_000)
    {
        AssemblerTarget assemblerTarget = AssemblerTargets.Find(target.AssemblerCpu)!;
        var origins = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (TargetSegment segment in target.Layout.Segments)
        {
            origins[segment.Name] = target.Layout.Areas.Single(a => a.Name == segment.Area).Start;
        }

        AssemblyResult image = new TwoPassAssembler(Repo.LoadTarget(assemblerTarget), assemblerTarget.DefaultSyntax)
            .Assemble(asm, "prog", _ => null, [], null, origins);
        ICpuRunner runner = CreateRunner(target);
        runner.Load(image.Origin, image.Image);
        runner.Start(origins["CODE"]);
        long steps = 0;
        while (!runner.Halted)
        {
            (steps++).Should().BeLessThan(maxSteps, "program ma się zatrzymać");
            runner.Step();
        }

        return new Result(runner, image, steps);
    }
}
