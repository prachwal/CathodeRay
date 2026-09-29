using System.Text;

namespace CathodeRay.C;

/// <summary>Interpreter kodu pośredniego: wyrocznia semantyki front-endu niezależna od procesora. Pamięć to 64 KB,
/// symbole dostają adresy, funkcje zapisują i odtwarzają <see cref="Ir.Function.Saved"/> tak jak ramka celu.
/// Funkcje zewnętrzne konsoli (<c>putchar</c>, <c>puthex</c>, <c>putdec</c>) są wbudowane i piszą do <see cref="Console"/>.</summary>
public sealed class IrInterpreter
{
    private const int DataBase = 0x1000;

    private const int FunctionBase = 0xC000;

    private readonly byte[] _memory = new byte[0x10000];

    private readonly List<(Ir.Module Module, Dictionary<string, int> Local)> _modules = [];

    private readonly Dictionary<string, int> _exported = new(StringComparer.Ordinal);

    private readonly Dictionary<int, (Ir.Function Function, Dictionary<string, int> Local)> _byAddress = [];

    private readonly StringBuilder _console = new();

    private int _next = DataBase;

    private long _steps;

    /// <summary>Utworzone przez <see cref="Load"/>.</summary>
    private IrInterpreter()
    {
    }

    /// <summary>Wypisane znaki konsoli.</summary>
    public string Console => _console.ToString();

    /// <summary>Limit wykonanych instrukcji (ochrona przed pętlą bez końca).</summary>
    public long StepLimit { get; init; } = 20_000_000;

    /// <summary>Liczba wykonanych instrukcji.</summary>
    public long Steps => _steps;

    /// <summary>Ładuje moduły (bez linkowania nazw lokalnych: <c>static</c> i symbole niewyeksportowane są prywatne modułu).</summary>
    /// <param name="modules">Moduły programu; funkcja <c>main</c> w którymś z nich.</param>
    /// <param name="stepLimit">Limit instrukcji.</param>
    /// <returns>Gotowy do uruchomienia interpreter.</returns>
    public static IrInterpreter Load(IReadOnlyList<Ir.Module> modules, long stepLimit = 20_000_000)
    {
        var interpreter = new IrInterpreter { StepLimit = stepLimit };
        interpreter._exported["cc_retbuf"] = interpreter._next;
        interpreter._next += Lowering.MaxReturnedStruct;
        interpreter._exported[WideLegalizer.ReturnHigh] = interpreter._next;
        interpreter._next += 2;
        foreach (Ir.Module module in modules)
        {
            interpreter.Allocate(module);
        }

        foreach ((Ir.Module module, Dictionary<string, int> local) in interpreter._modules)
        {
            interpreter.Initialize(module, local);
        }

        return interpreter;
    }

    /// <summary>Czyta bajty pamięci pod adresem wyeksportowanego symbolu (do porównań z wynikiem na celu).</summary>
    /// <param name="symbol">Symbol wyeksportowany.</param>
    /// <param name="size">Liczba bajtów.</param>
    /// <returns>Bajty.</returns>
    public byte[] Peek(string symbol, int size)
    {
        int address = _exported.TryGetValue(symbol, out int found) ? found : throw new InvalidOperationException($"unknown symbol '{symbol}'.");
        return _memory[address..(address + size)];
    }

    /// <summary>Uruchamia inicjalizatory globali, potem <c>main</c>.</summary>
    /// <returns>Wynik <c>main</c> (młodsze 16 bitów) oraz szerokość wyniku (0 = void).</returns>
    public (int Value, int Width) RunMain()
    {
        foreach ((Ir.Module module, Dictionary<string, int> local) in _modules)
        {
            foreach (Ir.Data table in module.Data.Where(static d => d.Segment == "INIT"))
            {
                foreach (Ir.SymWord entry in table.Init!.OfType<Ir.SymWord>())
                {
                    Call(_byAddress[Resolve(local, entry.Sym)], []);
                }
            }
        }

        (Ir.Function function, Dictionary<string, int> mainLocal) = _byAddress[_exported["main"]];
        return ((int)Call((function, mainLocal), []), function.RetW);
    }

    private static long Mask(int width) => width == 1 ? 0xFF : width == 2 ? 0xFFFF : 0xFFFFFFFFL;

    private static long Signed(long value, int width) => width == 1 ? (sbyte)value : width == 2 ? (short)value : (int)value;

    private static int WidthOf(Ir.Op op) => op switch
    {
        Ir.Cell cell => cell.W,
        Ir.Imm imm => imm.W,
        _ => 2,
    };

    private void Allocate(Ir.Module module)
    {
        var local = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Ir.Data data in module.Data.Where(static d => d.Sym.Length > 0))
        {
            local[data.Sym] = _next;
            if (data.Exported)
            {
                _exported[data.Sym] = _next;
            }

            _next += Math.Max(data.Size, 1);
        }

        foreach (Ir.Function function in module.Functions)
        {
            int address = FunctionBase + (_byAddress.Count * 4);
            local[function.Name] = address;
            if (!function.IsStatic)
            {
                _exported[function.Name] = address;
            }

            _byAddress[address] = (function, local);
        }

        _modules.Add((module, local));
        if (_next > FunctionBase)
        {
            throw new InvalidOperationException("program data does not fit the interpreter memory.");
        }
    }

    private void Initialize(Ir.Module module, Dictionary<string, int> local)
    {
        foreach (Ir.Data data in module.Data.Where(static d => d.Sym.Length > 0 && d.Init is not null))
        {
            int at = local[data.Sym];
            foreach (Ir.Piece piece in data.Init!)
            {
                switch (piece)
                {
                    case Ir.Bytes bytes:
                        bytes.Value.CopyTo(_memory, at);
                        at += bytes.Value.Length;
                        break;
                    case Ir.SymWord word:
                        Write(at, Resolve(local, word.Sym) + word.Off, 2);
                        at += 2;
                        break;
                }
            }
        }
    }

    private int Resolve(Dictionary<string, int> local, string symbol) =>
        local.TryGetValue(symbol, out int address) || _exported.TryGetValue(symbol, out address)
            ? address
            : throw new InvalidOperationException($"unresolved symbol '{symbol}'.");

    private long Read(int address, int width)
    {
        long value = 0;
        for (int i = 0; i < width; i++)
        {
            value |= (long)_memory[(address + i) & 0xFFFF] << (8 * i);
        }

        return value;
    }

    private void Write(int address, long value, int width)
    {
        for (int i = 0; i < width; i++)
        {
            _memory[(address + i) & 0xFFFF] = (byte)(value >> (8 * i));
        }
    }

    private long Get(Ir.Op op, Dictionary<string, int> local) => op switch
    {
        Ir.Cell cell => Read(Resolve(local, cell.Sym), cell.W),
        Ir.Imm imm => (uint)imm.Value & Mask(imm.W),
        Ir.AddrOf address => (Resolve(local, address.Sym) + address.Off) & 0xFFFF,
        _ => throw new InvalidOperationException($"unsupported operand {op.GetType().Name}."),
    };

    private void Put(Ir.Cell cell, long value, Dictionary<string, int> local) =>
        Write(Resolve(local, cell.Sym), value & Mask(cell.W), cell.W);

    private long Call((Ir.Function Function, Dictionary<string, int> Local) target, long[] arguments)
    {
        (Ir.Function function, Dictionary<string, int> local) = target;
        var snapshot = new List<(int Address, byte Value)>();
        foreach (Ir.Owned owned in function.Saved)
        {
            int address = Resolve(local, owned.Sym);
            for (int i = 0; i < owned.Size; i++)
            {
                snapshot.Add((address + i, _memory[(address + i) & 0xFFFF]));
            }
        }

        for (int i = 0; i < function.Params.Count && i < arguments.Length; i++)
        {
            Put(function.Params[i], arguments[i], local);
        }

        long result = Run(function, local);
        for (int i = snapshot.Count - 1; i >= 0; i--)
        {
            _memory[snapshot[i].Address & 0xFFFF] = snapshot[i].Value;
        }

        return result;
    }

    private long Run(Ir.Function function, Dictionary<string, int> local)
    {
        var labels = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < function.Body.Count; i++)
        {
            if (function.Body[i] is Ir.Label label)
            {
                labels[label.Name] = i;
            }
        }

        for (int pc = 0; pc < function.Body.Count; pc++)
        {
            if (++_steps > StepLimit)
            {
                throw new InvalidOperationException("step limit exceeded.");
            }

            switch (function.Body[pc])
            {
                case Ir.Mov mov:
                    Put(mov.Dst, Get(mov.Src, local), local);
                    break;
                case Ir.Bin bin:
                    Put(bin.Dst, Binary(bin, local), local);
                    break;
                case Ir.Un un:
                {
                    long a = Get(un.A, local);
                    Put(un.Dst, un.Kind == Ir.UnOp.Neg ? -a : ~a, local);
                    break;
                }

                case Ir.Load load:
                {
                    int address = (int)((Get(load.Ptr, local) + load.Off) & 0xFFFF);
                    Put(load.Dst, Read(address, load.Bytes), local);
                    break;
                }

                case Ir.Store store:
                {
                    int address = (int)((Get(store.Ptr, local) + store.Off) & 0xFFFF);
                    Write(address, Get(store.Value, local), store.Bytes);
                    break;
                }

                case Ir.CopyBlock copy:
                {
                    int to = (int)Get(copy.Dst, local);
                    int from = (int)Get(copy.Src, local);
                    for (int i = 0; i < copy.Size; i++)
                    {
                        _memory[(to + i) & 0xFFFF] = _memory[(from + i) & 0xFFFF];
                    }

                    break;
                }

                case Ir.Fill fill:
                {
                    int at = (int)Get(fill.Dst, local);
                    for (int i = 0; i < fill.Size; i++)
                    {
                        _memory[(at + i) & 0xFFFF] = (byte)fill.Value;
                    }

                    break;
                }

                case Ir.BrCmp branch when Compare(branch, local):
                    pc = labels[branch.Target];
                    break;
                case Ir.Jmp jump:
                    pc = labels[jump.Target];
                    break;
                case Ir.Call call:
                    Invoke(call, local);
                    break;
                case Ir.Ret ret:
                    return ret.Value is null ? 0 : Get(ret.Value, local) & Mask(Math.Max(ret.W, 1));
            }
        }

        return 0;
    }

    private void Invoke(Ir.Call call, Dictionary<string, int> local)
    {
        long[] arguments = [.. call.Args.Select(a => Get(a, local))];
        long result;
        if (call.Direct is not null && !_exported.ContainsKey(call.Direct) && !local.ContainsKey(call.Direct))
        {
            result = Builtin(call.Direct, arguments);
        }
        else
        {
            int address = call.Direct is not null ? Resolve(local, call.Direct) : (int)Get(call.Indirect!, local);
            result = Call(_byAddress.TryGetValue(address, out var target) ? target : throw new InvalidOperationException($"call through bad address {address:X4}."), arguments);
        }

        if (call.Result is not null)
        {
            Put(call.Result, result, local);
        }
    }

    private long Builtin(string name, long[] arguments)
    {
        switch (name)
        {
            case "putchar":
                _console.Append((char)(arguments[0] & 0xFF));
                return 0;
            case "puthex":
                _console.Append((arguments[0] & 0xFF).ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                return 0;
            case "putdec":
                _console.Append(((short)arguments[0]).ToString(System.Globalization.CultureInfo.InvariantCulture));
                return 0;
            default:
                throw new InvalidOperationException($"unresolved function '{name}'.");
        }
    }

    private long Binary(Ir.Bin bin, Dictionary<string, int> local)
    {
        int width = bin.Dst.W;
        long a = Get(bin.A, local);
        long b = Get(bin.B, local);
        int bits = width * 8;
        switch (bin.Kind)
        {
            case Ir.BinOp.Add:
                return a + b;
            case Ir.BinOp.Sub:
                return a - b;
            case Ir.BinOp.And:
                return a & b;
            case Ir.BinOp.Or:
                return a | b;
            case Ir.BinOp.Xor:
                return a ^ b;
            case Ir.BinOp.Mul:
                return a * b;
            case Ir.BinOp.Shl:
                return (b & 0xFF) >= bits ? 0 : a << (int)(b & 0xFF);
            case Ir.BinOp.Shr:
                return (b & 0xFF) >= bits ? 0 : a >> (int)(b & 0xFF);
            case Ir.BinOp.Sar:
            {
                long signed = Signed(a, WidthOf(bin.A));
                return signed >> (int)Math.Min(b & 0xFF, 31);
            }

            case Ir.BinOp.Div:
                return b == 0 ? 0 : a / b;
            case Ir.BinOp.Mod:
                return b == 0 ? 0 : a % b;
            case Ir.BinOp.DivS:
            {
                long sa = Signed(a, WidthOf(bin.A));
                long sb = Signed(b, WidthOf(bin.B));
                return sb == 0 ? 0 : sa / sb;
            }

            default:
            {
                long sa = Signed(a, WidthOf(bin.A));
                long sb = Signed(b, WidthOf(bin.B));
                return sb == 0 ? 0 : sa % sb;
            }
        }
    }

    private bool Compare(Ir.BrCmp branch, Dictionary<string, int> local)
    {
        int width = Math.Max(WidthOf(branch.A), WidthOf(branch.B));
        long a = Get(branch.A, local);
        long b = Get(branch.B, local);
        bool signed = branch.C is Ir.Cond.Lt or Ir.Cond.Le or Ir.Cond.Gt or Ir.Cond.Ge;
        if (signed)
        {
            a = Signed(a, width);
            b = Signed(b, width);
        }

        return branch.C switch
        {
            Ir.Cond.Eq => a == b,
            Ir.Cond.Ne => a != b,
            Ir.Cond.Lt or Ir.Cond.Ltu => a < b,
            Ir.Cond.Le or Ir.Cond.Leu => a <= b,
            Ir.Cond.Gt or Ir.Cond.Gtu => a > b,
            _ => a >= b,
        };
    }
}
