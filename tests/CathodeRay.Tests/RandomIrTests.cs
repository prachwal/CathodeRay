using CathodeRay.C;
using FluentAssertions;
using Xunit.Abstractions;

namespace CathodeRay.Tests;

/// <summary>Plan 33, krok 16: losowe programy IR (budowane wprost, nie z C) z pętlami (etykieta i <see cref="Ir.BrCmp"/> wstecz),
/// <c>goto</c> w przód, wołaniami bezpośrednimi i przez wskaźnik, rekurencją o ograniczonej głębokości, wskaźnikami do tablic
/// (<see cref="Ir.Load"/>/<see cref="Ir.Store"/> z <see cref="Ir.AddrOf"/> i przesunięciami) na komórkach 1, 2 i 4 B. Wyrocznią
/// jest <see cref="IrInterpreter"/> (nie zna rejestrów ani selektora); każdy program biegnie na wszystkich celach z runnerem przy
/// <c>optimize</c> true i false. Seed stały, wartości oczekiwane nie są wpisane.</summary>
public sealed class RandomIrTests
{
    private const int Programs = 200;

    private readonly ITestOutputHelper _output;

    public RandomIrTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Random_Programs_Match_The_Interpreter()
    {
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, Programs, seed =>
        {
            try
            {
                Generator.Create(seed).AssertConforms($"RandomIr seed {seed}");
            }
            catch (Exception e) when (e is Xunit.Sdk.XunitException or InvalidOperationException)
            {
                failures.Add($"seed {seed}: {e.Message}");
            }
        });

        failures.Should().BeEmpty($"{failures.Count} z {Programs} programów: {string.Join(" | ", failures.Order(StringComparer.Ordinal).Take(5))}");
    }

    /// <summary>Diagnostyka: <c>RIR_SEED=n</c> drukuje program IR tego seeda, a <c>RIR_CPU=z80</c> także jego asembler.</summary>
    [Fact]
    public void Print_Program_For_Seed_From_Environment()
    {
        string? env = Environment.GetEnvironmentVariable("RIR_SEED");
        if (env is null)
        {
            return;
        }

        Ir.Module module = Generator.Create(int.Parse(env, System.Globalization.CultureInfo.InvariantCulture)).Build();
        foreach (Ir.Function f in module.Functions)
        {
            _output.WriteLine($"== {f.Name} params {string.Join(",", f.Params)} ret {f.RetW} saved {string.Join(",", f.Saved)}");
            foreach (Ir.Ins ins in f.Body)
            {
                _output.WriteLine("  " + ins.ToString().Replace("CathodeRay.C.Ir+", string.Empty, StringComparison.Ordinal));
            }
        }

        foreach (ICTarget t in TargetHarness.Targets)
        {
            if (t.Name == Environment.GetEnvironmentVariable("RIR_CPU"))
            {
                _output.WriteLine(t.Emit(module, true));
            }
        }
    }

    [Theory]
    [InlineData("z80")]
    [InlineData("8080")]
    public void Allocator_Assigns_Registers_In_Random_Programs(string cpu)
    {
        var target = (ByteTarget)CTargets.Find(cpu)!;
        int functions = 0;
        int withRegisters = 0;
        int savingCalls = 0;
        for (int seed = 0; seed < Programs; seed++)
        {
            Ir.Module module = Generator.Create(seed).Build();
            Ir.Module legal = ParamAlias.Run(Legalizer.Run(WideLegalizer.Run(Legalizer.Run(CaseFold.Apply(module), wide: true), TargetByteOrder.Little, keepArithmetic: true)));
            ByteIsa isa = target.CreateIsa();
            Dictionary<string, string> map = RegisterAllocator.Run(legal, isa);
            RegisterAllocator.Tune(legal, isa);
            functions += legal.Functions.Count;
            withRegisters += legal.Functions.Count(f => f.Body.SelectMany(IrFacts.Operands).OfType<Ir.Cell>().Any(c => map.ContainsKey(IrLiveness.BaseSymbol(c.Sym))));
            savingCalls += legal.Functions.SelectMany(static f => f.Body).OfType<Ir.Call>().Count(c => isa.Cells.SavedAround(c).Count > 0);
        }

        _output.WriteLine($"{cpu}: {withRegisters} z {functions} funkcji w {Programs} programach dostało rejestr, {savingCalls} wołań z push/pop par");
        withRegisters.Should().BeGreaterThan(functions / 2, "generator ma ćwiczyć przydział rejestrów, nie tylko pamięć");
        savingCalls.Should().BePositive("generator ma ćwiczyć rejestry żywe przez wołanie");
    }

    /// <summary>Generator jednego programu: <c>main</c> woła funkcje pomocnicze (graf wołań bez cykli: funkcja woła tylko dalsze
    /// na liście) i najwyżej jedną funkcję rekurencyjną (parametr <c>n</c> maleje do zera), na końcu zapisuje komórki <c>main</c>,
    /// globalne i całe tablice w slotach wyników.</summary>
    private sealed class Generator
    {
        private const int StepBudget = 1_500;

        private static readonly int[] Interesting = [0, 1, 2, 3, 0x7F, 0x80, 0xFF, 0x100, 0x7FFF, 0x8000, 0xFFFF, 0x10000, 0x7FFFFFFF, unchecked((int)0x80000000), -1];

        private static readonly Ir.BinOp[] MulDiv = [Ir.BinOp.Mul, Ir.BinOp.Div, Ir.BinOp.Mod];

        private static readonly Ir.BinOp[] Shifts = [Ir.BinOp.Shl, Ir.BinOp.Shr, Ir.BinOp.Sar];

        private static readonly Ir.BinOp[] Logic = [Ir.BinOp.Add, Ir.BinOp.Sub, Ir.BinOp.And, Ir.BinOp.Or, Ir.BinOp.Xor];

        private static readonly (string Sym, int Element, int Count)[] Arrays = [("ra0", 1, 8), ("ra1", 2, 8), ("ra2", 4, 4)];

        private readonly Random _random;

        private readonly IrProgram _program = new();

        private readonly List<Ir.Cell> _globals = [];

        private readonly List<Signature> _signatures = [];

        private List<Ir.Ins> _body = [];

        private List<Ir.Cell> _readable = [];

        private List<Ir.Cell> _writable = [];

        private List<Ir.Cell> _counters = [];

        private List<Ir.Cell> _pointers = [];

        private Ir.Cell _fp = new(string.Empty, 2);

        private Ir.Cell _ix = new(string.Empty, 1);

        private Stack<string> _forward = new();

        private int _self;

        private int _loops;

        private int _label;

        private Generator(int seed) => _random = new Random(seed);

        /// <summary>Program dla seeda; gdy interpreter wykona więcej niż <see cref="StepBudget"/> instrukcji, bierze następny wariant
        /// (deterministycznie), żeby 200 programów × 6 celów × 2 przebiegi mieściło się w czasie.</summary>
        public static IrProgram Create(int seed)
        {
            for (int attempt = 0; ; attempt++)
            {
                IrProgram program = new Generator((seed * 7919) + attempt).Generate();
                var interpreter = IrInterpreter.Load([program.Build()], StepBudget);
                try
                {
                    interpreter.RunMain();
                    return program;
                }
                catch (InvalidOperationException) when (interpreter.Steps > StepBudget)
                {
                }
            }
        }

        private static Ir.Data Bss(string sym, int size) => new(sym, "BSS", size, null, false);

        private static int Log2(int value) => value == 4 ? 2 : value / 2;

        private IrProgram Generate()
        {
            foreach ((string sym, int element, int count) in Arrays)
            {
                _program.AddData(Bss(sym, element * count));
            }

            for (int i = 0; i < 3; i++)
            {
                _globals.Add(Cell($"rg{i}", Width()));
            }

            int helpers = _random.Next(1, 4);
            int recursive = _random.Next(2) == 0 ? _random.Next(helpers + 1) : -1;
            for (int i = 0; i <= helpers; i++)
            {
                if (i == recursive)
                {
                    _signatures.Add(NewSignature("rr", true));
                }
                else if (i < helpers || recursive < 0)
                {
                    _signatures.Add(NewSignature($"rf{i}", false));
                }
            }

            for (int i = 0; i < _signatures.Count; i++)
            {
                _program.AddFunction(Function(i));
            }

            List<Ir.Cell> locals = Begin("main", [], -1);
            Block(0, 6);
            _program.Emit([.. _body]);
            foreach (Ir.Cell cell in locals.Concat(_globals))
            {
                _program.Record(cell, cell.Sym);
            }

            foreach ((string sym, int element, int count) in Arrays)
            {
                Ir.Cell probe = Cell($"main__probe{element}", element);
                for (int i = 0; i < count; i++)
                {
                    _program.Emit(new Ir.Load(probe, new Ir.AddrOf(sym, i * element), 0, element)).Record(probe, $"{sym}[{i}]");
                }
            }

            return _program;
        }

        private Signature NewSignature(string name, bool recursive)
        {
            List<Ir.Cell> parameters = recursive ? [Cell($"{name}__n", 1)] : [];
            int count = _random.Next(recursive ? 0 : 1, 3);
            for (int i = 0; i < count; i++)
            {
                parameters.Add(Cell($"{name}__a{i}", Width()));
            }

            return new Signature(name, parameters, _random.Next(4) switch { 0 => 0, 1 => 1, 2 => 2, _ => 4 }, recursive);
        }

        /// <summary>Ciało funkcji; rekurencyjna: <c>if (n &lt; 1) goto base; m = n - 1; wynik = rr(m, …)</c> raz albo dwa razy, a
        /// <see cref="Ir.Function.Saved"/> jak w <c>Lowering.Frames</c>: parametry i komórki żywe za którymś wołaniem.</summary>
        private Ir.Function Function(int index)
        {
            Signature signature = _signatures[index];
            List<Ir.Cell> locals = Begin(signature.Name, signature.Params, index);
            Block(0, signature.Recursive ? 3 : 5);
            if (signature.Recursive)
            {
                string bottom = NewLabel();
                Ir.Cell m = Cell($"{signature.Name}__m", 1);
                locals.Add(m);
                _readable.Add(m);
                _body.Add(new Ir.BrCmp(Ir.Cond.Ltu, signature.Params[0], new Ir.Imm(1, 1), bottom));
                _body.Add(new Ir.Bin(Ir.BinOp.Sub, m, signature.Params[0], new Ir.Imm(1, 1)));
                _forward.Push(bottom);
                int calls = _random.Next(1, 3);
                for (int i = 0; i < calls; i++)
                {
                    _body.Add(new Ir.Call(signature.Name, null, [m, .. signature.Params.Skip(1).Select(p => Operand(p.W, exact: true))], [.. signature.Params.Select(static p => p.W)], Result(signature.RetW)));
                    Block(1, 2);
                }

                _forward.Pop();
                _body.Add(new Ir.Label(bottom));
                Block(0, 2);
            }

            _body.Add(new Ir.Ret(signature.RetW == 0 ? null : Operand(signature.RetW, exact: true), signature.RetW));
            List<Ir.Owned> saved = [];
            if (signature.Recursive)
            {
                Dictionary<string, int> sizes = locals.Concat(signature.Params).ToDictionary(static c => c.Sym, static c => c.W, StringComparer.Ordinal);
                int Size(string sym) => sizes.GetValueOrDefault(sym);
                var live = IrLiveness.Of(_body, Size);
                var across = new HashSet<string>(signature.Params.Select(static p => p.Sym), StringComparer.Ordinal);
                for (int i = 0; i < _body.Count; i++)
                {
                    if (_body[i] is Ir.Call call)
                    {
                        string? result = IrLiveness.Killed(call, Size);
                        across.UnionWith(live.LiveOut(i).Where(s => s != result && sizes.ContainsKey(s)));
                    }
                }

                saved.AddRange(across.Order(StringComparer.Ordinal).Select(s => new Ir.Owned(s, sizes[s], false)));
            }

            return new Ir.Function(signature.Name, _random.Next(2) == 0, signature.Params, signature.RetW, saved, [.. _body]);
        }

        /// <summary>Stan generatora na nową funkcję: komórki lokalne (część zainicjowana na początku, reszta czytana przed zapisem),
        /// wskaźniki, indeks, adres funkcji i liczniki pętli; zwraca lokalne (bez parametrów).</summary>
        private List<Ir.Cell> Begin(string name, IReadOnlyList<Ir.Cell> parameters, int index)
        {
            _body = [];
            _forward = new Stack<string>();
            _self = index;
            _loops = 0;
            List<Ir.Cell> locals = [];
            int count = _random.Next(2, index >= 0 && _signatures[index].Recursive ? 4 : 6);
            for (int i = 0; i < count; i++)
            {
                locals.Add(Cell($"{name}__v{i}", Width()));
            }

            // wskaźniki i adres funkcji tylko pod wskaźnikiem/wołaniem: adresy różnią się między interpreterem a celem
            _pointers = [Cell($"{name}__p0", 2), Cell($"{name}__p1", 2)];
            _fp = Cell($"{name}__fp", 2);
            _ix = Cell($"{name}__ix", 1);
            _counters = [Cell($"{name}__c0", _random.Next(2) + 1), Cell($"{name}__c1", 1)];
            bool recursive = index >= 0 && _signatures[index].Recursive;
            _writable = [.. locals, _ix, .. parameters.Skip(recursive ? 1 : 0), .. _globals];
            _readable = [.. _writable, .. _counters, .. parameters.Take(recursive ? 1 : 0)];
            foreach (Ir.Cell cell in locals.Where(_ => _random.Next(5) > 0))
            {
                _body.Add(new Ir.Mov(cell, Imm(cell.W)));
            }

            return [.. locals, _ix, .. _counters];
        }

        private void Block(int depth, int size)
        {
            int count = _random.Next(1, size + 1);
            for (int i = 0; i < count; i++)
            {
                int kind = _random.Next(100);
                if (kind < 35)
                {
                    Arithmetic();
                }
                else if (kind < 55)
                {
                    Memory();
                }
                else if (kind < 67 && depth < 3)
                {
                    string skip = NewLabel();
                    _body.Add(Branch(skip));
                    _forward.Push(skip);
                    Block(depth + 1, 3);
                    _forward.Pop();
                    _body.Add(new Ir.Label(skip));
                }
                else if (kind < 75 && depth < 3 && _loops < _counters.Count)
                {
                    Loop(depth);
                }
                else if (kind < 88)
                {
                    Call();
                }
                else if (kind < 93 && _forward.Count > 0)
                {
                    // goto w przód do etykiety zamykającej któryś z otaczających bloków
                    string target = _forward.ElementAt(_random.Next(_forward.Count));
                    _body.Add(_random.Next(2) == 0 ? new Ir.Jmp(target) : Branch(target));
                }
                else if (kind < 96 && _self >= 0)
                {
                    string skip = NewLabel();
                    Signature signature = _signatures[_self];
                    _body.Add(Branch(skip));
                    _body.Add(new Ir.Ret(signature.RetW == 0 ? null : Operand(signature.RetW, exact: true), signature.RetW));
                    _body.Add(new Ir.Label(skip));
                }
                else
                {
                    Arithmetic();
                }
            }
        }

        /// <summary>Pętla z licznikiem (komórka tylko do odczytu w ciele): w górę do stałej albo w dół do zera; <c>continue</c>
        /// przez etykietę przed krokiem licznika.</summary>
        private void Loop(int depth)
        {
            Ir.Cell counter = _counters[_loops++];
            string top = NewLabel();
            string next = NewLabel();
            int bound = _random.Next(1, 5);
            bool down = _random.Next(2) == 0;
            _body.Add(new Ir.Mov(counter, new Ir.Imm(down ? bound : 0, counter.W)));
            _body.Add(new Ir.Label(top));
            _forward.Push(next);
            Block(depth + 1, 4);
            _forward.Pop();
            _body.Add(new Ir.Label(next));
            _body.Add(new Ir.Bin(down ? Ir.BinOp.Sub : Ir.BinOp.Add, counter, counter, new Ir.Imm(1, counter.W)));
            _body.Add(down
                ? new Ir.BrCmp(Ir.Cond.Ne, counter, new Ir.Imm(0, counter.W), top)
                : new Ir.BrCmp(Ir.Cond.Ltu, counter, new Ir.Imm(bound, counter.W), top));
            _loops--;
        }

        private void Arithmetic()
        {
            Ir.Cell dst = Pick(_writable);
            int w = dst.W;
            switch (_random.Next(10))
            {
                case 0:
                    _body.Add(new Ir.Mov(dst, _random.Next(3) == 0 ? Imm(w) : Pick(_readable)));
                    break;
                case 1:
                    _body.Add(new Ir.Un(_random.Next(2) == 0 ? Ir.UnOp.Neg : Ir.UnOp.Cpl, dst, Operand(w, exact: false)));
                    break;
                case 2:
                {
                    Ir.BinOp op = MulDiv[_random.Next(MulDiv.Length)];
                    if (w > 1 && _random.Next(2) == 0 && op != Ir.BinOp.Mul)
                    {
                        op = op == Ir.BinOp.Div ? Ir.BinOp.DivS : Ir.BinOp.ModS;
                    }

                    bool exact = op is Ir.BinOp.DivS or Ir.BinOp.ModS;
                    _body.Add(new Ir.Bin(op, dst, Operand(w, exact), Operand(w, exact)));
                    break;
                }

                case 3 when w <= 2:
                {
                    Ir.BinOp op = Shifts[_random.Next(w == 2 ? 3 : 2)];
                    Ir.Op amount = new Ir.Imm(_random.Next(w * 8), 1);
                    if (_random.Next(2) == 0)
                    {
                        _body.Add(new Ir.Mov(_ix, Pick(_readable)));
                        _body.Add(new Ir.Bin(Ir.BinOp.And, _ix, _ix, new Ir.Imm((w * 8) - 1, 1)));
                        amount = _ix;
                    }

                    _body.Add(new Ir.Bin(op, dst, Operand(w, exact: op == Ir.BinOp.Sar), amount));
                    break;
                }

                default:
                {
                    Ir.BinOp op = Logic[_random.Next(Logic.Length)];
                    Ir.Op a = _random.Next(3) == 0 ? dst : Operand(w, exact: false);
                    _body.Add(new Ir.Bin(op, dst, a, Operand(w, exact: false)));
                    break;
                }
            }
        }

        /// <summary>Odczyt albo zapis elementu tablicy: adres stały, wskaźnik na element albo wskaźnik z indeksu obciętego maską
        /// (zawsze w granicach); czasem odczyt słowa do komórki wskaźnika (wynik i wskaźnik w tej samej komórce).</summary>
        private void Memory()
        {
            (string sym, int element, int count) = Arrays[_random.Next(Arrays.Length)];
            Ir.Cell pointer = Pick(_pointers);
            Ir.Op address;
            int offset;
            switch (_random.Next(3))
            {
                case 0:
                    address = new Ir.AddrOf(sym, _random.Next(count) * element);
                    offset = 0;
                    break;
                case 1:
                {
                    int at = _random.Next(count);
                    _body.Add(new Ir.Mov(pointer, new Ir.AddrOf(sym, at * element)));
                    address = pointer;
                    offset = _random.Next(count - at) * element;
                    break;
                }

                default:
                {
                    _body.Add(new Ir.Mov(_ix, Pick(_readable)));
                    _body.Add(new Ir.Bin(Ir.BinOp.And, _ix, _ix, new Ir.Imm((count / 2) - 1, 1)));
                    if (element > 1)
                    {
                        _body.Add(new Ir.Bin(Ir.BinOp.Shl, _ix, _ix, new Ir.Imm(Log2(element), 1)));
                    }

                    _body.Add(new Ir.Bin(Ir.BinOp.Add, pointer, new Ir.AddrOf(sym, 0), _ix));
                    address = pointer;
                    offset = _random.Next(count / 2) * element;
                    break;
                }
            }

            List<Ir.Cell> wide = [.. _writable.Where(c => c.W >= element)];
            if (wide.Count == 0 || _random.Next(2) == 0)
            {
                _body.Add(new Ir.Store(address, offset, Operand(element, exact: true), element));
                return;
            }

            Ir.Cell dst = (element == 2 && address == pointer && _random.Next(3) == 0) ? pointer : Pick(wide);
            _body.Add(new Ir.Load(dst, address, offset, element));
        }

        /// <summary>Wołanie funkcji dalszej na liście (bezpośrednio albo przez komórkę z adresem); rekurencyjna dostaje głębokość 0..3.</summary>
        private void Call()
        {
            int first = _self + 1;
            if (first >= _signatures.Count)
            {
                Arithmetic();
                return;
            }

            Signature callee = _signatures[_random.Next(first, _signatures.Count)];
            List<Ir.Op> args = [];
            foreach (Ir.Cell param in callee.Params)
            {
                args.Add(Operand(param.W, exact: true));
            }

            if (callee.Recursive)
            {
                if (_random.Next(2) == 0)
                {
                    args[0] = new Ir.Imm(_random.Next(4), 1);
                }
                else
                {
                    _body.Add(new Ir.Mov(_ix, Pick(_readable)));
                    _body.Add(new Ir.Bin(Ir.BinOp.And, _ix, _ix, new Ir.Imm(3, 1)));
                    args[0] = _ix;
                }
            }

            Ir.Cell? indirect = null;
            if (_random.Next(4) == 0)
            {
                indirect = _fp;
                _body.Add(new Ir.Mov(indirect, new Ir.AddrOf(callee.Name, 0)));
            }

            _body.Add(new Ir.Call(indirect is null ? callee.Name : null, indirect, args, [.. callee.Params.Select(static p => p.W)], Result(callee.RetW)));
        }

        private Ir.BrCmp Branch(string target)
        {
            // porównanie ze znakiem 1 B nie powstaje z C (char jest promowany), więc tylko 2 i 4 B
            int w = Width();
            Ir.Cond[] conditions = w == 1 ? [Ir.Cond.Eq, Ir.Cond.Ne, Ir.Cond.Ltu, Ir.Cond.Leu, Ir.Cond.Gtu, Ir.Cond.Geu] : Enum.GetValues<Ir.Cond>();
            return new Ir.BrCmp(conditions[_random.Next(conditions.Length)], Operand(w, exact: true), Operand(w, exact: true), target);
        }

        private Ir.Cell? Result(int width)
        {
            List<Ir.Cell> fits = [.. _writable.Where(c => c.W == width)];
            return width == 0 || fits.Count == 0 || _random.Next(4) == 0 ? null : Pick(fits);
        }

        /// <summary>Operand szerokości <paramref name="width"/>: stała albo komórka tej samej (albo, gdy nie <paramref name="exact"/>, węższej) szerokości.</summary>
        private Ir.Op Operand(int width, bool exact)
        {
            List<Ir.Cell> fits = [.. _readable.Where(c => exact ? c.W == width : c.W <= width)];
            return fits.Count == 0 || _random.Next(3) == 0 ? Imm(width) : Pick(fits);
        }

        private Ir.Imm Imm(int width)
        {
            int value = _random.Next(2) == 0 ? Interesting[_random.Next(Interesting.Length)] : _random.Next();
            return new Ir.Imm(width == 4 ? value : value & ((1 << (8 * width)) - 1), width);
        }

        private Ir.Cell Pick(List<Ir.Cell> cells) => cells[_random.Next(cells.Count)];

        private int Width() => _random.Next(5) switch { 0 or 1 => 1, 4 => 4, _ => 2 };

        private string NewLabel() => $"L{++_label}";

        private Ir.Cell Cell(string sym, int width)
        {
            _program.AddData(Bss(sym, width));
            return new Ir.Cell(sym, width);
        }

        /// <summary>Sygnatura funkcji pomocniczej.</summary>
        /// <param name="Name">Nazwa.</param>
        /// <param name="Params">Parametry (rekurencyjna: pierwszy to głębokość <c>n</c>).</param>
        /// <param name="RetW">Szerokość wyniku (0 = void).</param>
        /// <param name="Recursive">Woła samą siebie.</param>
        private sealed record Signature(string Name, List<Ir.Cell> Params, int RetW, bool Recursive);
    }
}
