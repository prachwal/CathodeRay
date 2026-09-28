using System.CommandLine;

namespace CathodeRay.Cli;

/// <summary>Drzewo komend <c>cathode</c> (osobno od <c>Main</c>, żeby testy wywoływały je w procesie).</summary>
internal static class CliApp
{
    /// <summary>Buduje komendę główną ze wszystkimi grupami CPU.</summary>
    /// <returns>Komenda główna.</returns>
    public static RootCommand CreateRoot()
    {
        var root = new RootCommand("CathodeRay: asemblery i uruchamianie programów dla emulowanych CPU.");
        root.Subcommands.Add(StubCommands.Create());
        return root;
    }
}
