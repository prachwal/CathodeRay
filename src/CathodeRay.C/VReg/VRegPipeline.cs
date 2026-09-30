namespace CathodeRay.C;

/// <summary>Potok wirtualnych rejestrów: program po kontroli typów idzie przez Cell IR
/// (<see cref="Codegen.Lower"/>), podniesienie do rejestrów (<see cref="VRegLift"/>), przebiegi VReg,
/// opadnięcie do komórek (<see cref="VRegToCell"/>) i istniejący cel. ABI, ramki i selektor nie zmieniają się.</summary>
public static class VRegPipeline
{
    /// <summary>Generuje tekst asemblera dla wybranego celu ścieżką VReg.</summary>
    /// <param name="program">Program po kontroli typów.</param>
    /// <param name="target">Cel (drukuje kod pośredni jako asembler).</param>
    /// <param name="fileName">Nazwa pliku C do adnotacji <c>;c:</c> (null = sama linia).</param>
    /// <param name="objectMode">Tryb obiektowy (linker).</param>
    /// <param name="optimize">Optymalizacje celu.</param>
    /// <returns>Źródło dla asemblera celu.</returns>
    public static string Emit(CheckedProgram program, ICTarget target, string? fileName = null, bool objectMode = false, bool optimize = true)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(target);
        Ir.Module cell = Codegen.Lower(program, fileName, objectMode, target.StackLimit, target.ByteOrder, target.CallSaveBytes);
        VReg.Module vreg = VRegLift.Run(cell);
        return target.Emit(VRegToCell.Run(vreg), optimize);
    }
}
