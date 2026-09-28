using CathodeRay.Assembler.Isa;

namespace CathodeRay.Assembler.Directives;

internal sealed class DataDirective(int width) : IDirective
{
    public void Execute(IAssemblyContext context, string? operand)
    {
        foreach (string item in OperandList.Split(operand ?? throw context.Error("value expected.")))
        {
            if (width == 1 && OperandList.TryUnquote(item, out string? text))
            {
                foreach (char c in text)
                {
                    context.Emit(c <= 0xFF ? (byte)c : throw context.Error($"character '{c}' is not 8-bit."));
                }

                continue;
            }

            int value = context.EvaluateEmission(item, width == 1 ? FieldKind.Byte : FieldKind.Word, out bool relocated);
            int max = width == 1 ? byte.MaxValue : ushort.MaxValue;
            if (!relocated && (value < 0 || value > max))
            {
                throw context.Error($"value {value} out of range 0..{max}.");
            }

            bool bigEndian = context.Endianness == Endianness.Big;
            for (int i = 0; i < width; i++)
            {
                int shift = 8 * (bigEndian ? width - 1 - i : i);
                context.Emit((byte)(value >> shift));
            }
        }
    }
}
