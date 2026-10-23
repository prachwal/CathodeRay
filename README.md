# CathodeRay

CathodeRay is a multi-target assembler and Mini-C compiler written in C# (.NET). It targets classic 8-bit CPUs and a simple educational "stub" architecture.

## Features

### Assembler

- Multi-ISA support: **Z80**, **Intel 8080**, **MOS 6502 / 65C02**, **Motorola 6800**, and a minimal **stub** CPU
- Multiple syntax dialects (Zilog, Intel, MOS/ca65-style, Motorola)
- Macros, conditionals, includes, segments, scopes, local labels, `.incbin`, alignment, expressions
- Object modules with relocations and a simple linker (CODE / DATA / BSS / INIT segments)

### Mini-C compiler (`cathode cc`)

- Subset of C compiling to the same targets via a shared frontend and backend IRs
- Pipeline: preprocessor → lexer → parser → type checker → IR lowering → IR passes → allocation (VReg path) → target emit → assemble → link
- Types: `uchar`/`char`, `int`, `uint`, `long`, `ulong`, pointers, arrays (including multi-dimensional), structs, unions, enums, `typedef`, function pointers
- Control flow: `if`/`else`, `while`, `do`/`while`, `for`, `switch`, `break`/`continue`, `goto`, recursion
- Standard library subset (`string.h`, `ctype.h`, `stdlib.h`, `stdio.h`) with automatic selective linking
- Calling convention and runtime documented per target (see `docs/codegen/stub-calling-conv.md` and `docs/codegen/targets.md`)

## Quick start

```bash
# Build
dotnet build

# Assemble
cathode asm program.s -o program.bin --cpu z80

# Compile Mini-C
cathode cc hello.c -o hello.bin --cpu stub
# or: --cpu 6502 | 65c02 | z80 | 8080 | 6800
```

## Install

Release builds (tag `vX.Y.Z`) attach self-contained `cathode` binaries for linux-x64, linux-arm64, win-x64 and osx-arm64,
plus a `dotnet tool` package. Put the binary on `PATH` and run `cathode cc prog.c -o prog.bin --cpu z80`.

```bash
dotnet tool install -g cathode            # needs the .NET SDK; or use the downloaded binary
tools/publish.sh linux-x64 win-x64        # local build into dist/
```

## Documentation

| Document | Description |
| --- | --- |
| [docs/minic.md](docs/minic.md) | Mini-C language reference (Polish; examples are tested) |
| [docs/codegen/targets.md](docs/codegen/targets.md) | Supported CPUs and how to add a new target |
| [docs/codegen/stub-calling-conv.md](docs/codegen/stub-calling-conv.md) | Calling convention, memory layout, runtime |
| [docs/assembler/z80-assembler.md](docs/assembler/z80-assembler.md) | Z80 assembler details |
| [docs/assembler/linker-segments.md](docs/assembler/linker-segments.md) | Linker and segments |
| [docs/codegen/minic-optimization.md](docs/codegen/minic-optimization.md) | Current optimization level and known gaps |
| [docs/ir-vreg-abi/ir-vreg-plan.md](docs/ir-vreg-abi/ir-vreg-plan.md) | Virtual-register IR and backend migration plan |

## Project layout

```text
src/
  CathodeRay.Assembler/   # multi-target assembler + linker
  CathodeRay.C/           # Mini-C frontend, IR, targets, stdlib
  ...
tests/                    # unit + conformance tests (IR oracle, target matrix)
samples/                  # example programs
docs/                     # language and design notes
tools/                    # helper scripts
```

## Design notes

- The C frontend is target-agnostic.
- **Cell IR** remains the current production path: absolute memory cells simplify lowering, interpretation and compatibility with existing targets.
- **VReg IR** is the planned scalable backend: virtual registers are target-independent, SSA-ready, and allocated only after target-independent optimization.
- The VReg path introduces explicit liveness and spill handling; richer allocators can be added without changing frontend lowering.
- `IrInterpreter` is the semantic oracle for Cell IR. `VRegInterpreter` will provide the corresponding oracle before allocation.
- Register allocation is staged: conservative allocation first, then liveness-aware linear scan; graph coloring is optional and measurement-driven.
- Debug info is limited to a map file (`--map`). No DWARF or sanitizers yet.

## License

See repository for license information.
