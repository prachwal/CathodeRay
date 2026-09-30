namespace CathodeRay.C;

/// <summary>Żywość rejestrów i pamięci w funkcji VReg: spłaszczenie bloków (etykieta → indeks pierwszej instrukcji),
/// sukcesory z <see cref="VReg.Jmp"/>/<see cref="VReg.Br"/>/<see cref="VReg.Ret"/> i iteracja wstecz do punktu stałego
/// (adaptacja <see cref="IrLiveness"/>). Zapis rejestru zawsze zabija cały rejestr (rejestry są całe z konstrukcji).</summary>
internal sealed class VRegLiveness
{
    private readonly List<VReg.Ins> _flat;

    private readonly HashSet<string>[] _in;

    private readonly HashSet<string>[] _out;

    private VRegLiveness(List<VReg.Ins> flat, HashSet<string>[] liveIn, HashSet<string>[] liveOut)
    {
        _flat = flat;
        _in = liveIn;
        _out = liveOut;
    }

    /// <summary>Liczba instrukcji po spłaszczeniu.</summary>
    public int Count => _flat.Count;

    /// <summary>Spłaszcza bloki funkcji do listy instrukcji (etykiety znikają, skoki wskazują indeksy).</summary>
    /// <param name="function">Funkcja.</param>
    /// <returns>Instrukcje i mapa etykieta → indeks (pusty blok wskazuje następną instrukcję albo koniec).</returns>
    public static (List<VReg.Ins> Flat, Dictionary<string, int> Labels) Flatten(VReg.Function function)
    {
        ArgumentNullException.ThrowIfNull(function);
        var flat = new List<VReg.Ins>();
        var labels = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (VReg.Block block in function.Blocks)
        {
            labels[block.Label] = flat.Count;
            flat.AddRange(block.Code);
        }

        return (flat, labels);
    }

    /// <summary>Liczy żywość dla funkcji.</summary>
    /// <param name="function">Funkcja.</param>
    /// <returns>Wynik analizy.</returns>
    public static VRegLiveness Of(VReg.Function function)
    {
        (List<VReg.Ins> flat, Dictionary<string, int> labels) = Flatten(function);
        int Target(string name) => labels.TryGetValue(name, out int index) ? index : throw new InvalidOperationException($"unknown label '{name}'.");
        var successors = new int[flat.Count][];
        var uses = new string[flat.Count][];
        var kills = new string?[flat.Count];
        for (int i = 0; i < flat.Count; i++)
        {
            VReg.Ins ins = flat[i];
            int[] next = (i + 1 < flat.Count) ? [i + 1] : [];
            successors[i] = ins switch
            {
                VReg.Jmp jump => [Target(jump.Target)],
                VReg.Br branch => [Target(branch.Then), Target(branch.Else)],
                VReg.Ret => [],
                _ => next,
            };
            uses[i] = [.. VRegFacts.Uses(ins).Select(static op => VRegFacts.Key(op)).OfType<string>()];
            kills[i] = VRegFacts.Def(ins) is { } def ? "r" + def.Id : null;
        }

        HashSet<string>[] liveIn = [.. flat.Select(static _ => new HashSet<string>(StringComparer.Ordinal))];
        HashSet<string>[] liveOut = [.. flat.Select(static _ => new HashSet<string>(StringComparer.Ordinal))];
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = flat.Count - 1; i >= 0; i--)
            {
                foreach (int successor in successors[i])
                {
                    liveOut[i].UnionWith(liveIn[successor]);
                }

                var fresh = new HashSet<string>(liveOut[i], StringComparer.Ordinal);
                if (kills[i] is { } killed)
                {
                    fresh.Remove(killed);
                }

                fresh.UnionWith(uses[i]);
                if (!fresh.SetEquals(liveIn[i]))
                {
                    liveIn[i] = fresh;
                    changed = true;
                }
            }
        }

        return new VRegLiveness(flat, liveIn, liveOut);
    }

    /// <summary>Instrukcja o indeksie płaskim.</summary>
    /// <param name="index">Indeks.</param>
    /// <returns>Instrukcja.</returns>
    public VReg.Ins At(int index) => _flat[index];

    /// <summary>Klucze żywe tuż przed instrukcją.</summary>
    /// <param name="index">Indeks instrukcji.</param>
    /// <returns>Klucze (<c>r{id}</c> albo <c>m{sym}</c>).</returns>
    public IReadOnlySet<string> LiveIn(int index) => _in[index];

    /// <summary>Klucze żywe tuż po instrukcji.</summary>
    /// <param name="index">Indeks instrukcji.</param>
    /// <returns>Klucze (<c>r{id}</c> albo <c>m{sym}</c>).</returns>
    public IReadOnlySet<string> LiveOut(int index) => _out[index];
}
