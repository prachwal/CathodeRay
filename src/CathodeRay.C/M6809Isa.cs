namespace CathodeRay.C;

/// <summary>Prymitywy Motorola 6809: ABI v1 jest pamięciowe, a ABI v2 może przekazać trzy pierwsze słowa w X/Y/U.</summary>
internal sealed class M6809Isa : M6800Isa, ICopyLoop, IRegArgs
{
    private static readonly IReadOnlySet<string> ReservedNames = new HashSet<string>(
        ["A", "B", "D", "X", "Y", "U", "S", "CC", "DP", "PC"],
        StringComparer.OrdinalIgnoreCase);

    private string? _argFetchRegister;

    private string? _argStoreRegister;

    public string? EntryParkCell => null;

    protected override IReadOnlySet<string> Reserved => ReservedNames;

    protected override string CpuName => "6809";

    protected override string LoadMnemonic => "lda";

    protected override string StoreMnemonic => "sta";

    protected override string OrMnemonic => "ora";

    public override void PushA() => Raw("pshs a");

    public override void PopA() => Raw("puls a");

    public override string ArgCell(int index, int part, int width) =>
        AbiV2 && width == 2 && index < Model.ArgRegs.Count ? Model.ArgRegs[index] : MemArgCell(index, part);

    public void SetupFp(string cell)
    {
        Raw($"ldx {Mem(cell)}");
        Raw("stx z:cc_fp");
    }

    public void FetchArg(string register)
    {
        Raw($"tfr {register},d");
        if (_argFetchRegister == register)
        {
            _argFetchRegister = null;
        }
        else
        {
            Raw("tfr b,a");
            _argFetchRegister = register;
        }
    }

    public void StoreArg(string register)
    {
        if (_argStoreRegister == register)
        {
            Raw("exg a,b");
            Raw($"tfr d,{register}");
            _argStoreRegister = null;
            return;
        }

        Raw("tfr a,b");
        _argStoreRegister = register;
    }

    public bool TryStoreArgWord(string register, Word value)
    {
        string mnemonic = register switch
        {
            "x" => "ldx",
            "y" => "ldy",
            "u" => "ldu",
            _ => string.Empty,
        };
        if (mnemonic.Length == 0 || (!value.IsImmediate && !IsMemoryWord(value)))
        {
            return false;
        }

        Raw(value.IsImmediate ? $"{mnemonic} #{value.Lo}" : $"{mnemonic} {Mem(value.Hi)}");
        return true;
    }

    public bool TryFetchArgWord(string register, Word dst)
    {
        string mnemonic = register switch
        {
            "x" => "stx",
            "y" => "sty",
            "u" => "stu",
            _ => string.Empty,
        };
        if (mnemonic.Length == 0 || !IsMemoryWord(dst))
        {
            return false;
        }

        Raw($"{mnemonic} {Mem(dst.Hi)}");
        return true;
    }

    public override bool TryStep(IReadOnlyList<string> bytes, bool increment)
    {
        string operation = increment ? "inc" : "dec";
        if (bytes.Count == 1)
        {
            Raw($"{operation} {Mem(bytes[0])}");
            return true;
        }

        string low = Mem(bytes[0]);
        string high = Mem(bytes[1]);
        string skip = LocalLabel();
        if (increment)
        {
            Raw($"inc {low}");
            Raw($"bne {skip}");
            Raw($"inc {high}");
            Raw($"{skip}:");
            return true;
        }

        Raw($"tst {low}");
        Raw($"bne {skip}");
        Raw($"dec {high}");
        Raw($"{skip}:");
        Raw($"dec {low}");
        return true;
    }

    /// <summary>Kopiuje bajty pętlą indexed na X/Y z 16-bitowym licznikiem w U.</summary>
    /// <param name="dst">Wskaźnik celu.</param>
    /// <param name="src">Wskaźnik źródła.</param>
    /// <param name="count">Licznik bajtów.</param>
    /// <returns><see langword="false"/>, gdy któryś operand nie jest sąsiednim słowem pamięci.</returns>
    public bool TryCopyLoop(Word dst, Word src, Word count)
    {
        if (!IsMemoryWord(dst) || !IsMemoryWord(src) || !IsMemoryWord(count))
        {
            return false;
        }

        Raw($"ldx {Mem(src.Hi)}");
        Raw($"ldy {Mem(dst.Hi)}");
        Raw($"ldu {Mem(count.Hi)}");
        string skip = LocalLabel();
        string loop = LocalLabel();
        Raw($"beq {skip}");
        Raw($"{loop}:");
        Raw("lda ,x+");
        Raw("sta ,y+");
        Raw("leau -1,u");
        Raw("cmpu #0");
        Raw($"bne {loop}");
        Raw($"{skip}:");
        return true;
    }

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

    private static bool IsMemoryWord(Word word) =>
        !word.IsImmediate && word.Lo != word.Hi + "_h" && CellMap.Adjacent(new Word(false, word.Hi, word.Lo));
}
