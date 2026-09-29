namespace CathodeRay.Tests;

/// <summary>Rejestr runnerów testowych po nazwie celu.</summary>
public static class Runners
{
    public static bool Has(string target) => target is "stub" or "6502" or "65c02" or "z80" or "8080";

    public static ICpuRunner Create(string target) => target switch
    {
        "stub" => new StubRunner(),
        "6502" or "65c02" => new Mos6502Runner(),
        "z80" => new Z80Runner(),
        "8080" => new Z80Runner(intel8080: true),
        _ => throw new NotSupportedException($"no test runner for target '{target}'."),
    };
}
