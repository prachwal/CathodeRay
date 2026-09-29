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

    public static Result Run(string source, params string[] extra)
    {
        string dir = Directory.CreateTempSubdirectory("cathode-cc-run-").FullName;
        try
        {
            string main = Path.Combine(dir, "main.c");
            File.WriteAllText(main, source);
            string bin = Path.Combine(dir, "p.bin");
            string map = Path.Combine(dir, "p.map");
            var error = new StringWriter();
            string[] args = ["cc", main, "-o", bin, "-l", Path.Combine(dir, "p.lst"), .. extra];
            int exit = CliApp.CreateRoot().Parse(args).Invoke(new System.CommandLine.InvocationConfiguration { Output = new StringWriter(), Error = error });
            exit.Should().Be(0, error.ToString());
            byte[] image = File.ReadAllBytes(bin);
            var bus = new StubBus();
            for (int i = 0; i < image.Length; i++)
            {
                bus.Write((ushort)(0x1000 + i), image[i]);
            }

            var cpu = new StubCpu(StubIsa.FromJsonFile(Repo.IsaFile("mcp_stub_instructions.json")), bus);
            cpu.State.ProgramCounter = 0x1000;
            long steps = 0;
            while (!cpu.State.Halted)
            {
                (steps++).Should().BeLessThan(20_000_000, "program ma się zatrzymać");
                cpu.Step();
            }

            string listing = File.ReadAllText(Path.Combine(dir, "p.lst"));
            string console = string.Empty;
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(listing, @"(?m)^\s*([0-9A-Fa-f]{4})\s.*__io_cur:");
            if (match.Success)
            {
                int cursor = bus.Read((ushort)Convert.ToInt32(match.Groups[1].Value, 16));
                System.Text.RegularExpressions.Match buf = System.Text.RegularExpressions.Regex.Match(listing, @"(?m)^\s*([0-9A-Fa-f]{4})\s.*__io_buf:");
                int start = Convert.ToInt32(buf.Groups[1].Value, 16);
                console = new string([.. Enumerable.Range(0, cursor).Select(i => (char)bus.Read((ushort)(start + i)))]);
            }

            return new Result((cpu.State.X * 256) + cpu.State.A, console, error.ToString(), steps);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
