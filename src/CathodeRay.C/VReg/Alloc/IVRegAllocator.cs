namespace CathodeRay.C;

/// <summary>Alokator rejestrów VReg: rejestr wirtualny → rejestr fizyczny (nazwa) albo spill (<see langword="null"/>).
/// Właściwy przydział komórek do rejestrów robi istniejący <see cref="RegisterAllocator"/> po opadnięciu do Cell IR;
/// alokator VReg decyduje tylko, które wartości w ogóle kandydują (przez scalanie kopii i eliminację).</summary>
public interface IVRegAllocator
{
    /// <summary>Nazwa do <c>--regalloc</c> (przyszłe) i raportów.</summary>
    string Name { get; }

    /// <summary>Przydziela rejestry funkcji.</summary>
    /// <param name="function">Funkcja po przebiegach VReg.</param>
    /// <param name="target">Opis celu.</param>
    /// <returns>Rejestr wirtualny → rejestr fizyczny albo <see langword="null"/> (spill do komórki).</returns>
    IReadOnlyDictionary<int, string?> Allocate(VReg.Function function, VRegTargetInfo target);
}
