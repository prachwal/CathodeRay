namespace CathodeRay.Tests;

/// <summary>Rejestr runnerów testowych po nazwie celu.</summary>
public static class Runners
{
    public static bool Has(string target) => target is "stub" or "6502" or "65c02" or "nes" or "6510" or "z80" or "8080" or "6800";

    public static ICpuRunner Create(string target) => target switch
    {
        "stub" => new StubRunner(),
        "6502" or "65c02" or "nes" or "6510" => new Mos6502Runner(),
        "z80" => new Z80Runner(),
        "8080" => new Z80Runner(intel8080: true),
        "6800" => new Mc6800Runner(),
        _ => throw new NotSupportedException($"no test runner for target '{target}'."),
    };
}
