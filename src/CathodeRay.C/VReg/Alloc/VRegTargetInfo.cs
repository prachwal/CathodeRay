namespace CathodeRay.C;

/// <summary>Opis celu dla alokatora VReg: rejestry fizyczne widoczne dla alokacji i strategia spillu
/// (na 8 bitach spill to statyczna komórka, nie slot ramki — <c>docs/vreg-design.md §D2</c>).</summary>
/// <param name="PhysRegs">Rejestry fizyczne (nazwy jak w selektorze celu).</param>
/// <param name="HasPairs">Cel ma pary rejestrów na wartości 2-bajtowe.</param>
/// <param name="MaxRegs">Ile wartości naraz warto trzymać w rejestrach (punkt startu, nie optimum).</param>
public sealed record VRegTargetInfo(IReadOnlyList<string> PhysRegs, bool HasPairs, int MaxRegs)
{
    /// <summary>Opis celu po nazwie z <c>cc --cpu</c> (tabela konfiguracji, nie gałęzie logiki).</summary>
    /// <param name="target">Cel kompilatora.</param>
    /// <returns>Opis dla alokatora.</returns>
    public static VRegTargetInfo For(ICTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.Name.ToLowerInvariant() switch
        {
            "stub" => new VRegTargetInfo(["a"], false, 1),
            "6502" or "65c02" => new VRegTargetInfo(["a", "x", "y"], false, 1),
            "z80" or "8080" => new VRegTargetInfo(["a", "hl", "bc", "de"], true, 3),
            "6800" => new VRegTargetInfo(["a", "b", "x"], false, 2),
            _ => new VRegTargetInfo([], false, 0),
        };
    }
}
