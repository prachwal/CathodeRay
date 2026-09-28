namespace CathodeRay.Assembler.Directives;

/// <summary>Rodzaj dyrektywy segmentu.</summary>
internal enum SegmentKind
{
    /// <summary>Przełącza na nazwany segment (emitujący): <c>.segment "NAZWA"</c>, <c>SEGMENT "NAZWA"</c>.</summary>
    Segment,

    /// <summary>Segment kodu: <c>.code</c>, <c>CODE</c>.</summary>
    Code,

    /// <summary>Segment danych: <c>.data</c>, <c>DATA</c>.</summary>
    Data,

    /// <summary>Segment nieemitowany: <c>.bss</c>, <c>BSS</c> (adresy rosną, bajty nie trafiają do obrazu).</summary>
    Bss,
}

/// <summary>Przełącza segment (<c>Pass</c> trzyma liczniki); logika w kontekście.</summary>
internal sealed class SegmentDirective(SegmentKind kind) : IDirective
{
    /// <summary>Rodzaj dyrektywy.</summary>
    public SegmentKind Kind => kind;

    public void Execute(IAssemblyContext context, string? operand)
    {
        switch (kind)
        {
            case SegmentKind.Segment:
                string name = operand is null
                    ? throw context.Error(".segment needs a quoted name.")
                    : Unquote(context, operand);
                context.SwitchSegment(name, emit: null);
                break;
            case SegmentKind.Code:
                NoOperand(context, operand);
                context.SwitchSegment("CODE", emit: true);
                break;
            case SegmentKind.Data:
                NoOperand(context, operand);
                context.SwitchSegment("DATA", emit: true);
                break;
            default:
                NoOperand(context, operand);
                context.SwitchSegment("BSS", emit: false);
                break;
        }
    }

    private static string Unquote(IAssemblyContext context, string operand)
    {
        string name = FileResolve.Unquote(operand, ".segment", context.Error);
        return name.Contains('@')
            ? throw context.Error($"segment name '{name}' must not contain '@' (--map uses NAME@address).")
            : name;
    }

    private static void NoOperand(IAssemblyContext context, string? operand)
    {
        if (operand is not null)
        {
            throw context.Error("no operand allowed here (use .segment \"NAME\" for a named segment).");
        }
    }
}
