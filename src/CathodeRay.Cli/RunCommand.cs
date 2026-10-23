using System.CommandLine;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using CathodeRay.Abstractions;
using CathodeRay.C;
using CathodeRay.Emulation;

namespace CathodeRay.Cli;

/// <summary>Komenda <c>run</c>: uruchamia obraz z <c>cc</c> na emulatorze celu (z80, 8080, 6502/65c02, 6800, 6809)
/// ze śladem wykonania, zrzutem pamięci i odczytem wyniku <c>main</c> z listingu.</summary>
internal static partial class RunCommand
{
    private const int NotHalted = 2;
    private const long MaxListingBytes = 32 * 1024 * 1024;

    /// <summary>Buduje komendę <c>run</c>.</summary>
    /// <returns>Komenda.</returns>
    public static Command Create()
    {
        var binary = new Argument<FileInfo>("binary") { Description = "Obraz z 'cathode cc' (ładowany pod --load)." };
        binary.AcceptExistingOnly();
        var cpu = new Option<string>("--cpu") { Description = "Cel: " + string.Join(", ", CpuRunners.Names) + " (65c02 działa na emulatorze NMOS: rozkazy tylko-CMOS jak STZ, BRA, PHX kończą się komunikatem o nieobsługiwanym rozkazie).", Required = true };
        cpu.AcceptOnlyFromAmong([.. CpuRunners.Names]);
        var load = new Option<string?>("--load") { Description = "Adres załadowania obrazu ($hex/0xhex/dec; domyślnie początek pamięci układu celu — jak w 'load' z cc)." };
        var start = new Option<string?>("--start") { Description = "Adres startu PC (domyślnie początek obszaru kodu celu, gdzie leży crt0)." };
        var maxSteps = new Option<long>("--max-steps") { Description = "Limit kroków (ochrona przed pętlą bez zatrzymania).", DefaultValueFactory = _ => 50_000_000 };
        var trace = new Option<bool>("--trace", "-t") { Description = "Ślad: PC sprzed kroku, bajty rozkazu i rejestry po kroku." };
        var traceLimit = new Option<long>("--trace-limit") { Description = "Maksymalna liczba linii śladu (po niej tylko licznik).", DefaultValueFactory = _ => 10_000 };
        var listing = new Option<FileInfo?>("--listing", "-l") { Description = "Listing z 'cc -l': pozwala wypisać wynik main (cc_ret) i konsolę (__io_buf)." };
        var dump = new Option<MemoryRange[]>("--dump", "-d")
        {
            Description = "Zrzut pamięci po zakończeniu, start:długość (np. 0x7000:16); można powtórzyć.",
            CustomParser = ParseRanges,
        };
        var command = new Command("run", "Uruchamia program z 'cc' na emulatorze celu do zatrzymania lub limitu kroków.") { binary, cpu, load, start, maxSteps, trace, traceLimit, listing, dump };
        command.SetAction(parse =>
        {
            TextWriter output = parse.InvocationConfiguration.Output;
            TextWriter error = parse.InvocationConfiguration.Error;
            string cpuName = parse.GetRequiredValue(cpu);
            ICTarget cTarget = CTargets.Find(cpuName)!;
            FileInfo binaryFile = parse.GetRequiredValue(binary);
            if (binaryFile.Length > 0x10000)
            {
                error.WriteLine($"Binary too large: {binaryFile.Length} B (max 65536).");
                return 1;
            }

            if (parse.GetValue(listing) is { Length: > MaxListingBytes } bigListing)
            {
                error.WriteLine($"Listing too large: {bigListing.Length} B (max {MaxListingBytes}).");
                return 1;
            }

            byte[] image = File.ReadAllBytes(binaryFile.FullName);
            int loadAddress = cTarget.Layout.Areas.Min(static a => a.Start);
            int startAddress = cTarget.Layout.Areas.First(a => a.Name == cTarget.Layout.Segments.First(static s => s.Name == "CODE").Area).Start;
            if (!TryAddress(parse.GetValue(load), "--load", ref loadAddress, error) || !TryAddress(parse.GetValue(start), "--start", ref startAddress, error))
            {
                return 1;
            }

            if (loadAddress + image.Length > 0x10000)
            {
                error.WriteLine($"Binary does not fit: load ${loadAddress:X4} + {image.Length} B exceeds 64 KB.");
                return 1;
            }

            ICpuRunner runner = CpuRunners.Create(cpuName);
            runner.Load(loadAddress, image);
            runner.Start(startAddress);
            long limit = parse.GetValue(maxSteps);
            bool tracing = parse.GetValue(trace);
            long traceMax = parse.GetValue(traceLimit);
            long steps = 0;
            long begin = Stopwatch.GetTimestamp();
            try
            {
                while (!runner.Halted && steps < limit)
                {
                    int pc = runner.Pc;
                    runner.Step();
                    steps++;
                    if (tracing && steps <= traceMax)
                    {
                        string bytes = string.Join(' ', Enumerable.Range(0, 3).Select(i => runner.Read((pc + i) & 0xFFFF).ToString("X2", CultureInfo.InvariantCulture)));
                        output.WriteLine($"{pc:X4}  {bytes,-8}  {runner.Registers()}");
                    }
                }
            }
            catch (Exception e) when (e is NotSupportedException or InvalidOperationException or NotImplementedException)
            {
                error.WriteLine($"run: {cpuName}: unsupported instruction at ${runner.Pc:X4} after {steps} steps: {e.Message}");
                return 1;
            }

            if (tracing && steps > traceMax)
            {
                output.WriteLine($"... trace truncated at {traceMax} of {steps} steps (--trace-limit)");
            }

            TimeSpan elapsed = Stopwatch.GetElapsedTime(begin);
            string cycles = runner.Cycles > 0 ? $" cycles={runner.Cycles}" : string.Empty;
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"steps={steps}{cycles} halted={runner.Halted} pc={runner.Pc:X4} time={elapsed.TotalMilliseconds:F3} ms"));
            output.WriteLine(runner.Registers());
            if (!runner.Halted)
            {
                error.WriteLine($"run: step limit {limit} reached without halting (pc=${runner.Pc:X4}); raise --max-steps or check for an endless loop.");
            }

            if (parse.GetValue(listing) is { } listingFile)
            {
                ReportProgramResult(File.ReadAllText(listingFile.FullName), runner, output);
            }

            var bus = new RunnerBus(runner);
            foreach (MemoryRange range in parse.GetValue(dump) ?? [])
            {
                range.WriteHexDump(output, bus);
            }

            return runner.Halted ? 0 : NotHalted;
        });
        return command;
    }

    private static void ReportProgramResult(string listing, ICpuRunner runner, TextWriter output)
    {
        if (Symbol(listing, "cc_ret") is int ret)
        {
            int value = runner.Read(ret) | (Symbol(listing, "cc_ret_h") is int high ? runner.Read(high) << 8 : 0);
            int signed = value > short.MaxValue ? value - 0x10000 : value;
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"result: {signed} (0x{value:X4})"));
        }
        else
        {
            output.WriteLine("result: cc_ret not found in the listing.");
        }

        if (Symbol(listing, "__io_cur") is int cursor && Symbol(listing, "__io_buf") is int buffer)
        {
            string console = new([.. Enumerable.Range(0, runner.Read(cursor)).Select(i => (char)runner.Read(buffer + i))]);
            output.WriteLine("console: " + Printable(console));
        }
    }

    /// <summary>Zastępuje znaki sterujące (poza nową linią i tabulacją), żeby program nie sterował terminalem.</summary>
    private static string Printable(string text) =>
        new([.. text.Select(static c => char.IsControl(c) && c is not ('\n' or '\t') ? '.' : c)]);

    private static int? Symbol(string listing, string name)
    {
        foreach (string line in listing.Split('\n'))
        {
            Match row = ListingRow().Match(line);
            if (!row.Success)
            {
                continue;
            }

            foreach (Match label in ListingLabel().Matches(line, row.Length))
            {
                if (label.Groups[1].Value == name || label.Groups[1].Value == "cc_g_" + name)
                {
                    return Convert.ToInt32(row.Groups[1].Value, 16);
                }
            }
        }

        return null;
    }

    [GeneratedRegex(@"^\s*([0-9A-Fa-f]{4})\s")]
    private static partial Regex ListingRow();

    [GeneratedRegex(@"\b(\w+):")]
    private static partial Regex ListingLabel();

    private static bool TryAddress(string? text, string option, ref int address, TextWriter error)
    {
        if (text is null)
        {
            return true;
        }

        if (!NumberLiteral.TryParse(text, out address) || address is < 0 or > ushort.MaxValue)
        {
            error.WriteLine($"Invalid {option} '{text}' (expected $0000..$FFFF).");
            return false;
        }

        return true;
    }

    private static MemoryRange[] ParseRanges(System.CommandLine.Parsing.ArgumentResult result)
    {
        var ranges = new List<MemoryRange>();
        foreach (System.CommandLine.Parsing.Token token in result.Tokens)
        {
            if (!MemoryRange.TryParse(token.Value, out MemoryRange range, out string? error))
            {
                result.AddError(error!);
                return [];
            }

            ranges.Add(range);
        }

        return [.. ranges];
    }

    /// <summary>Szyna tylko do odczytu nad emulatorem (zrzuty pamięci).</summary>
    private sealed class RunnerBus(ICpuRunner runner) : IBus
    {
        public byte Read(ushort address) => (byte)runner.Read(address);

        public void Write(ushort address, byte value) => throw new NotSupportedException("read-only bus.");
    }
}
