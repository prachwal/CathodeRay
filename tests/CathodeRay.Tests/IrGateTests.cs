using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Bramka migracji do IR (plan 30, krok 2): asembler wyjściowy dla korpusu programów (samples, przykłady z
/// docs/minic.md, moduły biblioteki) ma być bajt w bajt taki jak zapisany wzorzec. Zmiana generatora wymaga
/// świadomej regeneracji: <c>UPDATE_IR_GATE=1 dotnet test --filter IrGateTests</c>.</summary>
public sealed partial class IrGateTests
{
    private static string GatePath => Repo.Path("tests", "CathodeRay.Tests", "ir-gate.txt");

    private static IEnumerable<(string Id, string Source, Func<string, string?> Reader)> Corpus()
    {
        string samples = Repo.Path("samples", "minic");
        string? Reader(string name)
        {
            if (name.StartsWith('<'))
            {
                return StdLib.Header(name[1..^1]);
            }

            string path = Path.Combine(samples, name);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        foreach (string file in Directory.GetFiles(samples, "*.c", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            yield return ($"sample:{Path.GetRelativePath(samples, file).Replace('\\', '/')}", File.ReadAllText(file), Reader);
        }

        foreach (StdModule module in StdLib.Modules.Where(static m => !m.IsAssembly))
        {
            yield return ($"stdlib:{module.Name}", module.Source, StdLib.HeaderReader);
        }

        string docs = File.ReadAllText(Repo.Path("docs", "minic.md"));
        int index = 0;
        foreach (Match match in Fence().Matches(docs))
        {
            index++;
            yield return ($"doc:{index:D2}", match.Groups["code"].Value, StdLib.HeaderReader);
        }
    }

    private static string Fingerprint(string source, Func<string, string?> reader, bool objectMode, bool optimize)
    {
        string text;
        try
        {
            CheckedProgram program = TypeChecker.Check(Parser.Parse(source, reader), allowPointerIntegerConversion: true);
            text = Codegen.Emit(program, "gate.c", objectMode, optimize);
        }
        catch (Exception e) when (e is CParseException or CTypeException or CCodegenException or CPreprocessException)
        {
            text = "ERROR " + e.GetType().Name + ": " + e.Message;
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private static List<string> Compute()
    {
        var lines = new List<string>();
        foreach ((string id, string source, Func<string, string?> reader) in Corpus())
        {
            foreach (bool objectMode in new[] { false, true })
            {
                foreach (bool optimize in new[] { false, true })
                {
                    lines.Add($"{id}|obj={(objectMode ? 1 : 0)}|opt={(optimize ? 1 : 0)}|{Fingerprint(source, reader, objectMode, optimize)}");
                }
            }
        }

        return lines;
    }

    [Fact]
    public void Generated_Assembly_Matches_The_Recorded_Gate_Byte_For_Byte()
    {
        List<string> actual = Compute();
        if (Environment.GetEnvironmentVariable("UPDATE_IR_GATE") == "1")
        {
            File.WriteAllLines(GatePath, actual);
        }

        string[] expected = File.ReadAllLines(GatePath);
        actual.Count.Should().BeGreaterThan(100, "korpus ma pokrywać samples, bibliotekę i dokumentację");
        actual.Should().Equal(expected);
    }

    [GeneratedRegex(@"```c (?:expect=\d+|error=""[^""]+"")\r?\n(?<code>.*?)```", RegexOptions.Singleline)]
    private static partial Regex Fence();
}
