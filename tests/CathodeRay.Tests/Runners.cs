namespace CathodeRay.Tests;

/// <summary>Rejestr runnerów testowych po nazwie celu.</summary>
public static class Runners
{
    public static bool Has(string target) => target is "stub";

    public static ICpuRunner Create(string target) => target switch
    {
        "stub" => new StubRunner(),
        _ => throw new NotSupportedException($"no test runner for target '{target}'."),
    };
}
