using CathodeRay.Assembler;
using CathodeRay.Assembler.Isa;

namespace CathodeRay.Tests;

/// <summary>Ścieżki do plików repozytorium i skróty do asemblera w testach.</summary>
internal static class Repo
{
    private static readonly DirectoryInfo Root = FindRoot();

    public static string Path(params string[] parts) => System.IO.Path.Combine([Root.FullName, .. parts]);

    public static string IsaFile(string name) => Path("data", "instructions", name);

    public static InstructionSet LoadTarget(AssemblerTarget target)
    {
        using FileStream json = File.OpenRead(IsaFile(target.IsaFile));
        return target.Load(json);
    }

    public static AssemblyResult Assemble(string cpu, string source, string? syntax = null)
    {
        AssemblerTarget target = AssemblerTargets.Find(cpu)!;
        return new TwoPassAssembler(LoadTarget(target), syntax is null ? target.DefaultSyntax : target.FindSyntax(syntax)!)
            .Assemble(source);
    }

    private static DirectoryInfo FindRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(System.IO.Path.Combine(dir.FullName, "data", "instructions")))
        {
            dir = dir.Parent;
        }

        return dir ?? throw new DirectoryNotFoundException("Repository root with data/instructions not found.");
    }
}
