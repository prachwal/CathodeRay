using System.CommandLine;
using System.Globalization;
using System.Text.RegularExpressions;
using CathodeRay.Assembler;
using CathodeRay.Assembler.Link;
using CathodeRay.C;

namespace CathodeRay.Cli;

/// <summary>Komenda <c>cc</c>: sterownik mini-C (tylko stub) — pliki .c przez codegen,
/// .s wprost, każdy moduł do obiektu, crt0 zawsze pierwsze, link w jednym wywołaniu.</summary>
internal static partial class CcCommand
{
    /// <summary>Buduje komendę <c>cc</c>.</summary>
    /// <returns>Komenda.</returns>
    public static Command Create()
    {
        var inputs = new Argument<FileInfo[]>("inputs") { Description = "Pliki .c (mini-C) i .s (stub) do połączenia." };
        var output = new Option<FileInfo?>("--output", "-o") { Description = "Plik wynikowy (domyślnie <pierwszy>.bin/.hex)." };
        var format = new Option<string>("--format", "-f") { Description = "Format wyjścia: bin (domyślnie) albo hex (Intel HEX)." };
        format.AcceptOnlyFromAmong(["bin", "hex"]);
        var listing = new Option<FileInfo?>("--listing", "-l") { Description = "Plik listingu zlinkowanego programu." };
        var map = new Option<FileInfo?>("--map") { Description = "Plik mapy debug (adres ↔ plik C:linia, z komentarzy ;c:)." };
        var config = new Option<FileInfo?>("--config") { Description = "Mapa pamięci linkera (.cfg; domyślnie layout C)." };
        var incdir = new Option<DirectoryInfo[]>("--incdir") { Description = "Dodatkowe katalogi poszukiwań .include (można powtarzać)." };

        var command = new Command("cc", "Kompiluje program mini-C na stub (C→obiekt→link).") { inputs, output, format, listing, map, config, incdir };
        command.SetAction(parse =>
        {
            TextWriter error = parse.InvocationConfiguration.Error;
            FileInfo[] files = parse.GetRequiredValue(inputs);
            foreach (FileInfo input in files)
            {
                if (!input.Exists)
                {
                    error.WriteLine($"File does not exist: {input.FullName}.");
                    return 1;
                }

                if (!input.Extension.Equals(".c", StringComparison.OrdinalIgnoreCase)
                    && !input.Extension.Equals(".s", StringComparison.OrdinalIgnoreCase))
                {
                    error.WriteLine($"{input.Name}: expected .c or .s input.");
                    return 1;
                }
            }

            if (files.Length == 0)
            {
                error.WriteLine("No input files.");
                return 1;
            }

            string[] includePaths = [.. parse.GetValue(incdir)!.Select(static d => d.FullName)];
            AssemblerTarget target = AssemblerTargets.Find("stub")!;
            var modules = new List<(string File, ObjectModule Module)>();
            try
            {
                modules.Add(("crt0.s", AssembleObject(target, Crt0.Source, "crt0.s", _ => null, includePaths)));
                foreach (FileInfo input in files)
                {
                    modules.Add((input.Name, AssembleModule(target, input, includePaths)));
                }
            }
            catch (AssemblerException e)
            {
                foreach (AssemblerError err in e.Errors)
                {
                    error.WriteLine($"{err.File ?? "cc"}: line {err.Line}: {err.Message}");
                }

                return 1;
            }
            catch (CParseException e)
            {
                error.WriteLine($"parse: {e.Message}");
                return 1;
            }
            catch (CPreprocessException e)
            {
                error.WriteLine($"preprocess: {e.Message}");
                return 1;
            }
            catch (CTypeException e)
            {
                error.WriteLine($"type: {e.Message}");
                return 1;
            }
            catch (CCodegenException e)
            {
                error.WriteLine($"codegen: {e.Message}");
                return 1;
            }

            LinkerConfig linkerConfig;
            try
            {
                FileInfo? cfg = parse.GetValue(config);
                if (cfg is not null && !cfg.Exists)
                {
                    error.WriteLine($"Config does not exist: {cfg.FullName}.");
                    return 1;
                }

                linkerConfig = cfg is not null
                    ? LinkerConfig.Parse(File.ReadAllText(cfg.FullName))
                    : DefaultConfig();
            }
            catch (LinkerException e)
            {
                error.WriteLine($"config: {e.Message}");
                return 1;
            }

            AssemblyResult result;
            try
            {
                result = Linker.Link(modules, linkerConfig);
            }
            catch (LinkerException e)
            {
                error.WriteLine($"link: {e.Message}");
                return 1;
            }

            string kind = parse.GetValue(format) ?? "bin";
            FileInfo dst = parse.GetValue(output)
                ?? new FileInfo(Path.ChangeExtension(files[0].FullName, kind == "hex" ? ".hex" : ".bin"));
            if (kind == "hex")
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

            if (parse.GetValue(map) is { } mapFile)
            {
                File.WriteAllText(mapFile.FullName, WriteDebugMap(result));
            }

            parse.InvocationConfiguration.Output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{dst.Name} ({result.Image.Length} B, load ${result.Origin:X4}, {modules.Count} modules)"));
            return 0;
        });
        return command;
    }

    /// <summary>Domyślny layout C (jak w testach runtime).</summary>
    /// <returns>Konfiguracja linkera.</returns>
    internal static LinkerConfig DefaultConfig() => new(
        [
            new MemoryArea("C_CODE", 0x1000, 0x1000),
            new MemoryArea("C_BSS", 0x2000, 0x100),
            new MemoryArea("C_DATA", 0x2100, 0xDF00),
        ],
        [
            new SegmentMapping("CODE", "C_CODE"),
            new SegmentMapping("BSS", "C_BSS"),
            new SegmentMapping("DATA", "C_DATA"),
        ]);

    internal static string WriteDebugMap(AssemblyResult result)
    {
        var output = new System.Text.StringBuilder();
        foreach (ListingLine row in result.Listing)
        {
            Match match = DebugComment().Match(row.Source);
            if (match.Success)
            {
                output.Append(CultureInfo.InvariantCulture, $"{row.Address:X4} ");
                output.Append(match.Groups[1].Value.Length == 0 ? "?" : match.Groups[1].Value);
                output.Append(':');
                output.AppendLine(match.Groups[2].Value);
            }
        }

        return output.ToString();
    }

    private static ObjectModule AssembleModule(AssemblerTarget target, FileInfo input, string[] includePaths)
    {
        string? dir = Path.GetDirectoryName(input.FullName);
        Func<string, string?> reader = path =>
        {
            foreach (string baseDir in new[] { dir ?? ".", "." }.Concat(includePaths))
            {
                string full = Path.Combine(baseDir, path);
                if (File.Exists(full))
                {
                    return File.ReadAllText(full);
                }
            }

            return null;
        };

        if (input.Extension.Equals(".c", StringComparison.OrdinalIgnoreCase))
        {
            CheckedProgram program = TypeChecker.Check(Parser.Parse(File.ReadAllText(input.FullName), reader));
            string asm = Codegen.Emit(program, input.Name, objectMode: true);
            return AssembleObject(target, asm, input.Name, _ => null, includePaths);
        }

        return AssembleObject(target, File.ReadAllText(input.FullName), input.Name, reader, includePaths);
    }

    private static ObjectModule AssembleObject(
        AssemblerTarget target,
        string source,
        string name,
        Func<string, string?> reader,
        string[] includePaths)
    {
        using FileStream json = File.OpenRead(Path.Combine(AppContext.BaseDirectory, target.IsaFile));
        return new TwoPassAssembler(target.Load(json), target.DefaultSyntax)
            .AssembleObject(target.Name, source, name, reader, includePaths);
    }

    [GeneratedRegex(@";c:(?:([^:]+):)?(\d+)")]
    private static partial Regex DebugComment();
}
