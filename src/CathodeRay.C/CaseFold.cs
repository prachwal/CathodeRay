using System.Text;

namespace CathodeRay.C;

/// <summary>Asemblery i linker nie rozróżniają wielkości liter w symbolach, a C tak: przebieg IR → IR zamienia każdą nazwę z wielką
/// literą na małe litery plus maskę pozycji wielkich (<c>Foo</c> → <c>foo__c1</c>), więc <c>foo</c>, <c>Foo</c> i <c>FOO</c> pozostają
/// różne. Odwzorowanie jest deterministyczne, więc moduły linkowane osobno zgadzają się co do nazw.</summary>
internal static class CaseFold
{
    /// <summary>Bezpieczna nazwa symbolu.</summary>
    /// <param name="name">Nazwa z kodu pośredniego.</param>
    /// <returns>Ta sama nazwa, gdy nie ma wielkich liter, inaczej małe litery z maską.</returns>
    public static string Name(string name)
    {
        if (!name.Any(char.IsAsciiLetterUpper))
        {
            return name;
        }

        var mask = new StringBuilder();
        for (int i = 0; i < name.Length; i += 4)
        {
            int digit = 0;
            for (int k = 0; k < 4 && i + k < name.Length; k++)
            {
                digit |= char.IsAsciiLetterUpper(name[i + k]) ? 1 << k : 0;
            }

            mask.Append("0123456789abcdef"[digit]);
        }

        return name.ToLowerInvariant() + "__c" + mask;
    }

    /// <summary>Zamienia nazwy symboli w całym module.</summary>
    /// <param name="module">Moduł IR.</param>
    /// <returns>Moduł z bezpiecznymi nazwami.</returns>
    public static Ir.Module Apply(Ir.Module module)
    {
        ArgumentNullException.ThrowIfNull(module);
        return module with
        {
            Functions = [.. module.Functions.Select(Function)],
            Data = [.. module.Data.Select(Data)],
            ExternFunctions = [.. module.ExternFunctions.Select(Name)],
            ExternCells = [.. module.ExternCells.Select(Name)],
        };
    }

    private static Ir.Function Function(Ir.Function function) => function with
    {
        Name = Name(function.Name),
        Params = [.. function.Params.Select(Cell)],
        Saved = [.. function.Saved.Select(static o => o with { Sym = Name(o.Sym) })],
        Body = [.. function.Body.Select(Instruction)],
    };

    private static Ir.Data Data(Ir.Data data) => data with
    {
        Sym = Name(data.Sym),
        Init = data.Init?.Select(static p => p is Ir.SymWord w ? w with { Sym = Name(w.Sym) } : p).ToList(),
    };

    private static Ir.Cell Cell(Ir.Cell cell) => cell with { Sym = Name(cell.Sym) };

    private static Ir.Op Operand(Ir.Op op) => op switch
    {
        Ir.Cell cell => Cell(cell),
        Ir.AddrOf address => address with { Sym = Name(address.Sym) },
        _ => op,
    };

    private static Ir.Ins Instruction(Ir.Ins ins) => ins switch
    {
        Ir.Mov mov => new Ir.Mov(Cell(mov.Dst), Operand(mov.Src)),
        Ir.Bin bin => new Ir.Bin(bin.Kind, Cell(bin.Dst), Operand(bin.A), Operand(bin.B)),
        Ir.Un un => new Ir.Un(un.Kind, Cell(un.Dst), Operand(un.A)),
        Ir.Load load => new Ir.Load(Cell(load.Dst), Operand(load.Ptr), load.Off, load.Bytes),
        Ir.Store store => new Ir.Store(Operand(store.Ptr), store.Off, Operand(store.Value), store.Bytes),
        Ir.CopyBlock copy => new Ir.CopyBlock(Operand(copy.Dst), Operand(copy.Src), copy.Size),
        Ir.Fill fill => fill with { Dst = Operand(fill.Dst) },
        Ir.BrCmp branch => new Ir.BrCmp(branch.C, Operand(branch.A), Operand(branch.B), Name(branch.Target)),
        Ir.Jmp jump => new Ir.Jmp(Name(jump.Target)),
        Ir.Label label => new Ir.Label(Name(label.Name)),
        Ir.Call call => new Ir.Call(call.Direct is null ? null : Name(call.Direct), call.Indirect is null ? null : Cell(call.Indirect), [.. call.Args.Select(Operand)], call.ParamWidths, call.Result is null ? null : Cell(call.Result)),
        Ir.Ret ret => ret with { Value = ret.Value is null ? null : Operand(ret.Value) },
        _ => ins,
    };
}
