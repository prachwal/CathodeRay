namespace CathodeRay.C;

/// <summary>Alokator zachowawczy (cel zgodności): spilluje wszystko do komórek, bieżąca wartość płynie przez akumulator.
/// Ścieżka VReg ma generować kod równoważny Cell, co daje test różnicowy za darmo; właściwy przydział robi
/// <see cref="RegisterAllocator"/> po <see cref="VRegToCell"/>.</summary>
public sealed class AccumulatorAllocator : IVRegAllocator
{
    /// <inheritdoc/>
    public string Name => "accumulator";

    /// <inheritdoc/>
    public IReadOnlyDictionary<int, string?> Allocate(VReg.Function function, VRegTargetInfo target)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(target);
        var ids = new HashSet<int>();
        foreach (VReg.Reg param in function.Params)
        {
            ids.Add(param.Id);
        }

        foreach (VReg.Block block in function.Blocks)
        {
            foreach (VReg.Ins ins in block.Code)
            {
                if (VRegFacts.Def(ins) is { } defined)
                {
                    ids.Add(defined.Id);
                }

                foreach (VReg.Op op in VRegFacts.Uses(ins))
                {
                    if (op is VReg.Reg reg)
                    {
                        ids.Add(reg.Id);
                    }
                }
            }
        }

        return ids.ToDictionary(static id => id, static _ => (string?)null);
    }
}
