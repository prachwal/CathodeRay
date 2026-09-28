using CathodeRay.Assembler.Directives;
using CathodeRay.Assembler.Isa;
using CathodeRay.Assembler.Syntax;

namespace CathodeRay.Assembler;

/// <summary>Dwuprzebiegowy asembler niezależny od CPU i składni: cel (<see cref="InstructionSet"/>) i dialekt
/// (<see cref="SyntaxDialect"/>) są wymienne. Pierwszy przebieg zbiera symbole i ustala rozmiary instrukcji,
/// drugi emituje bajty. Pierwszy błąd przerywa asemblację (<see cref="AssemblerException"/> z numerem linii).</summary>
/// <param name="isa">Zestaw instrukcji celu.</param>
/// <param name="dialect">Dialekt składni.</param>
public sealed class TwoPassAssembler(InstructionSet isa, SyntaxDialect dialect)
{
    private const int AddressSpace = 0x10000;

    private InstructionSet Isa => isa;

    private SyntaxDialect Dialect => dialect;

    /// <summary>Asembluje źródło.</summary>
    /// <param name="source">Tekst źródła.</param>
    /// <returns>Obraz, symbole i listing.</returns>
    /// <exception cref="AssemblerException">Błąd w źródle.</exception>
    public AssemblyResult Assemble(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        SourceLine[] lines = [.. source.Split('\n').Select((text, i) =>
            LineParser.Parse(i + 1, text.TrimEnd('\r'), dialect, IsKeyword))];
        var symbols = new Dictionary<string, int>(dialect.SymbolComparer);
        var choices = new Dictionary<int, FormChoice>();

        var first = new Pass(this, symbols, choices, final: false);
        first.Run(lines);
        ResolvePendingAssignments(first);
        var second = new Pass(this, symbols, choices, final: true);
        second.Run(lines);
        return second.Result();
    }

    private static void ResolvePendingAssignments(Pass pass)
    {
        bool progress = true;
        while (progress && pass.Pending.Count > 0)
        {
            progress = false;
            foreach (SourceLine line in pass.Pending.ToArray())
            {
                if (pass.TryDefineAssignment(line))
                {
                    pass.Pending.Remove(line);
                    progress = true;
                }
            }
        }
    }

    private bool IsKeyword(string word) => isa.Contains(word) || dialect.Directives.ContainsKey(word);

    private sealed class Pass(TwoPassAssembler owner, Dictionary<string, int> symbols, Dictionary<int, FormChoice> choices, bool final)
        : IAssemblyContext
    {
        private readonly byte[] _image = new byte[AddressSpace];
        private readonly bool[] _written = new bool[AddressSpace];
        private readonly List<ListingLine> _listing = [];
        private readonly List<byte> _lineBytes = [];
        private SourceLine _line = new(0, string.Empty, null, null, null);
        private bool _stopped;

        public int ProgramCounter { get; private set; }

        public Endianness Endianness => owner.Isa.Endianness;

        public List<SourceLine> Pending { get; } = [];

        public void Run(IEnumerable<SourceLine> lines)
        {
            foreach (SourceLine line in lines.TakeWhile(_ => !_stopped))
            {
                _line = line;
                _lineBytes.Clear();
                int start = ProgramCounter;
                try
                {
                    Process(line);
                }
                catch (FormatException e)
                {
                    throw Error(e.Message);
                }

                int address = _lineBytes.Count > 0 ? start : ProgramCounter;
                _listing.Add(new ListingLine(line.Number, address, [.. _lineBytes], line.Text));
            }
        }

        public AssemblyResult Result()
        {
            int low = Array.IndexOf(_written, true);
            int high = Array.LastIndexOf(_written, true);
            byte[] image = low < 0 ? [] : _image[low..(high + 1)];
            return new AssemblyResult(Math.Max(low, 0), image, symbols, _listing);
        }

        public bool TryDefineAssignment(SourceLine line)
        {
            _line = line;
            int? value = TryEvaluate(line.Operand ?? throw Error("value expected."));
            if (value is int known)
            {
                Define(line.Label!, known);
            }

            return value is not null;
        }

        public void SetProgramCounter(int address) =>
            ProgramCounter = address is >= 0 and < AddressSpace ? address : throw Error($"address {address} outside $0000..$FFFF.");

        public void Emit(byte value)
        {
            if (ProgramCounter >= AddressSpace)
            {
                throw Error("code runs past $FFFF.");
            }

            if (final)
            {
                if (_written[ProgramCounter])
                {
                    throw Error($"overlapping output at ${ProgramCounter:X4}.");
                }

                _image[ProgramCounter] = value;
                _written[ProgramCounter] = true;
                _lineBytes.Add(value);
            }

            ProgramCounter++;
        }

        public int? TryEvaluate(string expression) =>
            Expression.Evaluate(expression, owner.Dialect, ProgramCounter, Lookup);

        public int Evaluate(string expression) =>
            TryEvaluate(expression) ?? throw Error($"'{expression}' must be known at this point (no forward references).");

        public void Stop() => _stopped = true;

        public AssemblerException Error(string message) => new(_line.Number, message);

        private int? Lookup(string name) =>
            symbols.TryGetValue(name, out int value) ? value : final ? throw new FormatException($"undefined symbol '{name}'.") : null;

        private void Process(SourceLine line)
        {
            if (line.IsAssignment)
            {
                if (!TryDefineAssignment(line))
                {
                    Pending.Add(line);
                }

                return;
            }

            if (line.Label is not null)
            {
                Define(line.Label, ProgramCounter);
            }

            if (line.Keyword is null)
            {
                return;
            }

            if (owner.Dialect.Directives.TryGetValue(line.Keyword, out IDirective? directive))
            {
                directive.Execute(this, line.Operand);
            }
            else if (owner.Isa.Contains(line.Keyword))
            {
                Instruction(line);
            }
            else
            {
                throw Error($"unknown mnemonic or directive '{line.Keyword}'.");
            }
        }

        private void Define(string name, int value)
        {
            if (!symbols.TryGetValue(name, out int existing))
            {
                symbols[name] = value;
            }
            else if (!final || existing != value)
            {
                throw Error(final ? $"phase error: '{name}' changed from {existing} to {value}." : $"duplicate symbol '{name}'.");
            }
        }

        private void Instruction(SourceLine line)
        {
            int start = ProgramCounter;
            if (!final)
            {
                choices[line.Number] = FormSelector.Select(owner.Isa, line.Keyword!.ToUpperInvariant(), line.Operand, TryEvaluate);
            }

            (InstructionForm form, string[] captures) = choices[line.Number];
            int[] values = [.. captures.Select(c => TryEvaluate(c) ?? 0)];
            foreach (byte b in form.Opcode)
            {
                Emit(b);
            }

            for (int i = 0; i < values.Length; i++)
            {
                EmitField(form.Pattern.Fields[i], values[i], start + form.Size);
            }
        }

        private void EmitField(FieldKind kind, int value, int nextInstruction)
        {
            switch (kind)
            {
                case FieldKind.Byte:
                    Emit(final && value is < 0 or > byte.MaxValue ? throw Error($"value {value} out of range 0..255.") : (byte)value);
                    break;
                case FieldKind.Word:
                    if (final && value is < 0 or > ushort.MaxValue)
                    {
                        throw Error($"value {value} out of range 0..65535.");
                    }

                    (byte first, byte second) = Endianness == Endianness.Little
                        ? ((byte)value, (byte)(value >> 8))
                        : ((byte)(value >> 8), (byte)value);
                    Emit(first);
                    Emit(second);
                    break;
                default:
                    int offset = value - nextInstruction;
                    Emit(final && offset is < sbyte.MinValue or > sbyte.MaxValue
                        ? throw Error($"branch target out of range ({offset} bytes, allowed -128..127).")
                        : (byte)offset);
                    break;
            }
        }
    }
}
