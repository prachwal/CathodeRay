# Mini-C optimization status and gaps

This note records the current depth of optimisations in the CathodeRay Mini-C compiler and the practical limits of its ecosystem. It is intended as a design checkpoint, not a roadmap commitment.

## Optimisation depth

Compared with GCC/LLVM-style compilers the pipeline is deliberately shallow:

| Area | Status |
| --- | --- |
| Local peephole | Present (target-specific, e.g. stub selector) |
| Constant folding / strength reduction on powers of two | Present in IR passes |
| Dead temporary elimination (`t = op; v = t` → `v = op`) | Present |
| Global / inter-block CSE | **Missing** — no available-expressions or GVN across basic blocks |
| Loop opts (full LICM, unrolling, induction-variable strength reduction) | **Embryonic** — only trivial local cases |
| Vectorisation / SLP | **None** (8-bit targets make it low-value) |
| Register allocation | **Greedy / absolute cells** — no graph colouring, limited live-range splitting |
| Profile-guided optimisation (PGO) | **None** beyond ad-hoc hotspot scripts (e.g. `hotspots.py` as a toy) |
| Interprocedural (inlining decisions driven by profile, IPO) | Minimal |

Consequently code size and speed are acceptable for small programs and educational use, but lag far behind mature compilers on larger codebases or tight inner loops.

### Why the gaps exist

- Targets have very few registers (A/X on stub/6502, limited pairs on Z80/8080/6800). A full graph-colouring allocator brings complexity and compile-time cost that rarely pays off on these machines.
- Absolute-memory cells simplify the IR → asm mapping and the IR interpreter oracle, at the expense of register pressure awareness.
- The primary goal so far has been correct multi-target semantics and a clean IR boundary, not peak performance.

## Ecosystem limitations

| Feature | Status |
| --- | --- |
| Debug information | Map file only (`--map`). No DWARF / source-level debugger support |
| Sanitizers (ASan, UBSan, …) | Not present |
| Standard library | Embryonic subset (`string.h`, `ctype.h`, `stdlib.h`, `stdio.h`) versus full libc/newlib |
| ABI documentation | Exists (see `stub-calling-conv.md`, `targets.md`) |
| ABI stability | Changing a convention (e.g. return value in HL vs. A/X, argument cells) is an operation on a living organism — every object file, hand-written asm and the runtime must be updated together |

The ABI is therefore treated as frozen once a target is considered usable. Future changes require a versioned dual-ABI period or a full rebuild of the ecosystem.

## Practical implications

1. For performance-critical kernels, hand-written assembly or careful source-level tuning remains the right tool.
2. The IR + `IrInterpreter` oracle already give a solid base for adding more passes without breaking target correctness.
3. Incremental improvements with the highest leverage are:
   - better local and intra-function CSE,
   - simple loop-invariant motion,
   - improved spill / live-range handling inside the existing greedy scheme,
   - optional hot/cold layout driven by a lightweight profile.

## Related documents

- [minic.md](minic.md) — language reference
- [targets.md](targets.md) — CPU targets and adding a new one
- [stub-calling-conv.md](stub-calling-conv.md) — calling convention and runtime layout
