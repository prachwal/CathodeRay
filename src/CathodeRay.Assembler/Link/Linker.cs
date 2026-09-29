namespace CathodeRay.Assembler.Link;

/// <summary>Linker modułów obiektu: składa segmenty z modułów (kolejność CLI),
/// liczy adresy z konfiguracji i aplikuje relokacje. Endianness: little-endian
/// (wszystkie wspierane CPU).</summary>
public static class Linker
{
    /// <summary>Składa moduły w obraz.</summary>
    /// <param name="modules">Moduły z nazwami plików (do komunikatów).</param>
    /// <param name="config">Konfiguracja pamięci.</param>
    /// <returns>Wynik jak z asemblera (obraz, symbole, segmenty; listing pusty).</returns>
    /// <exception cref="LinkerException">Błąd linkowania.</exception>
    public static AssemblyResult Link(IReadOnlyList<(string File, ObjectModule Module)> modules, LinkerConfig config)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(config);
        if (modules.Count == 0)
        {
            throw new LinkerException("No object modules.");
        }

        string cpu = modules[0].Module.Cpu;
        Isa.Endianness endianness = AssemblerTargets.Find(cpu)?.Endianness ?? Isa.Endianness.Little;
        if (modules.Any(m => !string.Equals(m.Module.Cpu, cpu, StringComparison.OrdinalIgnoreCase)))
        {
            throw new LinkerException("All modules must target the same CPU.");
        }

        var areas = config.Areas.ToDictionary(static a => a.Name, StringComparer.OrdinalIgnoreCase);
        var cursors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var chunks = new Dictionary<(int Module, string Segment), int>();
        var spans = new List<SegmentSpan>();
        var synthetic = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (SegmentMapping mapping in config.Segments)
        {
            if (!areas.TryGetValue(mapping.Load, out MemoryArea? area))
            {
                throw new LinkerException($"Segment '{mapping.Name}' loads unknown area '{mapping.Load}'.");
            }

            cursors.TryAdd(area.Name, area.Start);
            string lower = mapping.Name.ToLowerInvariant();
            synthetic[$"__{lower}_start"] = cursors[area.Name];
            for (int m = 0; m < modules.Count; m++)
            {
                foreach (ObjectSegment segment in modules[m].Module.Segments)
                {
                    if (!string.Equals(segment.Name, mapping.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int address = cursors[area.Name];
                    if (address + segment.Length > area.Start + area.Size)
                    {
                        throw new LinkerException($"Segment '{segment.Name}' from {modules[m].File} overflows area '{area.Name}'.");
                    }

                    chunks[(m, segment.Name)] = address;
                    cursors[area.Name] = address + segment.Length;
                    spans.Add(new SegmentSpan(segment.Name, address, address + segment.Length, segment.Bss));
                }
            }

            synthetic[$"__{lower}_end"] = cursors[area.Name];
        }

        for (int m = 0; m < modules.Count; m++)
        {
            foreach (ObjectSegment segment in modules[m].Module.Segments)
            {
                if (!config.Segments.Any(s => string.Equals(s.Name, segment.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new LinkerException($"Segment '{segment.Name}' from {modules[m].File} is not in config.");
                }
            }
        }

        var locals = new List<Dictionary<string, int>>();
        var globals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var globalFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int m = 0; m < modules.Count; m++)
        {
            var own = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectSymbol symbol in modules[m].Module.Symbols)
            {
                if (!chunks.TryGetValue((m, symbol.Segment), out int baseAddress))
                {
                    continue;
                }

                own[symbol.Name] = baseAddress + symbol.Offset;
                if (symbol.Global)
                {
                    if (globals.ContainsKey(symbol.Name))
                    {
                        throw new LinkerException($"Duplicate symbol '{symbol.Name}' ({globalFiles[symbol.Name]} and {modules[m].File}).");
                    }

                    globals[symbol.Name] = baseAddress + symbol.Offset;
                    globalFiles[symbol.Name] = modules[m].File;
                }
            }

            locals.Add(own);
        }

        // Symbole syntetyczne __<segment>_start/_end każdego segmentu z konfiguracji (także pustego),
        // np. __bss_end i __init_start dla crt0; symbol zdefiniowany w module ma pierwszeństwo.
        foreach ((string name, int address) in synthetic)
        {
            globals.TryAdd(name, address);
        }

        var image = new Dictionary<int, byte>();
        for (int m = 0; m < modules.Count; m++)
        {
            foreach (ObjectSegment segment in modules[m].Module.Segments)
            {
                if (segment.Bss)
                {
                    continue;
                }

                int baseAddress = chunks[(m, segment.Name)];
                for (int i = 0; i < segment.Data.Length; i++)
                {
                    if (!image.TryAdd(baseAddress + i, segment.Data[i]))
                    {
                        throw new LinkerException($"Overlapping output at ${baseAddress + i:X4}.");
                    }
                }
            }
        }

        for (int m = 0; m < modules.Count; m++)
        {
            foreach (Relocation reloc in modules[m].Module.Relocations)
            {
                if (!chunks.TryGetValue((m, reloc.Segment), out int baseAddress))
                {
                    throw new LinkerException($"Relocation in unknown segment '{reloc.Segment}' ({modules[m].File}).");
                }

                if (!locals[m].TryGetValue(reloc.Symbol, out int target)
                    && !globals.TryGetValue(reloc.Symbol, out target))
                {
                    throw new LinkerException($"Undefined symbol '{reloc.Symbol}' (imported by {modules[m].File}).");
                }

                Apply(image, modules[m].File, reloc, baseAddress + reloc.Offset, target + reloc.Addend, endianness);
            }
        }

        List<ListingLine> listing = MergeListing(modules, chunks);
        if (image.Count == 0)
        {
            return new AssemblyResult(0, [], globals, listing, spans);
        }

        int low = image.Keys.Min();
        int high = image.Keys.Max();
        byte[] flat = new byte[high - low + 1];
        foreach ((int address, byte value) in image)
        {
            flat[address - low] = value;
        }

        return new AssemblyResult(low, flat, globals, listing, spans);
    }

    private static List<ListingLine> MergeListing(
        IReadOnlyList<(string File, ObjectModule Module)> modules,
        Dictionary<(int Module, string Segment), int> chunks)
    {
        var patched = new HashSet<(int Module, string Segment, int Offset)>();
        for (int m = 0; m < modules.Count; m++)
        {
            foreach (Relocation reloc in modules[m].Module.Relocations)
            {
                int width = reloc.Kind == RelocKind.Abs16 ? 2 : 1;
                for (int i = 0; i < width; i++)
                {
                    patched.Add((m, reloc.Segment, reloc.Offset + i));
                }
            }
        }

        var rows = new List<(int Address, int Order, ListingLine Row)>();
        int order = 0;
        for (int m = 0; m < modules.Count; m++)
        {
            foreach (ObjectSegment segment in modules[m].Module.Segments)
            {
                if (segment.Lines is null || !chunks.TryGetValue((m, segment.Name), out int baseAddress))
                {
                    continue;
                }

                foreach (ObjectLine row in segment.Lines)
                {
                    byte[] bytes = row.Bytes;
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        if (patched.Contains((m, segment.Name, row.Offset + i)))
                        {
                            bytes = [];
                            break;
                        }
                    }

                    rows.Add((baseAddress + row.Offset, order++, new ListingLine(row.Line, baseAddress + row.Offset, bytes, row.Text, row.File, segment.Name)));
                }
            }
        }

        return [.. rows.OrderBy(static r => r.Address).ThenBy(static r => r.Order).Select(static r => r.Row)];
    }

    private static void Apply(Dictionary<int, byte> image, string file, Relocation reloc, int position, int value, Isa.Endianness endianness)
    {
        switch (reloc.Kind)
        {
            case RelocKind.Abs8 when value is >= 0 and <= byte.MaxValue:
            case RelocKind.Disp8 when value is >= sbyte.MinValue and <= sbyte.MaxValue:
                image[position] = (byte)value;
                break;
            case RelocKind.Abs16 when value is >= 0 and <= ushort.MaxValue:
                image[position] = endianness == Isa.Endianness.Little ? (byte)value : (byte)(value >> 8);
                image[position + 1] = endianness == Isa.Endianness.Little ? (byte)(value >> 8) : (byte)value;
                break;
            case RelocKind.Lo8 when value is >= 0 and <= ushort.MaxValue:
                image[position] = (byte)value;
                break;
            case RelocKind.Hi8 when value is >= 0 and <= ushort.MaxValue:
                image[position] = (byte)(value >> 8);
                break;
            case RelocKind.Rel8:
                int offset = value - (position + 1);
                if (offset is >= sbyte.MinValue and <= sbyte.MaxValue)
                {
                    image[position] = (byte)offset;
                    break;
                }

                throw new LinkerException($"Branch in {file} to '{reloc.Symbol}' out of range ({offset} bytes).");
            default:
                throw new LinkerException($"Value {value} for '{reloc.Symbol}' in {file} out of range for {reloc.Kind}.");
        }
    }
}
