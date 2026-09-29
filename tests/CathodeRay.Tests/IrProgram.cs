using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Buduje moduł IR z serii przypadków testowych: każdy przypadek zapisuje wynik w kolejnym 2-bajtowym slocie
/// tablicy <c>c_res</c>. Ten sam moduł biegnie na interpreterze IR (wyrocznia) i na każdym celu; tablice muszą być równe.</summary>
public sealed class IrProgram
{
    private readonly List<Ir.Ins> _body = [];
    private readonly List<string> _names = [];
    private readonly List<int> _widths = [];
    private readonly List<Ir.Data> _extraData = [];
    private readonly List<Ir.Function> _extraFunctions = [];
    private int _label;

    public Ir.Cell A(int width) => new("c_a", width);

    public Ir.Cell B(int width) => new("c_b", width);

    public Ir.Cell R(int width) => new("c_r", width);

    public string NewLabel() => $"Q{++_label}";

    public IrProgram Emit(params Ir.Ins[] instructions)
    {
        _body.AddRange(instructions);
        return this;
    }

    public IrProgram AddData(Ir.Data data)
    {
        _extraData.Add(data);
        return this;
    }

    public IrProgram AddFunction(Ir.Function function)
    {
        _extraFunctions.Add(function);
        return this;
    }

    /// <summary>Zapisuje wartość komórki w kolejnym slocie wyników.</summary>
    public IrProgram Record(Ir.Cell result, string description)
    {
        _body.Add(new Ir.Store(new Ir.AddrOf("c_res", 0), _names.Count * 4, result, result.W));
        _names.Add(description);
        _widths.Add(result.W);
        return this;
    }

    public Ir.Module Build()
    {
        var body = new List<Ir.Ins>(_body) { new Ir.Ret(null, 0) };
        List<Ir.Data> data =
        [
            new("c_a", "BSS", 4, null, false),
            new("c_b", "BSS", 4, null, false),
            new("c_r", "BSS", 4, null, false),
            new("c_res", "BSS", Math.Max(_names.Count * 4, 4), null, true),
            .. _extraData,
        ];
        return new Ir.Module([new Ir.Function("main", false, [], 0, [], body), .. _extraFunctions], data, [], [], ObjectMode: false);
    }

    /// <summary>Uruchamia na interpreterze IR i na każdym celu; wyniki muszą być bajt w bajt równe.</summary>
    public void AssertConforms(string what)
    {
        Ir.Module module = Build();
        var interpreter = IrInterpreter.Load([module]);
        interpreter.RunMain();
        int size = Math.Max(_names.Count * 4, 4);
        byte[] expected = interpreter.Peek("c_res", size);
        foreach (ICTarget target in TargetHarness.Targets)
        {
            foreach (bool optimize in new[] { true, false })
            {
                byte[] actual = TargetHarness.Run(target, module, optimize).Read("c_res", size);
                for (int slot = 0; slot < _names.Count; slot++)
                {
                    int width = _widths[slot];
                    if (target.ByteOrder == TargetByteOrder.Big)
                    {
                        Array.Reverse(actual, slot * 4, width);
                    }

                    for (int i = 0; i < width; i++)
                    {
                        if (actual[(slot * 4) + i] != expected[(slot * 4) + i])
                        {
                            Assert.Fail($"{what} [{target.Name}, optimize={optimize}]: {_names[slot]}: interpreter {Hex(expected, slot * 4, width)}, target {Hex(actual, slot * 4, width)}");
                        }
                    }
                }
            }
        }
    }

    private static string Hex(byte[] bytes, int at, int width) => string.Concat(Enumerable.Range(0, width).Reverse().Select(i => bytes[at + i].ToString("X2", System.Globalization.CultureInfo.InvariantCulture)));
}
