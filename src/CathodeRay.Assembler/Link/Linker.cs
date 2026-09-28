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
        if (modules.Any(m => !string.Equals(m.Module.Cpu, cpu, StringComparison.OrdinalIgnoreCase)))
        {
            throw new LinkerException("All modules must target the same CPU.");
        }

        var areas = config.Areas.ToDictionary(static a => a.Name, StringComparer.OrdinalIgnoreCase);
        var mappings = config.Segments.ToDictionary(static s => s.Name, static s => s, StringComparer.OrdinalIgnoreCase);
        var cursors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var chunks = new Dictionary<(int Module, string Segment), int>();
        var spans = new List<SegmentSpan>();
        for (int m = 0; m < modules.Count; m++)
        {
            foreach (ObjectSegment segment in modules[m].Module.Segments)
            {
                if (!mappings.TryGetValue(segment.Name, out SegmentMapping? mapping))
                {
                    throw new LinkerException($"Segment '{segment.Name}' from {modules[m].File} is not in config.");
                }

                if (!areas.TryGetValue(mapping.Load, out MemoryArea? area))
                {
                    throw new LinkerException($"Segment '{segment.Name}' loads unknown area '{mapping.Load}'.");
                }

                cursors.TryAdd(area.Name, area.Start);
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

                Apply(image, modules[m].File, reloc, baseAddress + reloc.Offset, target + reloc.Addend);
            }
        }

        if (image.Count == 0)
        {
            return new AssemblyResult(0, [], globals, [], spans);
        }

        int low = image.Keys.Min();
        int high = image.Keys.Max();
        byte[] flat = new byte[high - low + 1];
        foreach ((int address, byte value) in image)
        {
            flat[address - low] = value;
        }

        return new AssemblyResult(low, flat, globals, [], spans);
    }

    private static void Apply(Dictionary<int, byte> image, string file, Relocation reloc, int position, int value)
    {
        switch (reloc.Kind)
        {
            case RelocKind.Abs8 when value is >= 0 and <= byte.MaxValue:
            case RelocKind.Disp8 when value is >= sbyte.MinValue and <= sbyte.MaxValue:
                image[position] = (byte)value;
                break;
            case RelocKind.Abs16 when value is >= 0 and <= ushort.MaxValue:
                image[position] = (byte)value;
                image[position + 1] = (byte)(value >> 8);
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
