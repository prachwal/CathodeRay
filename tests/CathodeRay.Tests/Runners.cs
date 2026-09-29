namespace CathodeRay.Tests;

/// <summary>Rejestr runnerów testowych po nazwie celu.</summary>
public static class Runners
{
    public static bool Has(string target) => target is "stub" or "6502";

    public static ICpuRunner Create(string target) => target switch
    {
        "stub" => new StubRunner(),
        "6502" => new Mos6502Runner(),
        _ => throw new NotSupportedException($"no test runner for target '{target}'."),
    };
}
