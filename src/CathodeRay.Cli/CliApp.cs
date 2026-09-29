using System.CommandLine;

namespace CathodeRay.Cli;

/// <summary>Drzewo komend <c>cathode</c> (osobno od <c>Main</c>, żeby testy wywoływały je w procesie).</summary>
internal static class CliApp
{
    /// <summary>Buduje komendę główną: wspólny asembler i grupy CPU z uruchamianiem.</summary>
    /// <returns>Komenda główna.</returns>
    public static RootCommand CreateRoot()
    {
        var root = new RootCommand("CathodeRay: asemblery i uruchamianie programów dla emulowanych CPU.");
        root.Subcommands.Add(AsmCommand.Create());
        root.Subcommands.Add(LinkCommand.Create());
        root.Subcommands.Add(CcCommand.Create());
        root.Subcommands.Add(StubCommands.Create());
        return root;
    }
}
