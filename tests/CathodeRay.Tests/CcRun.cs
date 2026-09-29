using CathodeRay.Cli;
using CathodeRay.Stub;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Kompiluje programy C sterownikiem <c>cc</c> (z biblioteką standardową), uruchamia je na stubie i
/// zwraca wynik oraz zawartość konsoli (<c>__io_buf</c>).</summary>
public static class CcRun
{
    public sealed record Result(int Value, string Console, string Stderr, long Steps);

    /// <summary>Tylko kompilacja i linkowanie: kod wyjścia i stderr.</summary>
    public static (int Exit, string Stderr) Compile(string source, params string[] extra)
    {
        string dir = Directory.CreateTempSubdirectory("cathode-cc-compile-").FullName;
        try
        {
            string main = Path.Combine(dir, "main.c");
            File.WriteAllText(main, source);
            var error = new StringWriter();
            string[] args = ["cc", main, "-o", Path.Combine(dir, "p.bin"), .. extra];
            int exit = CliApp.CreateRoot().Parse(args).Invoke(new System.CommandLine.InvocationConfiguration { Output = new StringWriter(), Error = error });
            return (exit, error.ToString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Rozmiary segmentów po linkowaniu (<c>cc --stats</c>).</summary>
    public static IReadOnlyDictionary<string, int> Sizes(string source, string cpu, params string[] extra)
    {
        string dir = Directory.CreateTempSubdirectory("cathode-cc-sizes-").FullName;
        try
        {
            string main = Path.Combine(dir, "main.c");
            File.WriteAllText(main, source);
            var output = new StringWriter();
            var error = new StringWriter();
            string[] args = ["cc", main, "-o", Path.Combine(dir, "p.bin"), "--cpu", cpu, "--stats", .. extra];
            int exit = CliApp.CreateRoot().Parse(args).Invoke(new System.CommandLine.InvocationConfiguration { Output = output, Error = error });
            exit.Should().Be(0, error.ToString());
            string line = output.ToString().Split('\n').First(static l => l.StartsWith("stats:", StringComparison.Ordinal));
            return line["stats:".Length..].Split(',', StringSplitOptions.TrimEntries)
                .Select(static part => part.Split(' '))
                .ToDictionary(static part => part[0], static part => int.Parse(part[1], System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    public static Result Run(string source, params string[] extra) => RunOn(source, "stub", extra);

    public static Result RunOn(string source, string cpu, params string[] extra)
    {
        string dir = Directory.CreateTempSubdirectory("cathode-cc-run-").FullName;
        try
        {
            string main = Path.Combine(dir, "main.c");
            File.WriteAllText(main, source);
            string bin = Path.Combine(dir, "p.bin");
            var error = new StringWriter();
            var output = new StringWriter();
            string[] args = ["cc", main, "-o", bin, "-l", Path.Combine(dir, "p.lst"), "--cpu", cpu, .. extra];
            int exit = CliApp.CreateRoot().Parse(args).Invoke(new System.CommandLine.InvocationConfiguration { Output = output, Error = error });
            exit.Should().Be(0, error.ToString());
            byte[] image = File.ReadAllBytes(bin);
            ICpuRunner runner = Runners.Create(cpu);
            int load = Convert.ToInt32(System.Text.RegularExpressions.Regex.Match(output.ToString(), @"load \$([0-9A-Fa-f]+)").Groups[1].Value, 16);
            runner.Load(load, image);
            runner.Start(0x1000);
            long steps = 0;
            while (!runner.Halted)
            {
                (steps++).Should().BeLessThan(50_000_000, "program ma się zatrzymać");
                runner.Step();
            }

            string listing = File.ReadAllText(Path.Combine(dir, "p.lst"));
            string console = string.Empty;
            int? cursorAddress = Symbol(listing, "__io_cur");
            if (cursorAddress is int cursor)
            {
                int start = Symbol(listing, "__io_buf")!.Value;
                console = new string([.. Enumerable.Range(0, runner.Read(cursor)).Select(i => (char)runner.Read(start + i))]);
            }

            int value = cpu == "stub"
                ? ((StubRunner)runner).Word()
                : runner.Read(Symbol(listing, "cc_ret")!.Value) | (runner.Read(Symbol(listing, "cc_ret_h")!.Value) << 8);
            return new Result(value, console, error.ToString(), steps);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static int? Symbol(string listing, string name)
    {
        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(listing, @"(?m)^\s*([0-9A-Fa-f]{4})\s.*\b(?:cc_g_)?" + name + ":");
        return match.Success ? Convert.ToInt32(match.Groups[1].Value, 16) : null;
    }
}
