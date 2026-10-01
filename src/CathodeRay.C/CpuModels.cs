namespace CathodeRay.C;

/// <summary>Rejestr modeli: jeden wpis na nazwę z <c>cc --cpu</c> (<c>nes</c>/<c>6510</c> jak <c>6502</c>).</summary>
public static class CpuModels
{
    private static readonly Dictionary<string, CpuModel> Models = new(StringComparer.OrdinalIgnoreCase)
    {
        ["stub"] = new("stub", [new("a", 1, []), new("x", 1, [])], new HashSet<string>(["a", "x"], StringComparer.Ordinal), null, [], new Dictionary<string, FlagEffects>()),
        ["6502"] = new("6502", [new("a", 1, []), new("x", 1, []), new("y", 1, [])], new HashSet<string>(["a", "x", "y"], StringComparer.Ordinal), null, [], new Dictionary<string, FlagEffects>()),
        ["65c02"] = new("65c02", [new("a", 1, []), new("x", 1, []), new("y", 1, [])], new HashSet<string>(["a", "x", "y"], StringComparer.Ordinal), null, [], new Dictionary<string, FlagEffects>()),
        ["nes"] = new("nes", [new("a", 1, []), new("x", 1, []), new("y", 1, [])], new HashSet<string>(["a", "x", "y"], StringComparer.Ordinal), null, [], new Dictionary<string, FlagEffects>()),
        ["6510"] = new("6510", [new("a", 1, []), new("x", 1, []), new("y", 1, [])], new HashSet<string>(["a", "x", "y"], StringComparer.Ordinal), null, [], new Dictionary<string, FlagEffects>()),
        ["z80"] = new(
            "z80",
            [new("a", 1, []), new("b", 1, []), new("c", 1, []), new("d", 1, []), new("e", 1, []), new("h", 1, []), new("l", 1, []),
                new("bc", 2, ["b", "c"]), new("de", 2, ["d", "e"]), new("hl", 2, ["h", "l"])],
            new HashSet<string>(["a", "b", "c", "d", "e", "h", "l"], StringComparer.Ordinal),
            "hl",
            [],
            new Dictionary<string, FlagEffects>()),
        ["8080"] = new(
            "8080",
            [new("a", 1, []), new("b", 1, []), new("c", 1, []), new("d", 1, []), new("e", 1, []), new("h", 1, []), new("l", 1, []),
                new("bc", 2, ["b", "c"]), new("de", 2, ["d", "e"]), new("hl", 2, ["h", "l"])],
            new HashSet<string>(["a", "b", "c", "d", "e", "h", "l"], StringComparer.Ordinal),
            "hl",
            [],
            new Dictionary<string, FlagEffects>()),
        ["6800"] = new("6800", [new("a", 1, []), new("b", 1, []), new("x", 2, [])], new HashSet<string>(["a", "b", "x"], StringComparer.Ordinal), null, [], new Dictionary<string, FlagEffects>()),
    };

    /// <summary>Model po nazwie celu.</summary>
    /// <param name="cpu">Nazwa z <c>cc --cpu</c>.</param>
    /// <returns>Model.</returns>
    public static CpuModel For(string cpu) =>
        Models.TryGetValue(cpu, out CpuModel? model) ? model : throw new ArgumentException($"Brak modelu CPU '{cpu}'.", nameof(cpu));

    /// <summary>Model celu kompilatora.</summary>
    /// <param name="target">Cel.</param>
    /// <returns>Model.</returns>
    public static CpuModel For(ICTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return For(target.Name);
    }
}
