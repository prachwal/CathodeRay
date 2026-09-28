using System.CommandLine;
using CathodeRay.Abstractions;
using CathodeRay.Stub;

namespace CathodeRay.Cli;

/// <summary>Grupa <c>stub</c>: asembler i uruchamianie programów dla ISA zaślepki.</summary>
internal static class StubCommands
{
    private const int NotHalted = 2;

    /// <summary>Buduje grupę <c>stub</c> z komendami <c>asm</c> i <c>run</c>.</summary>
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

        var group = new Command("stub", "Asembler i uruchamianie programów dla ISA zaślepki.") { isa };
        group.Subcommands.Add(CreateAsm(isa));
        group.Subcommands.Add(CreateRun(isa));
        return group;
    }

    private static Command CreateAsm(Option<FileInfo> isa)
    {
        var source = new Argument<FileInfo>("source") { Description = "Plik źródłowy asemblera." };
        source.AcceptExistingOnly();
        var output = new Option<FileInfo?>("--output", "-o") { Description = "Plik binarny (domyślnie <source>.bin)." };

        var command = new Command("asm", "Asembluje źródło do pliku binarnego.") { source, output };
        command.SetAction(parse =>
        {
            FileInfo src = parse.GetRequiredValue(source);
            FileInfo dst = parse.GetValue(output) ?? new FileInfo(Path.ChangeExtension(src.FullName, ".bin"));
            try
            {
                byte[] image = StubAssembler.Assemble(File.ReadAllText(src.FullName), LoadIsa(parse, isa));
                File.WriteAllBytes(dst.FullName, image);
                parse.InvocationConfiguration.Output.WriteLine($"{src.Name} -> {dst.Name} ({image.Length} B)");
                return 0;
            }
            catch (AssemblerException e)
            {
                parse.InvocationConfiguration.Error.WriteLine($"{src.Name}: {e.Message}");
                return 1;
            }
        });
        return command;
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
            Description = "Zrzut pamięci po zakończeniu, start:długość (np. $2000:16); można powtórzyć.",
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
            int steps;
            try
            {
                var observer = new ConsoleObserver(output, parse.GetValue(trace));
                steps = new CpuDiagnostics(cpu, observer).Run(parse.GetValue(maxSteps));
            }
            catch (InvalidOperationException e)
            {
                parse.InvocationConfiguration.Error.WriteLine(e.Message);
                return 1;
            }

            output.WriteLine($"steps={steps} cycles={cpu.CycleCount} halted={cpu.State.Halted}");
            output.WriteLine(ConsoleObserver.Format(cpu.CaptureRegisters()));
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

    private sealed class ConsoleObserver(TextWriter output, bool trace) : ICpuExecutionObserver
    {
        public static string Format(RegisterView registers) =>
            string.Join(' ', registers.Select(static r => $"{r.Name}={r.Format()}"));

        public bool ShouldBreak(CpuDebugSnapshot snapshot) => snapshot.Halted;

        public void OnStepCompleted(CpuStepTrace step)
        {
            if (trace)
            {
                output.WriteLine($"{step.Before.ProgramCounter:X4}  {step.After.LastMnemonic,-4} {Format(step.After.Registers)}  +{step.Cycles}");
            }
        }
    }
}
