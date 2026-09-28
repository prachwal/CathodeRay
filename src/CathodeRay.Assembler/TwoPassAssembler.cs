using CathodeRay.Assembler.Directives;
using CathodeRay.Assembler.Isa;
using CathodeRay.Assembler.Link;
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
        return RunPasses(
            [.. source.Split('\n').Select((text, i) => LineParser.Parse(i + 1, text.TrimEnd('\r'), dialect, IsKeyword))],
            binaryReader: null,
            [],
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            objectMode: false).Result();
    }

    /// <summary>Asembluje plik z include'ami: ekspansja przed pierwszym przebiegiem.</summary>
    /// <param name="source">Tekst pliku wejściowego.</param>
    /// <param name="entryFile">Ścieżka pliku wejściowego (baza ścieżek względnych, atrybucja błędów i listingu).</param>
    /// <param name="reader">Czyta plik o ścieżce znormalizowanej; <see langword="null"/> = brak pliku.</param>
    /// <param name="includePaths">Dodatkowe katalogi poszukiwań (odpowiednik <c>--incdir</c>).</param>
    /// <param name="binaryReader">Czyta plik binarny dla <c>.incbin</c>; <see langword="null"/> = brak kontekstu pliku.</param>
    /// <param name="segmentOrigins">Bazowe adresy segmentów (odpowiednik <c>--map</c>); brak = 0.</param>
    /// <returns>Obraz, symbole i listing.</returns>
    /// <exception cref="AssemblerException">Błąd w źródle (z plikiem).</exception>
    public AssemblyResult Assemble(string source, string entryFile, Func<string, string?> reader, IReadOnlyList<string>? includePaths = null, Func<string, byte[]?>? binaryReader = null, IReadOnlyDictionary<string, int>? segmentOrigins = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(entryFile);
        ArgumentNullException.ThrowIfNull(reader);
        return RunPasses(
            SourceLoader.Expand(source, entryFile, reader, dialect, IsKeyword, includePaths),
            binaryReader,
            includePaths ?? [],
            segmentOrigins ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            objectMode: false).Result();
    }

    /// <summary>Asembluje moduł obiektu do linkera: symbole relatywne, EXTRN jako relokacje.</summary>
    /// <param name="cpu">Nazwa celu (do nagłówka obiektu).</param>
    /// <param name="source">Tekst pliku wejściowego.</param>
    /// <param name="entryFile">Ścieżka pliku wejściowego.</param>
    /// <param name="reader">Czyta plik tekstowy; <see langword="null"/> = brak pliku.</param>
    /// <param name="includePaths">Dodatkowe katalogi poszukiwań.</param>
    /// <returns>Moduł obiektu.</returns>
    /// <exception cref="AssemblerException">Błąd w źródle (z plikiem).</exception>
    public ObjectModule AssembleObject(string cpu, string source, string entryFile, Func<string, string?> reader, IReadOnlyList<string>? includePaths = null)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(entryFile);
        ArgumentNullException.ThrowIfNull(reader);
        IReadOnlyList<SourceLine> lines = SourceLoader.Expand(source, entryFile, reader, dialect, IsKeyword, includePaths);
        Pass second = RunPasses(lines, binaryReader: null, includePaths ?? [], new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), objectMode: true);
        return second.ExportObject(cpu);
    }

    private static void ResolvePendingAssignments(Pass pass)
    {
        bool progress = true;
        while (progress && pass.Pending.Count > 0)
        {
            progress = false;
            foreach (SourceLine line in pass.Pending.ToArray())
            {
                try
                {
                    if (pass.TryDefineAssignment(line))
                    {
                        pass.Pending.Remove(line);
                        progress = true;
                    }
                }
                catch (AssemblerException e)
                {
                    pass.CollectAll(e.Errors);
                    pass.Pending.Remove(line);
                    progress = true;
                }
            }
        }
    }

    private static void ThrowIfErrors(Pass pass)
    {
        if (pass.Errors.Count > 0)
        {
            throw new AssemblerException(pass.Errors);
        }
    }

    private Pass RunPasses(IReadOnlyList<SourceLine> lines, Func<string, byte[]?>? binaryReader, IReadOnlyList<string> includePaths, IReadOnlyDictionary<string, int> origins, bool objectMode)
    {
        lines = MacroExpander.Expand(lines, dialect, IsKeyword);
        var symbols = new Dictionary<string, int>(dialect.SymbolComparer);
        var choices = new Dictionary<int, FormChoice>();
        var links = new LinkScope(dialect.SymbolComparer);
        var symbolSegments = new Dictionary<string, string>(dialect.SymbolComparer);

        var first = new Pass(this, symbols, choices, final: false, binaryReader, includePaths, origins, links, symbolSegments, objectMode);
        first.Run(lines);
        ResolvePendingAssignments(first);
        ThrowIfErrors(first);
        first.ValidateGlobals();
        ThrowIfErrors(first);

        var second = new Pass(this, symbols, choices, final: true, binaryReader, includePaths, origins, links, symbolSegments, objectMode);
        second.Run(lines);
        ThrowIfErrors(second);
        return second;
    }

    private bool IsKeyword(string word) => isa.Contains(word) || dialect.Directives.ContainsKey(word);

    private sealed class Pass(TwoPassAssembler owner, Dictionary<string, int> symbols, Dictionary<int, FormChoice> choices, bool final, Func<string, byte[]?>? binaryReader, IReadOnlyList<string> includePaths, IReadOnlyDictionary<string, int> segmentOrigins, LinkScope links, Dictionary<string, string> symbolSegments, bool objectMode)
        : IAssemblyContext
    {
        private const int MaxErrors = 20;

        private readonly byte[] _image = new byte[AddressSpace];
        private readonly bool[] _written = new bool[AddressSpace];
        private readonly List<ListingLine> _listing = [];
        private readonly List<byte> _lineBytes = [];
        private readonly List<AssemblerError> _errors = [];
        private readonly Dictionary<string, SegmentState> _segments = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _segmentOrder = [];
        private SourceLine _line = new(0, string.Empty, null, null, null);
        private int _lineStart;
        private string? _scope;
        private Stack<ScopeFrame> _scopes = new();
        private string _segment = "CODE";
        private bool _stopped;

        public int ProgramCounter
        {
            get => EnsureSegment(_segment).PC;
            private set => EnsureSegment(_segment).PC = value;
        }

        public List<SourceLine> Pending { get; } = [];

        public Endianness Endianness => owner.Isa.Endianness;

        /// <summary>Błędy zebrane w przebiegu (kolejność źródła).</summary>
        public IReadOnlyList<AssemblerError> Errors => _errors;

        /// <summary>Relokacje zebrane w przebiegu (tylko tryb obiektu).</summary>
        public List<Relocation> Relocations { get; } = [];

        /// <summary>Komunikaty <c>.out</c>/<c>.warning</c> (tylko przebieg finalny).</summary>
        public List<AsmMessage> Messages { get; } = [];

        public void Notify(string text, bool warning)
        {
            if (final)
            {
                Messages.Add(new AsmMessage(_line.File, _line.Number, text, warning));
            }
        }

        public void Run(IReadOnlyList<SourceLine> lines)
        {
            var conditionals = new Stack<ConditionalFrame>();
            _scopes = new Stack<ScopeFrame>();
            Stack<ScopeFrame> scopes = _scopes;
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
                    else if (IsScope(line))
                    {
                        HandleScope(line, scopes);
                    }
                    else if (conditionals.All(static f => f.Active))
                    {
                        Process(line, index);
                    }
                }
                catch (FormatException e)
                {
                    Collect(new AssemblerError(_line.File, _line.Number, FormatMessage(e.Message)));
                    SyncAfterError(line, index);
                }
                catch (AssemblerException e)
                {
                    CollectAll(e.Errors);
                    SyncAfterError(line, index);
                }

                int address = _lineBytes.Count > 0 ? start : ProgramCounter;
                _listing.Add(new ListingLine(line.Number, address, [.. _lineBytes], line.Text, line.File, _segment));
            }

            if (conditionals.Count > 0)
            {
                ConditionalFrame open = conditionals.Peek();
                Collect(new AssemblerError(open.File, open.Number, "unterminated .if (opened here)."));
            }

            if (scopes.Count > 0)
            {
                ScopeFrame open = scopes.Peek();
                Collect(new AssemblerError(open.File, open.Number, $"unterminated '{open.Opener}' (opened here)."));
            }
        }

        public AssemblyResult Result()
        {
            int low = Array.IndexOf(_written, true);
            int high = Array.LastIndexOf(_written, true);
            byte[] image = low < 0 ? [] : _image[low..(high + 1)];
            var spans = _segmentOrder
                .Where(name => _segments[name].Min != int.MaxValue)
                .Select(name =>
                {
                    SegmentState segment = _segments[name];
                    int end = segment.Max == int.MinValue ? segment.Min : segment.Max;
                    return new SegmentSpan(name, segment.Min, end, !segment.Emit);
                })
                .ToList();
            return new AssemblyResult(Math.Max(low, 0), image, symbols, _listing, spans, Messages);
        }

        /// <summary>Eksportuje moduł obiektu (po czystym przebiegu w trybie obiektu).</summary>
        /// <param name="cpu">Nazwa celu.</param>
        /// <returns>Moduł obiektu.</returns>
        public ObjectModule ExportObject(string cpu)
        {
            var segments = new List<ObjectSegment>();
            foreach (string name in _segmentOrder)
            {
                SegmentState segment = _segments[name];
                if (segment.Min == int.MaxValue)
                {
                    continue;
                }

                int origin = segmentOrigins.GetValueOrDefault(name, 0);
                int start = segment.Min;
                int end = segment.Max == int.MinValue ? start : segment.Max;
                int length = end - origin;
                byte[] data = new byte[Math.Max(length, 0)];
                foreach ((int offset, byte value) in segment.ObjData)
                {
                    if (offset >= 0 && offset < data.Length)
                    {
                        data[offset] = value;
                    }
                }

                segments.Add(new ObjectSegment(name, !segment.Emit, segment.Emit ? data : [], segment.Emit ? data.Length : end - start));
            }

            var symbolsOut = new List<ObjectSymbol>();
            foreach ((string name, int address) in symbols)
            {
                if (name.Contains('\0') || !symbolSegments.TryGetValue(name, out string? segment))
                {
                    continue;
                }

                int origin = segmentOrigins.GetValueOrDefault(segment, 0);
                symbolsOut.Add(new ObjectSymbol(name, segment, address - origin, links.Globals.Contains(name)));
            }

            return new ObjectModule(cpu, segments, symbolsOut, Relocations);
        }

        /// <summary>Dodaje błędy z zewnątrz pętli linii (np. przypisania odłożone).</summary>
        /// <param name="errors">Błędy.</param>
        public void CollectAll(IEnumerable<AssemblerError> errors)
        {
            foreach (AssemblerError error in errors)
            {
                Collect(error);
            }
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

        public void SetProgramCounter(int address)
        {
            if (address is < 0 or >= AddressSpace)
            {
                throw Error($"address {address} outside $0000..$FFFF.");
            }

            SegmentState segment = EnsureSegment(_segment);
            segment.PC = address;
            segment.Min = Math.Min(segment.Min, address);
            segment.Max = Math.Max(segment.Max, address);
        }

        public void SwitchSegment(string name, bool? emit)
        {
            SegmentState segment = EnsureSegment(name, emit);
            _segment = name;
            segment.Max = Math.Max(segment.Max, segment.PC);
        }

        public void Emit(byte value)
        {
            SegmentState segment = EnsureSegment(_segment);
            if (segment.PC >= AddressSpace)
            {
                throw Error("code runs past $FFFF.");
            }

            segment.Min = Math.Min(segment.Min, segment.PC);
            if (segment.Emit)
            {
                if (final)
                {
                    if (objectMode)
                    {
                        int origin = segmentOrigins.GetValueOrDefault(_segment, 0);
                        segment.ObjData[segment.PC - origin] = value;
                    }
                    else
                    {
                        if (_written[segment.PC])
                        {
                            throw Error($"overlapping output at ${segment.PC:X4}.");
                        }

                        foreach (string bssName in _segmentOrder)
                        {
                            SegmentState bss = _segments[bssName];
                            if (!bss.Emit && segment.PC >= bss.Min && segment.PC < bss.Max)
                            {
                                throw Error($"address ${segment.PC:X4} overlaps BSS segment '{bssName}'.");
                            }
                        }

                        _image[segment.PC] = value;
                        _written[segment.PC] = true;
                    }

                    _lineBytes.Add(value);
                }

                segment.PC++;
                segment.Max = Math.Max(segment.Max, segment.PC);
            }
            else
            {
                if (final && !objectMode && _written[segment.PC])
                {
                    throw Error($"address ${segment.PC:X4} overlaps BSS segment '{_segment}'.");
                }

                segment.PC++;
                segment.Max = Math.Max(segment.Max, segment.PC);
            }
        }

        /// <summary>Symbol PC (<c>*</c>, <c>$</c>) w wyrażeniu to adres początku linii, także w dyrektywach danych
        /// (<c>DW a, $</c>), jak w oryginalnych asemblerach; <see cref="ProgramCounter"/> przesuwa się przy emisji.</summary>
        public int? TryEvaluate(string expression) =>
            Expression.Evaluate(expression, owner.Dialect, _lineStart, Lookup);

        public int Evaluate(string expression) =>
            TryEvaluate(expression) ?? throw Error($"'{expression}' must be known at this point (no forward references).");

        public void Stop() => _stopped = true;

        public AssemblerException Error(string message) => new(_line.Number, FormatMessage(message), _line.File);

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

        public void DeclareGlobal(string name)
        {
            links.Globals.Add(name);
            links.GlobalSites[name] = _line;
        }

        public void DeclareExternal(string name) => links.Externals.Add(name);

        /// <summary>Sprawdza, czy każdy eksport jest zdefiniowany (po pass 1).</summary>
        public void ValidateGlobals()
        {
            foreach (string name in links.Globals)
            {
                if (!symbols.ContainsKey(name))
                {
                    SourceLine site = links.GlobalSites[name];
                    Collect(new AssemblerError(site.File, site.Number, $"exported symbol '{name}' is not defined."));
                }
            }
        }

        public int EvaluateEmission(string expression, FieldKind kind, out bool relocated)
        {
            relocated = false;
            int? value = TryEvaluate(expression);
            if (value is not null)
            {
                if (!final || !objectMode || kind == FieldKind.Constant)
                {
                    return value.Value;
                }

                string? defined = SingleSymbol(expression);
                if (defined is null)
                {
                    return value.Value;
                }

                relocated = true;
                int bas = EvalWith(expression, defined, 0);
                Relocate(kind, defined, bas);
                return bas;
            }

            if (!final)
            {
                return 0;
            }

            (string symbol, int addend) = SplitExternal(expression);
            if (kind == FieldKind.Constant)
            {
                throw Error($"'{expression}' must be known at this point (no forward references).");
            }

            if (!objectMode)
            {
                throw Error($"external symbol '{symbol}' requires object output (--format obj).");
            }

            relocated = true;
            Relocate(kind, symbol, addend);
            return addend;
        }

        private void Relocate(FieldKind kind, string symbol, int addend)
        {
            SegmentState segment = EnsureSegment(_segment);
            int origin = segmentOrigins.GetValueOrDefault(_segment, 0);
            RelocKind reloc = kind switch
            {
                FieldKind.Word => RelocKind.Abs16,
                FieldKind.Relative8 => RelocKind.Rel8,
                FieldKind.Displacement8 => RelocKind.Disp8,
                _ => RelocKind.Abs8,
            };
            Relocations.Add(
                new Relocation(
                    _segment,
                    ProgramCounter - origin,
                    reloc,
                    symbol,
                    addend));
        }

        private string? SingleSymbol(string expression)
        {
            var used = new HashSet<string>(owner.Dialect.SymbolComparer);
            Expression.Evaluate(expression, owner.Dialect, _lineStart, name =>
            {
                string key = IsLocal(name) ? ScopeKey(name) : name;
                if (symbols.ContainsKey(key) || links.Externals.Contains(name))
                {
                    used.Add(name);
                }

                return Lookup(name);
            });

            if (used.Count > 1)
            {
                throw Error($"expression '{expression}' must reference exactly one symbol.");
            }

            return used.Count == 1 ? used.First() : null;
        }

        private int EvalWith(string expression, string symbol, int probe)
        {
            return Expression.Evaluate(expression, owner.Dialect, _lineStart, name =>
                links.Externals.Contains(name)
                    ? owner.Dialect.SymbolComparer.Equals(name, symbol) ? probe : 0
                    : Lookup(name)) ?? 0;
        }

        private string FormatMessage(string message)
        {
            if (_line.Macro is { } macro)
            {
                string definedAt = macro.DefFile is null ? $"line {macro.DefLine}" : $"{macro.DefFile}:{macro.DefLine}";
                message += $" (in expansion of '{macro.Name}' defined at {definedAt})";
            }

            return message;
        }

        private string ScopeKey(string name) =>
            _scope is null
                ? throw new FormatException($"no preceding global label for '{name}'.")
                : $"{_scope}\0{name}";

        private SegmentState EnsureSegment(string name, bool? emit = null)
        {
            if (!_segments.TryGetValue(name, out SegmentState? segment))
            {
                segment = new SegmentState { PC = segmentOrigins.GetValueOrDefault(name, 0), Emit = emit ?? true };
                _segments[name] = segment;
                _segmentOrder.Add(name);
            }
            else if (emit.HasValue)
            {
                segment.Emit = emit.Value;
            }

            return segment;
        }

        private void Collect(AssemblerError error)
        {
            if (_errors.Count >= MaxErrors)
            {
                _stopped = true;
                return;
            }

            _errors.Add(error);
            if (_errors.Count == MaxErrors)
            {
                _errors.Add(new AssemblerError(error.File, error.Line, $"too many errors (showing first {MaxErrors})."));
                _stopped = true;
            }
        }

        private void SyncAfterError(SourceLine line, int index)
        {
            if (final || line.Keyword is null || choices.ContainsKey(index) || line.IsAssignment
                || owner.Dialect.Directives.ContainsKey(line.Keyword) || !owner.Isa.Contains(line.Keyword))
            {
                return;
            }

            int size = owner.Isa.FormsFor(line.Keyword.ToUpperInvariant()).Select(static f => f.Size).DefaultIfEmpty(0).Max();
            for (int i = 0; i < size && ProgramCounter < AddressSpace; i++)
            {
                ProgramCounter++;
            }
        }

        private bool IsConditional(SourceLine line) =>
            line.Keyword is not null
            && owner.Dialect.Directives.TryGetValue(line.Keyword, out IDirective? directive)
            && directive is ConditionalDirective;

        private bool IsScope(SourceLine line) =>
            line.Keyword is not null
            && owner.Dialect.Directives.TryGetValue(line.Keyword, out IDirective? directive)
            && directive is ScopeDirective;

        private void HandleScope(SourceLine line, Stack<ScopeFrame> scopes)
        {
            if (line.Label is not null)
            {
                throw Error($"label on '{line.Keyword}' is not allowed.");
            }

            var scope = (ScopeDirective)owner.Dialect.Directives[line.Keyword!];
            switch (scope.Kind)
            {
                case ScopeKind.Scope:
                case ScopeKind.Proc:
                    string? name = ScopeOperand(line);
                    if (name is null)
                    {
                        scopes.Push(new ScopeFrame($"#{line.File}:{line.Number}", line.Number, line.File, line.Keyword!, isProc: false));
                        _scope = null;
                    }
                    else
                    {
                        if (scope.Kind == ScopeKind.Proc)
                        {
                            Define(name, ProgramCounter);
                        }
                        else
                        {
                            _scope = null;
                        }

                        scopes.Push(new ScopeFrame(name, line.Number, line.File, line.Keyword!, scope.Kind == ScopeKind.Proc));
                    }

                    break;
                case ScopeKind.EndScope:
                case ScopeKind.EndProc:
                    if (scopes.Count == 0)
                    {
                        throw Error($"'{line.Keyword}' without '.scope' or '.proc'.");
                    }

                    ScopeFrame open = scopes.Pop();
                    bool wantProc = scope.Kind == ScopeKind.EndProc;
                    if (open.IsProc != wantProc)
                    {
                        throw Error($"'{line.Keyword}' closes '{open.Opener}' (use '{(open.IsProc ? ".endproc" : ".endscope")}').");
                    }

                    _scope = null;
                    break;
            }
        }

        private string? ScopeOperand(SourceLine line)
        {
            if (line.Operand is null)
            {
                return null;
            }

            string name = line.Operand.Trim();
            return Expression.IsIdentifier(name) ? name : throw Error($"invalid scope name '{line.Operand}'.");
        }

        private string ScopePath(Stack<ScopeFrame> scopes) =>
            string.Join("::", scopes.Reverse().Select(static f => f.Name));

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
                case ConditionalKind.IfBlank:
                case ConditionalKind.IfNBlank:
                    bool outer = conditionals.All(static f => f.Active);
                    bool blank = line.Operand is null || line.Operand.Trim().Length == 0;
                    bool branch = conditional.Kind == ConditionalKind.IfBlank ? blank : !blank;
                    conditionals.Push(new ConditionalFrame(line.Number, line.File, outer, outer && branch, elseSeen: false, outer && branch));
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

        private bool IsLocal(string name) => name.Length > 1 && name[0] == '@';

        private int? Lookup(string name)
        {
            if (name.Contains("::", StringComparison.Ordinal))
            {
                return symbols.TryGetValue(name, out int absolute) ? absolute
                    : links.Externals.Contains(name) ? null
                    : final ? throw new FormatException($"undefined symbol '{name}'.") : null;
            }

            string key = IsLocal(name) ? ScopeKey(name) : Qualify(name);
            if (symbols.TryGetValue(key, out int value))
            {
                return value;
            }

            if (links.Externals.Contains(name))
            {
                return null;
            }

            return final ? throw new FormatException($"undefined symbol '{name}'.") : null;
        }

        private string Qualify(string name)
        {
            foreach (string prefix in ScopePrefixes())
            {
                string candidate = prefix.Length == 0 ? name : $"{prefix}::{name}";
                if (symbols.ContainsKey(candidate))
                {
                    return candidate;
                }
            }

            return ScopePath(_scopes).Length == 0 ? name : $"{ScopePath(_scopes)}::{name}";
        }

        private IEnumerable<string> ScopePrefixes()
        {
            string[] frames = [.. _scopes.Reverse().Select(static f => f.Name)];
            for (int depth = frames.Length; depth >= 0; depth--)
            {
                yield return string.Join("::", frames[..depth]);
            }
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
                if (links.Externals.Contains(name))
                {
                    throw Error($"'{name}' is declared external.");
                }

                name = ScopePath(_scopes).Length == 0 ? name : $"{ScopePath(_scopes)}::{name}";
                display = name;
                _scope = name;
            }
            else
            {
                name = ScopeKey(name);
            }

            symbolSegments[name] = _segment;
            if (!symbols.TryGetValue(name, out int existing))
            {
                symbols[name] = value;
            }
            else if (!final || existing != value)
            {
                throw Error(final ? $"phase error: '{display}' changed from {existing} to {value}." : $"duplicate symbol '{display}'.");
            }
        }

        private (string Symbol, int Addend) SplitExternal(string expression)
        {
            var used = new HashSet<string>(owner.Dialect.SymbolComparer);
            int? At(int probe, string skip)
            {
                used.Clear();
                return Expression.Evaluate(expression, owner.Dialect, _lineStart, name =>
                {
                    if (links.Externals.Contains(name))
                    {
                        used.Add(name);
                        return owner.Dialect.SymbolComparer.Equals(name, skip) ? probe : 0;
                    }

                    return Lookup(name);
                });
            }

            int? baseValue = At(0, string.Empty);
            if (used.Count != 1)
            {
                throw Error($"expression '{expression}' must reference exactly one external symbol.");
            }

            string symbol = used.First();
            int? sloped = At(1, symbol);
            if (baseValue is null || sloped is null || sloped - baseValue != 1)
            {
                throw Error($"expression with '{symbol}' is not relocatable (use symbol ± constant).");
            }

            return (symbol, baseValue.Value);
        }

        private void Instruction(SourceLine line, int index)
        {
            int start = ProgramCounter;
            if (!final)
            {
                choices[index] = FormSelector.Select(owner.Isa, line.Keyword!.ToUpperInvariant(), line.Operand, TryEvaluate);
            }

            (InstructionForm form, string[] captures) = choices[index];
            foreach (EncodingPart part in form.Encoding)
            {
                if (part.IsField)
                {
                    FieldKind kind = form.Pattern.Fields[part.Field];
                    int value = EvaluateEmission(captures[part.Field], kind, out bool relocated);
                    EmitField(kind, value, start + form.Size, !relocated);
                }
                else
                {
                    Emit(part.Literal);
                }
            }
        }

        private void EmitField(FieldKind kind, int value, int nextInstruction, bool checkRange = true)
        {
            bool check = final && checkRange;
            switch (kind)
            {
                case FieldKind.Byte:
                    Emit(check && value is < 0 or > byte.MaxValue ? throw Error($"value {value} out of range 0..255.") : (byte)value);
                    break;
                case FieldKind.Word:
                    if (check && value is < 0 or > ushort.MaxValue)
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
                    Emit(check && value is < sbyte.MinValue or > sbyte.MaxValue
                        ? throw Error($"displacement {value} out of range -128..127.")
                        : (byte)value);
                    break;
                default:
                    int offset = checkRange ? value - nextInstruction : value;
                    Emit(check && offset is < sbyte.MinValue or > sbyte.MaxValue
                        ? throw Error($"branch target out of range ({offset} bytes, allowed -128..127).")
                        : (byte)offset);
                    break;
            }
        }

        private sealed class SegmentState
        {
            public int PC { get; set; }

            public bool Emit { get; set; } = true;

            public int Min { get; set; } = int.MaxValue;

            public int Max { get; set; } = int.MinValue;

            public Dictionary<int, byte> ObjData { get; } = new();
        }

        private sealed class ScopeFrame(string name, int number, string? file, string opener, bool isProc)
        {
            public string Name => name;

            public int Number => number;

            public string? File => file;

            public string Opener => opener;

            public bool IsProc => isProc;
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
