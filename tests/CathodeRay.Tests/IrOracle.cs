using CathodeRay.C;
using CathodeRay.Stub;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Wyrocznia różnicowa: ten sam program, obniżony do IR, uruchomiony w <see cref="IrInterpreter"/> (bez CPU),
/// musi zwrócić z <c>main</c> to samo co skompilowany program na stubie. Dzięki temu każdy test używający
/// <c>CCodegenTests.RunC</c> sprawdza także semantykę front-endu niezależnie od celu.</summary>
public static class IrOracle
{
    private static readonly string[] Builtins = ["putchar", "puthex", "putdec"];

    public static (int Value, int Width, string Console)? Run(CheckedProgram program)
    {
        Ir.Module module = Codegen.Lower(program);
        if (module.ExternFunctions.Any(static f => !Builtins.Contains(f)) || module.ExternCells.Count > 0)
        {
            return null;
        }

        if (!module.Functions.Any(static f => f.Name == "main"))
        {
            return null;
        }

        var interpreter = IrInterpreter.Load([module]);
        (int value, int width) = interpreter.RunMain();
        return (value, width, interpreter.Console);
    }

    public static void AssertSameAsCpu(CheckedProgram program, StubCpu cpu)
    {
        (int Value, int Width, string Console)? oracle = Run(program);
        if (oracle is null || oracle.Value.Width == 0)
        {
            return;
        }

        int actual = oracle.Value.Width == 2 ? cpu.State.A | (cpu.State.X << 8) : cpu.State.A;
        oracle.Value.Value.Should().Be(actual, "interpreter IR i procesor stub muszą zwracać to samo z main");
    }
}
