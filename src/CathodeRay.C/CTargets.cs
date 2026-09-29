namespace CathodeRay.C;

/// <summary>Rejestr celów kompilatora (jak <c>AssemblerTargets</c>): fabryka po nazwie z <c>cc --cpu</c>.</summary>
public static class CTargets
{
    /// <summary>Zaimplementowane cele.</summary>
    public static IReadOnlyList<ICTarget> All { get; } = [new StubTarget(), new Mos6502Target(), new Mos6502Target(cmos: true), new Z80Target(), new Intel8080Target(), new M6800Target()];

    /// <summary>Cel domyślny (stub).</summary>
    public static ICTarget Default => All[0];

    /// <summary>Nazwy celów zapowiedzianych w planie 30, których jeszcze nie ma.</summary>
    public static IReadOnlyList<string> Planned { get; } = [];

    /// <summary>Szuka celu po nazwie.</summary>
    /// <param name="name">Nazwa (bez rozróżniania wielkości liter).</param>
    /// <returns>Cel albo <see langword="null"/>.</returns>
    public static ICTarget? Find(string name) =>
        All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
}
