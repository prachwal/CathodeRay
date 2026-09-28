using System.CommandLine;
using System.Diagnostics;
using System.Globalization;
using CathodeRay.Abstractions;
using CathodeRay.Stub;

namespace CathodeRay.Cli;

/// <summary>Grupa <c>stub</c>: uruchamianie programów dla ISA zaślepki (asemblacja: <c>cathode asm --cpu stub</c>).</summary>
internal static class StubCommands
{
    private const int NotHalted = 2;

    /// <summary>Buduje grupę <c>stub</c> z komendą <c>run</c>.</summary>
    /// <returns>Komenda grupy.</returns>
    public static Command Create()
    {
        var isa = new Option<FileInfo>("--isa")
        {
            Description = "Plik ISA JSON (domyślnie mcp_stub_instructions.json obok programu).",
            DefaultValueFactory = _ => new FileInfo(Path.Combine(AppContext.BaseDirectory, "mcp_stub_instructions.json")),
            Recursive = true,
        };
        isa.AcceptExistingOnly();

        var group = new Command("stub", "Uruchamianie programów dla ISA zaślepki.") { isa };
        group.Subcommands.Add(CreateRun(isa));
        return group;
    }

    private static Command CreateRun(Option<FileInfo> isa)
    {
        var binary = new Argument<FileInfo>("binary") { Description = "Plik binarny ładowany od adresu 0." };
        binary.AcceptExistingOnly();
        var maxSteps = new Option<int>("--max-steps")
        {
            Description = "Limit kroków (ochrona przed pętlą bez HLT).",
            DefaultValueFactory = _ => 1_000_000,
        };
        var trace = new Option<bool>("--trace", "-t") { Description = "Wypisuje każdy wykonany krok." };
        var dump = new Option<MemoryRange[]>("--dump", "-d")
        {
            Description = "Zrzut pamięci po zakończeniu, start:długość (np. 0x2000:16; w powłoce unikaj $, bo rozwinie np. $0); można powtórzyć.",
            CustomParser = ParseRanges,
        };

        var command = new Command("run", "Uruchamia program do HLT lub limitu kroków.") { binary, maxSteps, trace, dump };
        command.SetAction(parse =>
        {
            TextWriter output = parse.InvocationConfiguration.Output;
            byte[] image = File.ReadAllBytes(parse.GetRequiredValue(binary).FullName);
            if (image.Length > 0x10000)
            {
                parse.InvocationConfiguration.Error.WriteLine($"Binary too large: {image.Length} B (max 65536).");
                return 1;
            }

            var bus = new StubBus();
            for (int i = 0; i < image.Length; i++)
            {
                bus.Write((ushort)i, image[i]);
            }

            var cpu = new StubCpu(LoadIsa(parse, isa), bus);
            int limit = parse.GetValue(maxSteps);
            int steps;
            long start = Stopwatch.GetTimestamp();
            try
            {
                steps = parse.GetValue(trace)
                    ? new CpuDiagnostics(cpu, new TraceObserver(output)).Run(limit)
                    : RunToHalt(cpu, limit);
            }
            catch (InvalidOperationException e)
            {
                parse.InvocationConfiguration.Error.WriteLine(e.Message);
                return 1;
            }

            TimeSpan elapsed = Stopwatch.GetElapsedTime(start);
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"steps={steps} cycles={cpu.CycleCount} halted={cpu.State.Halted} time={elapsed.TotalMilliseconds:F3} ms"));
            output.WriteLine(TraceObserver.Format(cpu.CaptureRegisters()));
            foreach (MemoryRange range in parse.GetValue(dump) ?? [])
            {
                range.WriteHexDump(output, bus);
            }

            return cpu.State.Halted ? 0 : NotHalted;
        });
        return command;
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

    private static StubIsa LoadIsa(ParseResult parse, Option<FileInfo> isa) =>
        StubIsa.FromJsonFile(parse.GetRequiredValue(isa).FullName);

    private static int RunToHalt(StubCpu cpu, int maxSteps)
    {
        int steps = 0;
        while (steps < maxSteps && !cpu.State.Halted)
        {
            cpu.Step();
            steps++;
        }

        return steps;
    }

    /// <summary>Obserwator dla <c>--trace</c>: linia na krok, zatrzymanie na HLT.</summary>
    private sealed class TraceObserver(TextWriter output) : ICpuExecutionObserver
    {
        public static string Format(RegisterView registers) =>
            string.Join(' ', registers.Select(static r => $"{r.Name}={r.Format()}"));

        public bool ShouldBreak(CpuDebugSnapshot snapshot) => snapshot.Halted;

        public void OnStepCompleted(CpuStepTrace step)
        {
            CpuDebugSnapshot after = step.After;
            output.WriteLine(
                $"{step.Before.ProgramCounter:X4}  {after.LastOpcode:X2} {after.LastMnemonic,-4} {Format(after.Registers)}  +{step.Cycles} cyc={after.CycleCount} bus={after.LastBusActivity}");
        }
    }
}
