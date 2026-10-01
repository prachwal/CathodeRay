namespace CathodeRay.C;

/// <summary>Rejestr modeli: jeden wpis na nazwę z <c>cc --cpu</c> (<c>nes</c>/<c>6510</c> jak <c>6502</c>).
/// <c>Scratch</c> to dawne <c>Z80Isa.Clobbers</c> (przeniesione do modelu); gdzie indziej całość, konserwatywnie.
/// <c>PrimEffects</c> wypełnione dla celów z <c>ByteIsa</c> (maski flag czytanych przez optymalizator:
/// N/S, Z, C/CY, V; H/AC/parzystość poza modelem — prymitywy ich nie czytają).</summary>
public static class CpuModels
{
    private static readonly Dictionary<string, CpuModel> Models = new(StringComparer.OrdinalIgnoreCase)
    {
        ["stub"] = new("stub", [new("a", 1, []), new("x", 1, [])], ScratchOf("a", "x"), ScratchOf("a", "x"), null, [], new Dictionary<string, FlagEffects>()),
        ["6502"] = new("6502", [new("a", 1, []), new("x", 1, []), new("y", 1, [])], ScratchOf("a", "x", "y"), ScratchOf("a", "x", "y"), null, [], Mos6502Effects()),
        ["65c02"] = new("65c02", [new("a", 1, []), new("x", 1, []), new("y", 1, [])], ScratchOf("a", "x", "y"), ScratchOf("a", "x", "y"), null, [], Mos6502Effects()),
        ["nes"] = new("nes", [new("a", 1, []), new("x", 1, []), new("y", 1, [])], ScratchOf("a", "x", "y"), ScratchOf("a", "x", "y"), null, [], Mos6502Effects()),
        ["6510"] = new("6510", [new("a", 1, []), new("x", 1, []), new("y", 1, [])], ScratchOf("a", "x", "y"), ScratchOf("a", "x", "y"), null, [], Mos6502Effects()),
        ["z80"] = new(
            "z80",
            [new("a", 1, []), new("b", 1, []), new("c", 1, []), new("d", 1, []), new("e", 1, []), new("h", 1, []), new("l", 1, []),
                new("bc", 2, ["b", "c"]), new("de", 2, ["d", "e"]), new("hl", 2, ["h", "l"])],
            ScratchOf("a", "b", "c", "d", "e", "h", "l"),
            ScratchOf("a", "h", "l"),
            "hl",
            [],
            Z80Effects()),
        ["8080"] = new(
            "8080",
            [new("a", 1, []), new("b", 1, []), new("c", 1, []), new("d", 1, []), new("e", 1, []), new("h", 1, []), new("l", 1, []),
                new("bc", 2, ["b", "c"]), new("de", 2, ["d", "e"]), new("hl", 2, ["h", "l"])],
            ScratchOf("a", "b", "c", "d", "e", "h", "l"),
            ScratchOf("a", "h", "l"),
            "hl",
            [],
            Intel8080Effects()),
        ["6800"] = new("6800", [new("a", 1, []), new("b", 1, []), new("x", 2, [])], ScratchOf("a", "b", "x"), ScratchOf("a", "b", "x"), null, [], M6800Effects()),
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

    private static Dictionary<string, FlagEffects> Mos6502Effects() => new()
    {
        ["LoadA"] = Fx([], ["A"], ["N", "Z"]),
        ["StoreA"] = Fx(["A"], [], []),
        ["Add"] = Fx(["A"], ["A"], ["N", "Z", "C", "V"]),
        ["Sub"] = Fx(["A"], ["A"], ["N", "Z", "C", "V"]),
        ["And"] = Fx(["A"], ["A"], ["N", "Z"]),
        ["Or"] = Fx(["A"], ["A"], ["N", "Z"]),
        ["Xor"] = Fx(["A"], ["A"], ["N", "Z"]),
        ["Cmp"] = Fx(["A"], [], ["N", "Z", "C"]),
        ["Shl"] = Fx(["A"], ["A"], ["N", "Z", "C"]),
        ["Shr"] = Fx(["A"], ["A"], ["N", "Z", "C"]),
    };

    private static Dictionary<string, FlagEffects> Z80Effects() => new()
    {
        ["LoadA"] = Fx([], ["A"], []),
        ["StoreA"] = Fx(["A"], [], []),
        ["Add"] = Fx(["A"], ["A"], ["S", "Z", "V", "C", "N"]),
        ["Sub"] = Fx(["A"], ["A"], ["S", "Z", "V", "C", "N"]),
        ["And"] = Fx(["A"], ["A"], ["S", "Z", "C", "N"]),
        ["Or"] = Fx(["A"], ["A"], ["S", "Z", "C", "N"]),
        ["Xor"] = Fx(["A"], ["A"], ["S", "Z", "C", "N"]),
        ["Cmp"] = Fx(["A"], [], ["S", "Z", "V", "C", "N"]),
        ["Shl"] = Fx(["A"], ["A"], ["S", "Z", "C", "N"]),
        ["Shr"] = Fx(["A"], ["A"], ["S", "Z", "C", "N"]),
    };

    private static Dictionary<string, FlagEffects> Intel8080Effects() => new()
    {
        ["LoadA"] = Fx([], ["A"], []),
        ["StoreA"] = Fx(["A"], [], []),
        ["Add"] = Fx(["A"], ["A"], ["S", "Z", "CY"]),
        ["Sub"] = Fx(["A"], ["A"], ["S", "Z", "CY"]),
        ["And"] = Fx(["A"], ["A"], ["S", "Z", "CY"]),
        ["Or"] = Fx(["A"], ["A"], ["S", "Z", "CY"]),
        ["Xor"] = Fx(["A"], ["A"], ["S", "Z", "CY"]),
        ["Cmp"] = Fx(["A"], [], ["S", "Z", "CY"]),
        ["Shl"] = Fx(["A"], ["A"], ["S", "Z", "CY"]),
        ["Shr"] = Fx(["A"], ["A"], ["S", "Z", "CY"]),
    };

    private static Dictionary<string, FlagEffects> M6800Effects() => new()
    {
        ["LoadA"] = Fx([], ["A"], ["N", "Z", "V"]),
        ["StoreA"] = Fx(["A"], [], []),
        ["Add"] = Fx(["A"], ["A"], ["N", "Z", "V", "C"]),
        ["Sub"] = Fx(["A"], ["A"], ["N", "Z", "V", "C"]),
        ["And"] = Fx(["A"], ["A"], ["N", "Z", "V"]),
        ["Or"] = Fx(["A"], ["A"], ["N", "Z", "V"]),
        ["Xor"] = Fx(["A"], ["A"], ["N", "Z", "V"]),
        ["Cmp"] = Fx(["A"], [], ["N", "Z", "V", "C"]),
        ["Shl"] = Fx(["A"], ["A"], ["N", "Z", "V", "C"]),
        ["Shr"] = Fx(["A"], ["A"], ["N", "Z", "V", "C"]),
    };

    private static FlagEffects Fx(string[] reads, string[] writes, string[] flags) =>
        new(new HashSet<string>(reads, StringComparer.Ordinal), new HashSet<string>(writes, StringComparer.Ordinal), new HashSet<string>(flags, StringComparer.Ordinal));

    private static HashSet<string> ScratchOf(params string[] registers) => new(registers, StringComparer.Ordinal);
}
