namespace CathodeRay.C;

/// <summary>Liniowy alokator zachłanny: przedziały życia z <see cref="VRegLiveness"/>, kolejność po początku,
/// do <see cref="VRegTargetInfo.MaxRegs"/> slotów (aktywne wygasają, gdy koniec &lt; początek).
/// Rejestry szersze niż 2 B bez par w celu spillują. Wynik jest doradczy, dopóki emisja idzie adapterem
/// do Cell (przydział właściwy robi <see cref="RegisterAllocator"/>); służy pomiarom i przyszłemu emiterowi.</summary>
public sealed class LinearScanAllocator : IVRegAllocator
{
    /// <inheritdoc/>
    public string Name => "linear-scan";

    /// <inheritdoc/>
    public IReadOnlyDictionary<int, string?> Allocate(VReg.Function function, VRegTargetInfo target)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(target);
        VRegLiveness live = VRegLiveness.Of(function);
        var starts = new Dictionary<int, int>();
        var ends = new Dictionary<int, int>();
        var widths = new Dictionary<int, int>();
        for (int i = 0; i < live.Count; i++)
        {
            VReg.Ins ins = live.At(i);
            if (VRegFacts.Def(ins) is { } defined)
            {
                starts.TryAdd(defined.Id, i);
                ends[defined.Id] = i;
                widths.TryAdd(defined.Id, defined.W);
            }

            foreach (VReg.Op op in VRegFacts.Uses(ins))
            {
                if (op is VReg.Reg reg)
                {
                    starts.TryAdd(reg.Id, i);
                    ends[reg.Id] = i;
                    widths.TryAdd(reg.Id, reg.W);
                }
            }

            foreach (string key in live.LiveOut(i))
            {
                if (key.StartsWith('r') && int.TryParse(key[1..], out int id))
                {
                    starts.TryAdd(id, i);
                    ends[id] = i;
                }
            }
        }

        foreach (VReg.Reg param in function.Params)
        {
            starts.TryAdd(param.Id, 0);
            ends.TryAdd(param.Id, 0);
            widths.TryAdd(param.Id, param.W);
        }

        var order = starts.Keys.OrderBy(id => starts[id]).ThenBy(static id => id).ToList();
        var active = new List<(int End, int Slot)>();
        var slotOf = new Dictionary<int, int>();
        foreach (int id in order)
        {
            active.RemoveAll(a => a.End < starts[id]);
            var used = new HashSet<int>(active.Select(static a => a.Slot));
            int slot = -1;
            for (int s = 0; s < target.MaxRegs; s++)
            {
                if (used.Add(s))
                {
                    slot = s;
                    break;
                }
            }

            if (slot >= 0)
            {
                slotOf[id] = slot;
                active.Add((ends[id], slot));
            }
        }

        var result = new Dictionary<int, string?>();
        foreach (int id in order)
        {
            bool wide = widths.TryGetValue(id, out int w) && w > 2 && !target.HasPairs;
            result[id] = (slotOf.TryGetValue(id, out int slot) && !wide && target.PhysRegs.Count > 0)
                ? target.PhysRegs[slot % target.PhysRegs.Count]
                : null;
        }

        return result;
    }
}
