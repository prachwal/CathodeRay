namespace CathodeRay.C;

/// <summary>Potok wirtualnych rejestrów: program po kontroli typów idzie przez Cell IR
/// (<see cref="Codegen.Lower"/>), podniesienie do rejestrów (<see cref="VRegLift"/>), przebiegi VReg,
/// opadnięcie do komórek (<see cref="VRegToCell"/>) i istniejący cel. ABI, ramki i selektor nie zmieniają się.</summary>
public static class VRegPipeline
{
    /// <summary>Obniża program do Cell IR ścieżką VReg (lift, przebiegi, opadnięcie).</summary>
    /// <param name="program">Program po kontroli typów.</param>
    /// <param name="fileName">Nazwa pliku C do adnotacji <c>;c:</c> (null = sama linia).</param>
    /// <param name="objectMode">Tryb obiektowy (linker).</param>
    /// <param name="stackLimit">Rozmiar stosu do kontroli głębokości wołań.</param>
    /// <param name="byteOrder">Kolejność bajtów słów.</param>
    /// <param name="callSaveBytes">Bajty odkładane przez cel wokół wołania.</param>
    /// <returns>Moduł Cell IR.</returns>
    public static Ir.Module Lower(CheckedProgram program, string? fileName = null, bool objectMode = false, int? stackLimit = 256, TargetByteOrder byteOrder = TargetByteOrder.Little, int callSaveBytes = 0)
    {
        ArgumentNullException.ThrowIfNull(program);
        Ir.Module cell = Codegen.Lower(program, fileName, objectMode, stackLimit, byteOrder, callSaveBytes);
        VReg.Module vreg = VRegLift.Run(cell);
        var functions = new List<VReg.Function>(vreg.Functions.Count);
        foreach (VReg.Function function in vreg.Functions)
        {
            functions.Add(VRegPasses.Run(function, vreg.Volatile));
        }

        return VRegToCell.Run(vreg with { Functions = functions });
    }

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
        return target.Emit(Lower(program, fileName, objectMode, target.StackLimit, target.ByteOrder, target.CallSaveBytes), optimize);
    }
}
