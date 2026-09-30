# Mini-C optimization status and gaps

This note describes the current Cell IR optimization depth and the planned VReg backend. It is a design checkpoint, not a claim that the planned backend is already implemented.

## Optimisation depth

Compared with GCC/LLVM-style compilers the pipeline is deliberately shallow:

| Area | Status |
| --- | --- |
| Local peephole | Present (target-specific, e.g. stub selector) |
| Constant folding / strength reduction on powers of two | Present in IR passes |
| Dead temporary elimination (`t = op; v = t` → `v = op`) | Present |
| Global / inter-block CSE | **Missing** in the current Cell path; VReg is SSA-ready for future GVN |
| Loop opts (full LICM, unrolling, induction-variable strength reduction) | **Embryonic** — only trivial local cases |
| Vectorisation / SLP | **None** (8-bit targets make it low-value) |
| Register allocation | **Transitioning** — Cell uses greedy / absolute cells; VReg plans explicit liveness, spilling and pluggable allocation |
| Profile-guided optimisation (PGO) | **None** beyond ad-hoc hotspot scripts (e.g. `hotspots.py` as a toy) |
| Interprocedural (inlining decisions driven by profile, IPO) | Minimal |

Consequently code size and speed are acceptable for small programs and educational use, but lag far behind mature compilers on larger codebases or tight inner loops.

### Why the gaps exist

- Targets have very few registers (A/X on stub/6502, limited pairs on Z80/8080/6800). A full graph-colouring allocator brings complexity and compile-time cost that rarely pays off on these machines.
- Absolute-memory cells simplify the IR → asm mapping and interpreter oracle, but do not model register pressure or live ranges explicitly.
- VReg therefore starts with a conservative allocator and spill rewriting, followed by liveness-aware linear scan.
- SSA is a later transformation of VReg, enabling GVN/LICM without creating a second incompatible IR.
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

## VReg optimization pipeline

```text
VReg
 ↓
local simplification / constant folding
 ↓
dead-vreg elimination / local CSE
 ↓
CFG + liveness analysis
 ↓
conservative allocation + spill rewrite
 ↓
prolog / epilog
 ↓
target emission
```

For global optimization:

```text
VReg → CFG/dominance → SSA → GVN/LICM → out-of-SSA → liveness/allocation
```

## Practical implications

1. `IrInterpreter` remains the semantic oracle for Cell IR; `VRegInterpreter` should be the equivalent oracle for VReg.
2. Allocated VReg output should be checked against `VRegInterpreter`, especially for branches, calls and spills.
3. The first backend milestone is correctness with conservative allocation—not graph coloring.
4. For performance-critical kernels, hand-written assembly or careful source-level tuning remains the right tool.
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
- [ir-vreg-plan.md](ir-vreg-plan.md) — virtual-register backend design
