# Plan: Virtual-register IR (scalable down to accumulator CPUs)

Status: **design proposal** — not implemented.
Related: [minic-optimization.md](minic-optimization.md), [targets.md](targets.md).

## Goal

Replace (or sit beside) the current cell-oriented IR with a **virtual-register IR** that:

1. Assumes an **unbounded** set of virtual registers (vregs).
2. Scales **down** to the simplest targets (stub, 6502, Z80…) via register allocation + spill.
3. Scales **up** to machines with many GPRs without rewriting the frontend.
4. Can be selected from the CLI through a **factory**, so both IRs can coexist during migration.

Design principle: *it is easier to go from many registers down to few than the opposite*.

---

## 1. Two IR flavours

| Name | Code name | Model | Today |
| --- | --- | --- | --- |
| **Cell IR** (current) | `cell` | Absolute memory cells + accumulator during select | Default, production |
| **VReg IR** (proposed) | `vreg` | SSA-like virtual registers, then allocate | New |

Both consume the same typed AST / checked program. Only lowering and everything after it differ.

```
C source
  → preprocessor → lexer → parser → type checker
  → IrFactory.Create(kind)           ← CLI chooses here
       ├─ CellLowering  → Ir.Module (cells)  → IrPasses → ByteTarget path
       └─ VRegLowering  → VRegModule         → VRegPasses → Allocator → Target path
```

---

## 2. VReg IR shape (sketch)

### 2.1 Values

```text
VReg   = %t0, %t1, …          // unbounded, function-local
Imm    = 42 | -1 | …
Addr   = @global | @func | stack slot
Width  = i8 | i16 | i32       // matches uchar / int|uint / long|ulong
```

No physical registers appear until after allocation.

### 2.2 Instructions (minimal set)

```text
%t = mov     <op>
%t = bin     op, <op>, <op>          // add sub mul div rem and or xor shl shr ashr
%t = un      op, <op>                // neg not zext sext trunc
%t = load    width, <addr>
     store   width, <addr>, <op>
%t = loadidx width, <base>, <index>  // optional sugar
     storeidx …
%t = cmp     cond, <op>, <op>        // → i8 0/1
     br      <op>, label, label
     jmp     label
%t = call    retwidth, callee, args…
     ret     <op>?
     phi     width, (label → op)…     // only if we keep SSA form
     copyblock / fill                // for structs (or lower earlier)
```

Operands `<op>` are `VReg | Imm | Addr`.

### 2.3 SSA vs. non-SSA

**Phase 1 (recommended):** non-SSA with explicit `mov` and mutable vregs.  
Easier to lower from the current AST, easier to interpret, still enough for a linear-scan or greedy allocator.

**Phase 2 (optional):** promote to SSA (phi insertion) for GVN / LICM / better coalescing.

Start with non-SSA; do not block the design on phi nodes.

### 2.4 Function frame

```text
VRegFunction {
  name, params: VReg[],
  retWidth,
  vregs: { id, width, spillSlot? }[],
  blocks: VRegBlock[],
  stackSlots: { id, size, align }[]   // for spills and large locals
}
```

Locals that today become absolute cells become either:
- a vreg (if scalar and short-lived), or
- a stack slot / static cell (arrays, structs, address-taken).

---

## 3. Passes on VReg IR

Order of work (each step independently testable):

| # | Pass | Purpose |
| --- | --- | --- |
| 1 | VReg lowering | AST → VRegModule |
| 2 | Constant fold / algebraic | same as today’s IrPasses |
| 3 | Dead vreg elimination | |
| 4 | Local CSE | optional early win |
| 5 | **Register allocator** | vreg → physical / spill |
| 6 | Spill insertion | loads/stores around uses |
| 7 | Prolog/epilog | save callee-saves, set up frame |
| 8 | Target emit | physical ops → asm via existing ByteIsa or a richer ISA |

### 3.1 Allocator strategies (pluggable)

```text
IRegisterAllocator
  ├─ GreedyLinearScanAllocator   // good default, fast
  ├─ GraphColoringAllocator      // optional later
  └─ AccumulatorForcingAllocator // maps almost everything through A (+ X)
```

For **stub / 6502 / Z80 / 8080 / 6800** the first implementation can be:

> “At most one (or two) live values in physical registers; everything else spills to the same absolute cells we use today.”

That recovers current code quality while keeping the IR rich.  
Richer targets later swap in a real linear-scan without touching lowering.

---

## 4. Scaling down to the simplest CPU

```text
VReg IR  (many virtual regs)
    │
    ▼  RegisterAllocator(target.PhysRegs, target.SpillStrategy)
Physical IR / machine ops
    │
    ▼  existing ByteSelector / ByteIsa   (or a thin adapter)
Assembly
```

| Target class | Phys regs exposed to allocator | Spill |
| --- | --- | --- |
| stub | A (value), X (index) | absolute cells `fn__t@N` |
| 6502 / 65C02 | A, X, Y (limited) | absolute / zero-page |
| Z80 / 8080 | A, HL, BC, DE (pairs) | memory |
| 6800 | A, B, X | memory |
| future 16/32-bit | full GPR set | stack frame |

The **same** VRegModule is legal for every target; only the allocator + emit change.

---

## 5. CLI and factory

### 5.1 Command-line

```bash
cathode cc hello.c -o hello.bin --cpu z80
# default IR = cell (today’s behaviour)

cathode cc hello.c -o hello.bin --cpu z80 --ir vreg
cathode cc hello.c -o hello.bin --cpu stub --ir cell

cathode cc hello.c --ir list          # print available IR kinds
```

Suggested flags:

| Flag | Values | Default |
| --- | --- | --- |
| `--ir <kind>` | `cell`, `vreg` | `cell` |
| `--ir-list` / `--ir list` | — | print and exit |

Optional later: `--regalloc linear-scan|greedy|accumulator`.

### 5.2 Factory (C# sketch)

```csharp
public enum IrKind
{
    Cell,   // current Ir.Module path
    VReg,   // new virtual-register path
}

public interface IIrPipeline
{
    string Name { get; }                    // "cell" / "vreg"
    object Lower(CheckedProgram program); // Ir.Module or VRegModule
    object RunPasses(object module);
    string Emit(object module, ICTarget target, EmitOptions opt);
}

public static class IrFactory
{
    private static readonly Dictionary<IrKind, Func<IIrPipeline>> _map = new()
    {
        [IrKind.Cell] = () => new CellIrPipeline(),
        [IrKind.VReg] = () => new VRegIrPipeline(),
    };

    public static IIrPipeline Create(IrKind kind)
    {
        if (!_map.TryGetValue(kind, out var ctor))
            throw new ArgumentException($"Unknown IR kind: {kind}");
        return ctor();
    }

    public static IReadOnlyList<(IrKind kind, string name)> List() =>
        _map.Keys.Select(k => (k, k.ToString().ToLowerInvariant())).ToList();

    public static IrKind Parse(string s) => s.ToLowerInvariant() switch
    {
        "cell" => IrKind.Cell,
        "vreg" or "virtual" or "ssa" => IrKind.VReg,
        _ => throw new ArgumentException($"Unknown --ir value: {s}")
    };
}
```

Wiring in the driver (`cathode cc`):

```csharp
var irKind = IrFactory.Parse(args.Ir ?? "cell");
var pipeline = IrFactory.Create(irKind);
var module = pipeline.Lower(checkedProgram);
module = pipeline.RunPasses(module);
var asm = pipeline.Emit(module, target, emitOptions);
```

Target selection (`--cpu`) stays orthogonal: the factory only chooses **how** the program is represented, not **which** CPU emits it.

### 5.3 Why a factory (not a bool)

- More than two IRs may appear (e.g. a future SSA-strict variant, a debug IR).
- Tests can instantiate a pipeline without parsing argv.
- Keeps the driver free of `if (ir == vreg)` trees.

---

## 6. Interpreter / oracle

Current `IrInterpreter` + `IrOracle` validate Cell IR against every target.

For VReg IR:

1. **VRegInterpreter** — execute VRegModule directly (vregs in a map, stack slots in a flat memory image).
2. Reuse the same oracle idea: `main` result and side effects must match Cell IR and the hardware runners.
3. After allocation, a third check is optional: allocated form vs. VRegInterpreter (spill correctness).

---

## 7. Migration plan

| Phase | Work | Exit criteria |
| --- | --- | --- |
| **0** | Document + factory stub (`--ir cell` only) | CLI accepts `--ir`, lists kinds |
| **1** | `VRegModule` types + VRegLowering for scalars | `vreg` path compiles empty/`return 42` |
| **2** | VRegInterpreter + oracle vs. Cell IR | all scalar expression tests pass on interpreter |
| **3** | AccumulatorForcingAllocator + emit via existing ByteIsa | same programs run on stub/6502/Z80 |
| **4** | Calls, structs, long, stdlib | TargetMatrix / IrConformance green for `--ir vreg` |
| **5** | Linear-scan allocator + better spills | measurable code-size win on 6809 / 65C816-class |
| **6** | Optional SSA promotion, LICM, GVN | as needed |

Default remains `--ir cell` until phase 4 is solid. Then flip the default or keep both forever.

---

## 8. File / type layout (proposed)

```text
src/CathodeRay.C/
  Ir/                        // existing cell IR stays
    Ir.cs
    IrPasses.cs
    IrInterpreter.cs
  VReg/
    VRegModule.cs            // functions, blocks, insns
    VRegLowering.cs
    VRegPasses.cs
    VRegInterpreter.cs
    Alloc/
      IRegisterAllocator.cs
      GreedyLinearScanAllocator.cs
      AccumulatorForcingAllocator.cs
  IrFactory.cs               // IrKind + Create + Parse
  Pipelines/
    CellIrPipeline.cs
    VRegIrPipeline.cs
```

Assembler and `ByteIsa` targets stay unchanged in phases 0–4; only the path *into* them grows an allocation step.

---

## 9. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Two IRs to maintain | Factory + shared tests; Cell IR frozen for bugfixes only after VReg is default |
| Worse code on 8-bit at first | AccumulatorForcingAllocator must match current cell quality before flip |
| Allocator complexity | Start with the dumbest correct allocator; measure before graph colouring |
| Debug / map files | Emit the same `--map` symbols; vreg names can appear as comments in listing |

---

## 10. Success metrics

1. `--ir vreg --cpu stub` passes the same conformance suite as `--ir cell`.
2. Adding a hypothetical rich-register target requires **only** a new `IRegisterAllocator` mapping + emit, not a new lowering.
3. CLI discovers IR kinds through the factory (`--ir list`), no hard-coded switches in the driver.

---

## Related documents

- [minic-optimization.md](minic-optimization.md) — why current depth is limited
- [targets.md](targets.md) — how CPUs are added today
- [stub-calling-conv.md](stub-calling-conv.md) — ABI that allocators must respect
