namespace CathodeRay.Emulation;

/// <summary>Rejestr emulatorów 8-bitowych procesorów po nazwie celu kompilatora (bez stuba, który ma własny przebieg).</summary>
public static class CpuRunners
{
    /// <summary>Nazwy celów, dla których istnieje emulator.</summary>
    public static IReadOnlyList<string> Names { get; } = ["6502", "65c02", "nes", "6510", "z80", "8080", "6800", "6809"];

    /// <summary>Czy cel ma emulator.</summary>
    /// <param name="target">Nazwa celu.</param>
    /// <returns>Prawda, gdy <see cref="Create"/> go zwróci.</returns>
    public static bool Has(string target) => Names.Contains(target, StringComparer.OrdinalIgnoreCase);

    /// <summary>Tworzy emulator celu.</summary>
    /// <param name="target">Nazwa celu.</param>
    /// <returns>Świeży emulator z zerową pamięcią.</returns>
    /// <exception cref="NotSupportedException">Cel bez emulatora.</exception>
    public static ICpuRunner Create(string target) => target.ToLowerInvariant() switch
    {
        "6502" or "65c02" or "nes" or "6510" => new Mos6502Runner(),
        "z80" => new Z80Runner(),
        "8080" => new Z80Runner(intel8080: true),
        "6800" => new Mc6800Runner(),
        "6809" => new Mc6809Runner(),
        _ => throw new NotSupportedException($"no emulator for target '{target}'."),
    };
}
