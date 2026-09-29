using System.CommandLine;
using CathodeRay.Cli;
using CathodeRay.Stub;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Testy sterownika <c>cathode cc</c>: dwa moduły C, link, runtime, mapa.</summary>
public sealed class CCliTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("cathode-cc-");

    public void Dispose() => _dir.Delete(recursive: true);

    private string File(string name, string content)
    {
        string path = Path.Combine(_dir.FullName, name);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    private static (int Exit, string Out, string Err) Cli(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exit = CliApp.CreateRoot().Parse(args).Invoke(new InvocationConfiguration { Output = output, Error = error });
        return (exit, output.ToString(), error.ToString());
    }

    [Fact]
    public void Cc_Links_Two_Modules_And_Runs_With_Map()
    {
        string a = File("a.c", "int base = 100;\nint add(int a, int b) { return base + a + b; }\n");
        string b = File("b.c", "int add(int a, int b);\nint main() { return add(40, 2); }\n");
        string bin = Path.Combine(_dir.FullName, "prog.bin");
        string listing = Path.Combine(_dir.FullName, "prog.lst");
        string map = Path.Combine(_dir.FullName, "prog.map");

        var (exit, output, err) = Cli("cc", a, b, "-o", bin, "-l", listing, "--map", map);

        exit.Should().Be(0, err);
        output.Should().Contain("prog.bin");
        byte[] image = System.IO.File.ReadAllBytes(bin);
        image.Should().NotBeEmpty();

        var bus = new StubBus();
        const int origin = 0x1000;
        for (int i = 0; i < image.Length; i++)
        {
            bus.Write((ushort)(origin + i), image[i]);
        }

        StubIsa isa = StubIsa.FromJsonFile(Repo.IsaFile("mcp_stub_instructions.json"));
        var cpu = new StubCpu(isa, bus);
        cpu.State.ProgramCounter = origin;
        for (int steps = 0; !cpu.State.Halted; steps++)
        {
            steps.Should().BeLessThan(100_000, "program ma się zatrzymać");
            cpu.Step();
        }

        cpu.State.A.Should().Be(142);
        string mapText = System.IO.File.ReadAllText(map);
        mapText.Should().MatchRegex(@"(?m)^[0-9A-F]{4} a\.c:2$");
        mapText.Should().MatchRegex(@"(?m)^[0-9A-F]{4} b\.c:2$");
        System.IO.File.ReadAllText(listing).Should().Contain(";c:");

        var run = Cli("stub", "run", bin, "--load", "0x1000");
        run.Exit.Should().Be(0, run.Err);
        run.Out.Should().Contain("halted=True");
        run.Out.Should().Contain("A=8E");
    }

    [Fact]
    public void Cc_Reports_Missing_Main_From_Link()
    {
        string a = File("nolib.c", "int f(int x) { return x; }\n");

        var (exit, _, err) = Cli("cc", a, "-o", Path.Combine(_dir.FullName, "x.bin"));

        exit.Should().Be(1);
        err.Should().Contain("main");
    }
}
