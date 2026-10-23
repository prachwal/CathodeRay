using System.Reflection;
using System.Text.RegularExpressions;

namespace CathodeRay.C;

/// <summary>Biblioteka standardowa mini-C: nagłówki (<c>&lt;string.h&gt;</c> …) i moduły
/// (C oraz konsola w asemblerze), osadzone jako zasoby. Sterownik <c>cc</c> linkuje tylko moduły,
/// które definiują nierozwiązane symbole.</summary>
public static partial class StdLib
{
    private static readonly Lazy<IReadOnlyList<StdModule>> LazyModules = new(LoadModules);

    private static readonly Lazy<IReadOnlyList<StdModule>> LazyRuntime = new(() =>
    [
        .. new[] { "rt_mul.c", "rt_mul8.c", "rt_div.c", "rt_divs.c", "rt_div8.c", "rt_shift.c", "rt_mem.c", "rt_mul32.c", "rt_div32.c", "rt_shift32.c", "rt_float.c", "rt_double.c", "rt_ll64.c" }.Select(static name =>
        {
            string source = Portable(name);
            return new StdModule(name, source, false, CDefinitions(source, null));
        }),
    ]);

    /// <summary>Moduły biblioteki (po nazwie pliku).</summary>
    public static IReadOnlyList<StdModule> Modules => LazyModules.Value;

    /// <summary>Moduły przenośnych procedur wykonawczych (<c>stdlib/portable/rt_*.c</c>): mnożenie, dzielenie, przesunięcia o zmienną liczbę
    /// pozycji (także 32-bitowe) i bloki pamięci. Każdy linkowany raz na żądanie zamiast kopii w każdym module.</summary>
    public static IReadOnlyList<StdModule> RuntimeModules => LazyRuntime.Value;

    /// <summary>Wszystkie procedury wykonawcze jako jeden program C (dla trybu całego programu bez linkera).</summary>
    public static string RuntimeSource => string.Join("\n", RuntimeModules.Select(static m => m.Source));

    /// <summary>Czyta nagłówek biblioteki.</summary>
    /// <param name="name">Nazwa (<c>string.h</c>).</param>
    /// <returns>Tekst albo <see langword="null"/>.</returns>
    public static string? Header(string name) => Read($"stdlib/include/{name}");

    /// <summary>Czytnik <c>#include</c> dla modułów biblioteki: tylko <c>&lt;nagłówki&gt;</c>.</summary>
    /// <param name="name">Nazwa z nawiasami kątowymi.</param>
    /// <returns>Tekst nagłówka albo <see langword="null"/>.</returns>
    public static string? HeaderReader(string name) =>
        name.StartsWith('<') ? Header(name[1..^1]) : null;

    /// <summary>Moduł asemblerowy procedur wykonawczych konkretnego celu (<c>stdlib/target/&lt;cel&gt;/plik.s</c>); linkowany zamiast wersji z C.</summary>
    /// <param name="target">Katalog celu (<c>6502</c>, <c>z80</c>).</param>
    /// <param name="name">Nazwa pliku.</param>
    /// <param name="defines">Symbole definiowane przez moduł.</param>
    /// <returns>Moduł.</returns>
    public static StdModule TargetModule(string target, string name, params string[] defines) =>
        new($"{name}@{target}", Read($"stdlib/target/{target}/{name}") ?? throw new InvalidOperationException($"missing embedded resource 'stdlib/target/{target}/{name}'."), true, new HashSet<string>(defines, StringComparer.Ordinal));

    /// <summary>Czyta źródło modułu przenośnego (<c>stdlib/portable</c>): C kompilowane na dowolny cel.</summary>
    /// <param name="name">Nazwa pliku (<c>rt.c</c>).</param>
    /// <returns>Tekst źródłowy.</returns>
    public static string Portable(string name) =>
        Read($"stdlib/portable/{name}") ?? throw new InvalidOperationException($"missing embedded resource 'stdlib/portable/{name}'.");

    /// <summary>Symbole definiowane przez moduł C: funkcje i zmienne globalne (nie static, nie extern) pod nazwą symbolu asemblera (<c>cc_g_</c> + nazwa), np. <c>cc_g___stderr_f</c> —
    /// program odwołujący się tylko do obiektu danych też dołącza moduł.</summary>
    private static HashSet<string> CDefinitions(string source, IReadOnlyDictionary<string, string>? defines)
    {
        Ast.Program program = Parser.Parse(source, HeaderReader, defines);
        return
        [
            .. program.Functions.Where(static f => !f.IsExtern && !f.IsStatic).Select(static f => f.Name),
            .. program.Globals.Where(static g => (g.Flags & (DeclFlags.Static | DeclFlags.Extern)) == 0).Select(static g => $"cc_g_{g.Name}"),
        ];
    }

    private static string? Read(string resource)
    {
        Assembly assembly = typeof(StdLib).Assembly;
        Stream? stream = assembly.GetManifestResourceStream(resource);
        if (stream is null)
        {
            // %(RecursiveDir) daje na Windowsie nazwy z ukośnikiem wstecznym (stdlib/target/6502\rt_mul.s), więc szukamy też po nazwie znormalizowanej.
            string? alias = assembly.GetManifestResourceNames().FirstOrDefault(n => n.Replace('\\', '/') == resource);
            stream = alias is null ? null : assembly.GetManifestResourceStream(alias);
        }

        if (stream is null)
        {
            return null;
        }

        using Stream owned = stream;
        using var reader = new StreamReader(owned);
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
                : CDefinitions(source, null);
            modules.Add(new StdModule(name, source, asm, defines));
        }

        return modules;
    }

    [GeneratedRegex(@"^\.global\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex GlobalDirective();
}
