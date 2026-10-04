namespace CathodeRay.C;

/// <summary>Prymitywy Motorola 6809: kompilator v1 używa A, B i X, a dane są big-endian.</summary>
internal sealed class M6809Isa : M6800Isa
{
    private static readonly IReadOnlySet<string> ReservedNames = new HashSet<string>(
        ["A", "B", "D", "X", "Y", "U", "S", "CC", "DP", "PC"],
        StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlySet<string> Reserved => ReservedNames;

    protected override string CpuName => "6809";

    protected override string LoadMnemonic => "lda";

    protected override string StoreMnemonic => "sta";

    protected override string OrMnemonic => "ora";

    public override void PushA() => Raw("pshs a");

    public override void PopA() => Raw("puls a");

    public override string Crt0()
    {
        string marker = ".segment \"CODE\"" + Environment.NewLine;
        string initialization = marker + "    lda #0" + Environment.NewLine + "    tfr a,dp" + Environment.NewLine;
        return base.Crt0()
            .Replace(marker, initialization, StringComparison.Ordinal)
            .Replace("dex", "leax -1,x", StringComparison.Ordinal)
            .Replace("inx", "leax 1,x", StringComparison.Ordinal)
            .Replace("cpx ", "cmpx ", StringComparison.Ordinal)
            .Replace("leax 1,x\nleax 1,x", "leax 2,x", StringComparison.Ordinal)
            .Replace("__crt_halt: bra __crt_halt", "__crt_halt: sync", StringComparison.Ordinal);
    }
}
