using System.Reflection;
using System.Text.RegularExpressions;

namespace CathodeRay.C;

/// <summary>Biblioteka standardowa mini-C: nagłówki (<c>&lt;string.h&gt;</c> …) i moduły
/// (C oraz konsola w asemblerze), osadzone jako zasoby. Sterownik <c>cc</c> linkuje tylko moduły,
/// które definiują nierozwiązane symbole.</summary>
public static partial class StdLib
{
    private static readonly Lazy<IReadOnlyList<StdModule>> LazyModules = new(LoadModules);

    /// <summary>Moduły biblioteki (po nazwie pliku).</summary>
    public static IReadOnlyList<StdModule> Modules => LazyModules.Value;

    /// <summary>Czyta nagłówek biblioteki.</summary>
    /// <param name="name">Nazwa (<c>string.h</c>).</param>
    /// <returns>Tekst albo <see langword="null"/>.</returns>
    public static string? Header(string name) => Read($"stdlib/include/{name}");

    /// <summary>Czytnik <c>#include</c> dla modułów biblioteki: tylko <c>&lt;nagłówki&gt;</c>.</summary>
    /// <param name="name">Nazwa z nawiasami kątowymi.</param>
    /// <returns>Tekst nagłówka albo <see langword="null"/>.</returns>
    public static string? HeaderReader(string name) =>
        name.StartsWith('<') ? Header(name[1..^1]) : null;

    /// <summary>Czyta źródło modułu przenośnego (<c>stdlib/portable</c>): C kompilowane na dowolny cel.</summary>
    /// <param name="name">Nazwa pliku (<c>rt.c</c>).</param>
    /// <returns>Tekst źródłowy.</returns>
    public static string Portable(string name) =>
        Read($"stdlib/portable/{name}") ?? throw new InvalidOperationException($"missing embedded resource 'stdlib/portable/{name}'.");

    private static string? Read(string resource)
    {
        using Stream? stream = typeof(StdLib).Assembly.GetManifestResourceStream(resource);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static IReadOnlyList<StdModule> LoadModules()
    {
        var modules = new List<StdModule>();
        foreach (string resource in typeof(StdLib).Assembly.GetManifestResourceNames().Where(static n => n.StartsWith("stdlib/lib/", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            string name = resource["stdlib/lib/".Length..];
            string source = Read(resource)!;
            bool asm = name.EndsWith(".s", StringComparison.Ordinal);
            HashSet<string> defines = asm
                ? [.. GlobalDirective().Matches(source).Select(static m => m.Groups[1].Value)]
                : [.. Parser.Parse(source, HeaderReader).Functions.Where(static f => !f.IsExtern && !f.IsStatic).Select(static f => f.Name)];
            modules.Add(new StdModule(name, source, asm, defines));
        }

        return modules;
    }

    [GeneratedRegex(@"^\.global\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex GlobalDirective();
}
