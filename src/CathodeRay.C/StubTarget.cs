using System.Text;

namespace CathodeRay.C;

/// <summary>Cel „stub”: minimalny procesor akumulatorowy (A, X, tylko adresowanie absolutne; wskaźniki przez
/// łatanie operandów). Kod pośredni zawiera na razie gotowe mnemoniki stuba (<see cref="Raw"/>), więc druk to złączenie.</summary>
public sealed class StubTarget : ICTarget
{
    /// <inheritdoc/>
    public string Name => "stub";

    /// <inheritdoc/>
    public string Description => "ISA zaślepki CathodeRay (akumulator A, indeks X)";

    /// <inheritdoc/>
    public string AssemblerCpu => "stub";

    /// <inheritdoc/>
    public TargetByteOrder ByteOrder => TargetByteOrder.Little;

    /// <inheritdoc/>
    public int? StackLimit => 256;

    /// <inheritdoc/>
    public TargetLayout Layout { get; } = new(
        [
            new TargetArea("C_CODE", 0x1000, 0x5F00),
            new TargetArea("C_INIT", 0x6F00, 0x100),
            new TargetArea("C_BSS", 0x7000, 0x1000),
            new TargetArea("C_DATA", 0x8000, 0x8000),
        ],
        [
            new TargetSegment("CODE", "C_CODE"),
            new TargetSegment("INIT", "C_INIT"),
            new TargetSegment("BSS", "C_BSS"),
            new TargetSegment("DATA", "C_DATA"),
        ]);

    /// <inheritdoc/>
    public string Crt0 => C.Crt0.Source;

    /// <inheritdoc/>
    public IReadOnlyList<StdModule> RuntimeModules => [.. StdLib.Modules.Where(static m => m.IsAssembly)];

    /// <inheritdoc/>
    public string Emit(IrModule module, bool optimize)
    {
        ArgumentNullException.ThrowIfNull(module);
        string code = Print(module.Code);
        if (optimize)
        {
            code = Peephole.Optimize(code);
        }

        return code + Print(module.Init) + Print(module.Data) + Print(module.Bss);
    }

    private static string Print(IEnumerable<IrItem> items)
    {
        var text = new StringBuilder();
        foreach (IrItem item in items)
        {
            switch (item)
            {
                case Raw raw:
                    text.Append(raw.Text);
                    break;
                case IrFunction function:
                    text.Append(Print(function.Body));
                    break;
                default:
                    throw new InvalidOperationException($"StubTarget cannot print {item.GetType().Name}.");
            }
        }

        return text.ToString();
    }
}
