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

    /// <summary>Asembluje źródło (jeden tekst, bez includów; <c>.include</c> wymaga przeciążenia z plikiem).</summary>
    /// <param name="source">Tekst źródła.</param>
    /// <returns>Obraz, symbole i listing.</returns>
    /// <exception cref="AssemblerException">Błąd w źródle.</exception>
    public AssemblyResult Assemble(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return AssembleCore([.. source.Split('\n').Select((text, i) =>
            LineParser.Parse(i + 1, text.TrimEnd('\r'), dialect, IsKeyword))]);
    }

    /// <summary>Asembluje plik z include'ami: ekspansja przed pierwszym przebiegiem.</summary>
    /// <param name="source">Tekst pliku wejściowego.</param>
    /// <param name="entryFile">Ścieżka pliku wejściowego (baza ścieżek względnych, atrybucja błędów i listingu).</param>
    /// <param name="reader">Czyta plik o ścieżce znormalizowanej; <see langword="null"/> = brak pliku.</param>
    /// <param name="includePaths">Dodatkowe katalogi poszukiwań (odpowiednik <c>--incdir</c>).</param>
    /// <param name="binaryReader">Czyta plik binarny dla <c>.incbin</c>; <see langword="null"/> = brak kontekstu pliku.</param>
    /// <returns>Obraz, symbole i listing.</returns>
    /// <exception cref="AssemblerException">Błąd w źródle (z plikiem).</exception>
    public AssemblyResult Assemble(string source, string entryFile, Func<string, string?> reader, IReadOnlyList<string>? includePaths = null, Func<string, byte[]?>? binaryReader = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(entryFile);
        ArgumentNullException.ThrowIfNull(reader);
        return AssembleCore(SourceLoader.Expand(source, entryFile, reader, dialect, IsKeyword, includePaths), binaryReader, includePaths ?? []);
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

    private AssemblyResult AssembleCore(IReadOnlyList<SourceLine> lines, Func<string, byte[]?>? binaryReader = null, IReadOnlyList<string>? includePaths = null)
    {
        lines = MacroExpander.Expand(lines, dialect, IsKeyword);
        var symbols = new Dictionary<string, int>(dialect.SymbolComparer);
        var choices = new Dictionary<int, FormChoice>();

        var first = new Pass(this, symbols, choices, final: false, binaryReader, includePaths ?? []);
        first.Run(lines);
        ResolvePendingAssignments(first);
        var second = new Pass(this, symbols, choices, final: true, binaryReader, includePaths ?? []);
        second.Run(lines);
        return second.Result();
    }

    private sealed class Pass(TwoPassAssembler owner, Dictionary<string, int> symbols, Dictionary<int, FormChoice> choices, bool final, Func<string, byte[]?>? binaryReader, IReadOnlyList<string> includePaths)
        : IAssemblyContext
    {
        private readonly byte[] _image = new byte[AddressSpace];
        private readonly bool[] _written = new bool[AddressSpace];
        private readonly List<ListingLine> _listing = [];
        private readonly List<byte> _lineBytes = [];
        private SourceLine _line = new(0, string.Empty, null, null, null);
        private int _lineStart;
        private string? _scope;
        private bool _stopped;

        public int ProgramCounter { get; private set; }

        public Endianness Endianness => owner.Isa.Endianness;

        public List<SourceLine> Pending { get; } = [];

        public void Run(IReadOnlyList<SourceLine> lines)
        {
            var conditionals = new Stack<ConditionalFrame>();
            for (int index = 0; index < lines.Count && !_stopped; index++)
            {
                SourceLine line = lines[index];
                _line = line;
                _lineBytes.Clear();
                int start = ProgramCounter;
                _lineStart = start;
                try
                {
                    if (IsConditional(line))
                    {
                        HandleConditional(line, conditionals);
                    }
                    else if (conditionals.All(static f => f.Active))
                    {
                        Process(line, index);
                    }
                }
                catch (FormatException e)
                {
                    throw Error(e.Message);
                }

                int address = _lineBytes.Count > 0 ? start : ProgramCounter;
                _listing.Add(new ListingLine(line.Number, address, [.. _lineBytes], line.Text, line.File));
            }

            if (conditionals.Count > 0)
            {
                ConditionalFrame open = conditionals.Peek();
                throw new AssemblerException(open.Number, $"unterminated .if (opened here).", open.File);
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

        /// <summary>Symbol PC (<c>*</c>, <c>$</c>) w wyrażeniu to adres początku linii, także w dyrektywach danych
        /// (<c>DW a, $</c>), jak w oryginalnych asemblerach; <see cref="ProgramCounter"/> przesuwa się przy emisji.</summary>
        public int? TryEvaluate(string expression) =>
            Expression.Evaluate(expression, owner.Dialect, _lineStart, Lookup);

        public int Evaluate(string expression) =>
            TryEvaluate(expression) ?? throw Error($"'{expression}' must be known at this point (no forward references).");

        public void Stop() => _stopped = true;

        public AssemblerException Error(string message)
        {
            if (_line.Macro is { } macro)
            {
                string definedAt = macro.DefFile is null ? $"line {macro.DefLine}" : $"{macro.DefFile}:{macro.DefLine}";
                message += $" (in expansion of '{macro.Name}' defined at {definedAt})";
            }

            return new(_line.Number, message, _line.File);
        }

        public byte[] ReadBinaryFile(string? operand)
        {
            if (binaryReader is null || _line.File is null)
            {
                throw Error(".incbin needs file context (assemble a file via CLI, not bare text).");
            }

            string name = FileResolve.Unquote(operand, ".incbin", Error);
            foreach (string candidate in FileResolve.Candidates(name, _line.File, includePaths))
            {
                if (binaryReader(candidate) is { } data)
                {
                    return data;
                }
            }

            throw Error($"binary file '{name}' not found.");
        }

        private static bool IsLocal(string name) => name.Length > 1 && name[0] == '@';

        private string ScopeKey(string name) =>
            _scope is null
                ? throw new FormatException($"no preceding global label for '{name}'.")
                : $"{_scope}\0{name}";

        private bool IsConditional(SourceLine line) =>
            line.Keyword is not null
            && owner.Dialect.Directives.TryGetValue(line.Keyword, out IDirective? directive)
            && directive is ConditionalDirective;

        private void HandleConditional(SourceLine line, Stack<ConditionalFrame> conditionals)
        {
            if (line.Label is not null)
            {
                Define(line.Label, ProgramCounter);
            }

            var conditional = (ConditionalDirective)owner.Dialect.Directives[line.Keyword!];
            switch (conditional.Kind)
            {
                case ConditionalKind.If:
                    bool parent = conditionals.All(static f => f.Active);
                    bool taken = parent && EvaluateCondition(line);
                    conditionals.Push(new ConditionalFrame(line.Number, line.File, parent, taken, elseSeen: false, taken));
                    break;
                case ConditionalKind.ElseIf:
                    ConditionalFrame elif = PopConditional(conditionals, "'.elseif' without '.if'.");
                    if (elif.ElseSeen)
                    {
                        throw Error("'.elseif' after '.else'.");
                    }

                    elif.Active = elif.ParentActive && !elif.Taken && EvaluateCondition(line);
                    elif.Taken |= elif.Active;
                    conditionals.Push(elif);
                    break;
                case ConditionalKind.Else:
                    ConditionalFrame els = PopConditional(conditionals, "'.else' without '.if'.");
                    if (els.ElseSeen)
                    {
                        throw Error("multiple '.else'.");
                    }

                    els.ElseSeen = true;
                    els.Active = els.ParentActive && !els.Taken;
                    els.Taken = true;
                    conditionals.Push(els);
                    break;
                default:
                    PopConditional(conditionals, "'.endif' without '.if'.");
                    break;
            }
        }

        private ConditionalFrame PopConditional(Stack<ConditionalFrame> conditionals, string message) =>
            conditionals.Count > 0 ? conditionals.Pop() : throw Error(message);

        private bool EvaluateCondition(SourceLine line) =>
            Evaluate(line.Operand ?? throw Error($"'{line.Keyword}' needs a condition.")) != 0;

        private int? Lookup(string name)
        {
            string key = IsLocal(name) ? ScopeKey(name) : name;
            return symbols.TryGetValue(key, out int value) ? value : final ? throw new FormatException($"undefined symbol '{name}'.") : null;
        }

        private void Process(SourceLine line, int index)
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
                Instruction(line, index);
            }
            else
            {
                throw Error($"unknown mnemonic or directive '{line.Keyword}'.");
            }
        }

        private void Define(string name, int value)
        {
            string display = name;
            if (!IsLocal(name))
            {
                _scope = name;
            }
            else
            {
                name = ScopeKey(name);
            }

            if (!symbols.TryGetValue(name, out int existing))
            {
                symbols[name] = value;
            }
            else if (!final || existing != value)
            {
                throw Error(final ? $"phase error: '{display}' changed from {existing} to {value}." : $"duplicate symbol '{display}'.");
            }
        }

        private void Instruction(SourceLine line, int index)
        {
            int start = ProgramCounter;
            if (!final)
            {
                choices[index] = FormSelector.Select(owner.Isa, line.Keyword!.ToUpperInvariant(), line.Operand, TryEvaluate);
            }

            (InstructionForm form, string[] captures) = choices[index];
            int[] values = [.. captures.Select(c => TryEvaluate(c) ?? 0)];
            foreach (EncodingPart part in form.Encoding)
            {
                if (part.IsField)
                {
                    EmitField(form.Pattern.Fields[part.Field], values[part.Field], start + form.Size);
                }
                else
                {
                    Emit(part.Literal);
                }
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
                case FieldKind.Constant:
                    break;
                case FieldKind.Displacement8:
                    Emit(final && value is < sbyte.MinValue or > sbyte.MaxValue
                        ? throw Error($"displacement {value} out of range -128..127.")
                        : (byte)value);
                    break;
                default:
                    int offset = value - nextInstruction;
                    Emit(final && offset is < sbyte.MinValue or > sbyte.MaxValue
                        ? throw Error($"branch target out of range ({offset} bytes, allowed -128..127).")
                        : (byte)offset);
                    break;
            }
        }

        private sealed class ConditionalFrame(
            int number,
            string? file,
            bool parentActive,
            bool taken,
            bool elseSeen,
            bool active)
        {
            public int Number => number;

            public string? File => file;

            public bool ParentActive => parentActive;

            public bool Taken { get; set; } = taken;

            public bool ElseSeen { get; set; } = elseSeen;

            public bool Active { get; set; } = active;
        }
    }
}
