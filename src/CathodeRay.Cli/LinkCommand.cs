using System.CommandLine;
using System.Globalization;
using CathodeRay.Assembler;
using CathodeRay.Assembler.Link;

namespace CathodeRay.Cli;

/// <summary>Komenda <c>link</c>: składa moduły obiektu (<c>--format obj</c>) według mapy pamięci.</summary>
internal static class LinkCommand
{
    /// <summary>Buduje komendę <c>link</c>.</summary>
    /// <returns>Komenda.</returns>
    public static Command Create()
    {
        var objects = new Argument<FileInfo[]>("objects") { Description = "Moduły obiektu do połączenia." };
        var map = new Option<FileInfo>("--map", "-m") { Description = "Mapa pamięci (.cfg z MEMORY/SEGMENTS).", Required = true };
        map.AcceptExistingOnly();
        var output = new Option<FileInfo?>("--output", "-o") { Description = "Plik wynikowy (domyślnie <pierwszy>.bin/.hex)." };
        var format = new Option<string>("--format", "-f") { Description = "Format wyjścia: bin (domyślnie) albo hex (Intel HEX)." };
        format.AcceptOnlyFromAmong(["bin", "hex"]);

        var command = new Command("link", "Łączy moduły obiektu w plik binarny.") { objects, map, output, format };
        command.SetAction(parse =>
        {
            TextWriter error = parse.InvocationConfiguration.Error;
            FileInfo[] inputs = parse.GetRequiredValue(objects);
            foreach (FileInfo input in inputs)
            {
                if (!input.Exists)
                {
                    error.WriteLine($"File does not exist: {input.FullName}.");
                    return 1;
                }
            }

            var modules = new List<(string File, ObjectModule Module)>();
            foreach (FileInfo input in inputs)
            {
                try
                {
                    modules.Add((input.Name, ObjectModule.FromJson(File.ReadAllText(input.FullName))));
                }
                catch (InvalidDataException e)
                {
                    error.WriteLine($"{input.Name}: {e.Message}");
                    return 1;
                }
            }

            LinkerConfig config;
            try
            {
                config = LinkerConfig.Parse(File.ReadAllText(parse.GetRequiredValue(map).FullName));
            }
            catch (LinkerException e)
            {
                error.WriteLine($"{parse.GetRequiredValue(map).Name}: {e.Message}");
                return 1;
            }

            AssemblyResult result;
            try
            {
                result = Linker.Link(modules, config);
            }
            catch (LinkerException e)
            {
                error.WriteLine($"link: {e.Message}");
                return 1;
            }

            string kind = parse.GetValue(format) ?? "bin";
            FileInfo dst = parse.GetValue(output)
                ?? new FileInfo(Path.ChangeExtension(inputs[0].FullName, kind == "hex" ? ".hex" : ".bin"));
            if (kind == "hex")
            {
                using StreamWriter writer = File.CreateText(dst.FullName);
                result.WriteIntelHex(writer);
            }
            else
            {
                File.WriteAllBytes(dst.FullName, result.Image);
            }

            parse.InvocationConfiguration.Output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{dst.Name} ({result.Image.Length} B, load ${result.Origin:X4}, {modules.Count} modules)"));
            return 0;
        });
        return command;
    }
}
