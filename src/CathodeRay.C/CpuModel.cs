namespace CathodeRay.C;

/// <summary>Jawny kontrakt rejestrowy CPU dla optymalizatora: które rejestry istnieją, jak się aliasują,
/// co wołanie niszczy, gdzie wraca wynik i dokąd idą argumenty. Dziś opisuje ABI v1
/// (<see cref="ArgRegs"/> puste — argumenty przez <c>cc_argN</c>); Opt 1 (ABI v2) wypełni to pole
/// bez rewolucji w selektorze. Rejestry alternatywne Z80 (AF', Rams, IX/IY) poza modelem —
/// generator ich nie używa.</summary>
/// <param name="Cpu">Nazwa celu z <c>cc --cpu</c>.</param>
/// <param name="Registers">Wszystkie rejestry danych (8- i 16-bitowe).</param>
/// <param name="ClobberedByCall">Rejestry, których wołanie może nie zachować (codegen nic nie zakłada).</param>
/// <param name="ResultReg">Rejestr wyniku (null, gdy wynik wraca przez <c>cc_ret</c>).</param>
/// <param name="ArgRegs">Rejestry argumentów (puste w ABI v1).</param>
/// <param name="PrimEffects">Efekty prymitywów (zadanie 4).</param>
public sealed record CpuModel(
    string Cpu,
    IReadOnlyList<CpuRegister> Registers,
    IReadOnlySet<string> ClobberedByCall,
    string? ResultReg,
    IReadOnlyList<string> ArgRegs,
    IReadOnlyDictionary<string, FlagEffects> PrimEffects)
{
    /// <summary>Czy rejestr istnieje w modelu.</summary>
    /// <param name="name">Nazwa.</param>
    /// <returns>Czy znany.</returns>
    public bool HasRegister(string name) =>
        Registers.Any(r => r.Name == name);

    /// <summary>Rejestr po nazwie.</summary>
    /// <param name="name">Nazwa.</param>
    /// <returns>Rejestr albo <see langword="null"/>.</returns>
    public CpuRegister? Find(string name) =>
        Registers.FirstOrDefault(r => r.Name == name);

    /// <summary>Wszystkie nazwy tych samych bitów: rejestr, jego części i całości, do których należy.</summary>
    /// <param name="name">Nazwa.</param>
    /// <returns>Nazwy (zawsze co najmniej on sam).</returns>
    public IReadOnlyList<string> AliasesOf(string name)
    {
        var result = new List<string> { name };
        foreach (CpuRegister register in Registers)
        {
            if (register.Parts.Contains(name) && !result.Contains(register.Name))
            {
                result.Add(register.Name);
            }
        }

        if (Find(name) is { } found)
        {
            foreach (string part in found.Parts)
            {
                if (!result.Contains(part))
                {
                    result.Add(part);
                }
            }
        }

        return result;
    }
}
