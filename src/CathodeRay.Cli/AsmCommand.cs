using System.CommandLine;
using System.Globalization;
using CathodeRay.Assembler;
using CathodeRay.Assembler.Syntax;

namespace CathodeRay.Cli;

/// <summary>Komenda <c>asm</c>: asembler wspólny dla wszystkich CPU (<c>--cpu</c>) i dialektów składni (<c>--syntax</c>).</summary>
internal static class AsmCommand
{
    /// <summary>Buduje komendę <c>asm</c>.</summary>
    /// <returns>Komenda.</returns>
    public static Command Create()
    {
        var source = new Argument<FileInfo>("source") { Description = "Plik źródłowy asemblera." };
        source.AcceptExistingOnly();
        var cpu = new Option<string>("--cpu", "-c")
        {
            Description = "CPU: " + string.Join(", ", AssemblerTargets.All.Select(static t => $"{t.Name} ({t.Description})")) + ".",
            Required = true,
        };
        cpu.AcceptOnlyFromAmong([.. AssemblerTargets.All.Select(static t => t.Name)]);
        var syntax = new Option<string?>("--syntax", "-s")
        {
            Description = "Dialekt składni (domyślnie pierwszy dozwolony dla CPU): "
                + string.Join("; ", AssemblerTargets.All.Select(static t => $"{t.Name}: {string.Join("/", t.Syntaxes.Select(static s => s.Name))}")) + ".",
        };
        var illegal = new Option<bool>("--illegal") { Description = "Nieudokumentowane instrukcje NMOS 6502 (skrót od --cpu 6502x, jak .setcpu \"6502X\" w ca65)." };
        var output = new Option<FileInfo?>("--output", "-o") { Description = "Plik binarny (domyślnie <source>.bin)." };
        var listing = new Option<FileInfo?>("--listing", "-l") { Description = "Plik listingu: adres, bajty, linia źródła." };
        var isa = new Option<FileInfo>("--isa") { Description = "Plik ISA JSON zamiast domyślnego dla CPU." };
        isa.AcceptExistingOnly();
        var incdir = new Option<DirectoryInfo[]>("--incdir") { Description = "Dodatkowe katalogi poszukiwań .include (można powtarzać)." };
        var format = new Option<string>("--format", "-f") { Description = "Format wyjścia: bin (domyślnie) albo hex (Intel HEX)." };
        format.AcceptOnlyFromAmong(["bin", "hex"]);
        var map = new Option<string[]>("--map", "-m") { Description = "Bazowy adres segmentu NAZWA@adres (powtarzalne, adres $hex/0xhex/dec)." };

        var command = new Command("asm", "Asembluje źródło do pliku binarnego.") { source, cpu, syntax, illegal, output, listing, isa, incdir, format, map };
        command.SetAction(parse =>
        {
            TextWriter error = parse.InvocationConfiguration.Error;
            FileInfo src = parse.GetRequiredValue(source);
            string cpuName = parse.GetRequiredValue(cpu);
            if (parse.GetValue(illegal))
            {
                if (!cpuName.Equals("6502", StringComparison.OrdinalIgnoreCase) && !cpuName.Equals("6502x", StringComparison.OrdinalIgnoreCase))
                {
                    error.WriteLine($"--illegal applies only to --cpu 6502 (got {cpuName}).");
                    return 1;
                }

                cpuName = "6502x";
            }

            AssemblerTarget target = AssemblerTargets.Find(cpuName)!;
            string? syntaxName = parse.GetValue(syntax);
            SyntaxDialect? dialect = syntaxName is null ? target.DefaultSyntax : target.FindSyntax(syntaxName);
            if (dialect is null)
            {
                error.WriteLine($"Syntax '{syntaxName}' is not available for --cpu {target.Name} (allowed: {string.Join(", ", target.Syntaxes.Select(static s => s.Name))}).");
                return 1;
            }

            string isaPath = parse.GetValue(isa)?.FullName ?? Path.Combine(AppContext.BaseDirectory, target.IsaFile);
            string[] includePaths = [.. parse.GetValue(incdir)!.Select(static d => d.FullName)];
            Dictionary<string, int> segmentOrigins = [];
            foreach (string entry in parse.GetValue(map)!)
            {
                int at = entry.LastIndexOf('@');
                if (at <= 0 || !CathodeRay.NumberLiteral.TryParse(entry[(at + 1)..], out int address) || address > ushort.MaxValue)
                {
                    error.WriteLine($"Invalid --map '{entry}' (expected NAME@address, address $0000..$FFFF).");
                    return 1;
                }

                string name = entry[..at];
                if (!segmentOrigins.TryAdd(name, address))
                {
                    error.WriteLine($"Duplicate --map for segment '{name}'.");
                    return 1;
                }
            }

            AssemblyResult result;
            try
            {
                using FileStream json = File.OpenRead(isaPath);
                string entryText = File.ReadAllText(src.FullName);
                Func<string, string?> reader = path => File.Exists(path) ? File.ReadAllText(path) : null;
                Func<string, byte[]?> binaryReader = path => File.Exists(path) ? File.ReadAllBytes(path) : null;
                result = new TwoPassAssembler(target.Load(json), dialect).Assemble(entryText, src.FullName, reader, includePaths, binaryReader, segmentOrigins);
            }
            catch (AssemblerException e)
            {
                foreach (AssemblerError err in e.Errors)
                {
                    error.WriteLine($"{err.File ?? src.Name}: line {err.Line}: {err.Message}");
                }

                return 1;
            }

            string? unknown = segmentOrigins.Keys.FirstOrDefault(name => !result.Segments.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)));
            if (unknown is not null)
            {
                error.WriteLine($"--map names unused segment '{unknown}'.");
                return 1;
            }

            FileInfo dst = parse.GetValue(output)
                ?? new FileInfo(Path.ChangeExtension(src.FullName, parse.GetValue(format) == "hex" ? ".hex" : ".bin"));
            if (parse.GetValue(format) == "hex")
            {
                using StreamWriter writer = File.CreateText(dst.FullName);
                result.WriteIntelHex(writer);
            }
            else
            {
                File.WriteAllBytes(dst.FullName, result.Image);
            }

            if (parse.GetValue(listing) is { } listingFile)
            {
                using StreamWriter writer = File.CreateText(listingFile.FullName);
                result.WriteListing(writer);
            }

            parse.InvocationConfiguration.Output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{src.Name} -> {dst.Name} ({result.Image.Length} B, load ${result.Origin:X4}, cpu {target.Name}, syntax {dialect.Name})"));
            return 0;
        });
        return command;
    }
}
