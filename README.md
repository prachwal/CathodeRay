# CathodeRay

CathodeRay is a multi-target assembler and Mini-C compiler written in C# (.NET). It targets classic 8-bit CPUs and a simple educational "stub" architecture.

## Features

### Assembler
- Multi-ISA support: **Z80**, **Intel 8080**, **MOS 6502 / 65C02**, **Motorola 6800**, and a minimal **stub** CPU
- Multiple syntax dialects (Zilog, Intel, MOS/ca65-style, Motorola)
- Macros, conditionals, includes, segments, scopes, local labels, `.incbin`, alignment, expressions
- Object modules with relocations and a simple linker (CODE / DATA / BSS / INIT segments)

### Mini-C compiler (`cathode cc`)
- Subset of C compiling to the same targets via a shared IR
- Pipeline: preprocessor → lexer → parser → type checker → IR lowering → IR passes → target emit → assemble → link
- Types: `uchar`/`char`, `int`, `uint`, `long`, `ulong`, pointers, arrays (including multi-dimensional), structs, unions, enums, `typedef`, function pointers
- Control flow: `if`/`else`, `while`, `do`/`while`, `for`, `switch`, `break`/`continue`, `goto`, recursion
- Standard library subset (`string.h`, `ctype.h`, `stdlib.h`, `stdio.h`) with automatic selective linking
- Calling convention and runtime documented per target (see `docs/stub-calling-conv.md` and `docs/targets.md`)

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

## Documentation

| Document | Description |
| --- | --- |
| [docs/minic.md](docs/minic.md) | Mini-C language reference (Polish; examples are tested) |
| [docs/targets.md](docs/targets.md) | Supported CPUs and how to add a new target |
| [docs/stub-calling-conv.md](docs/stub-calling-conv.md) | Calling convention, memory layout, runtime |
| [docs/z80-assembler.md](docs/z80-assembler.md) | Z80 assembler details |
| [docs/linker-segments.md](docs/linker-segments.md) | Linker and segments |
| [docs/minic-optimization.md](docs/minic-optimization.md) | Current optimization level and known gaps |

## Project layout

```
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

- The C frontend is target-agnostic. Targets only implement a small `ByteIsa` (load/store A, ALU, jumps, stack, pointer primitives) plus crt0.
- IR is three-address; an interpreter (`IrInterpreter`) acts as an oracle so semantics can be checked independently of any CPU.
- Register allocation is intentionally simple (greedy / absolute cells). There is no graph-coloring allocator and no vectorization.
- Debug info is limited to a map file (`--map`). No DWARF or sanitizers yet.

## License

See repository for license information.
