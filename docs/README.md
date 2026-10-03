# CathodeRay — dokumentacja

## Pliki w root (powiązane z kodem — nie przenosić)

Ścieżki do tych plików są na sztywno w testach i skryptach (`MiniCDocsTests`, `TargetSizeTests`, `tools/compare.py`, `tools/disasm_compare.py`, `tools/hotspots.py`).

| Plik | Zawartość | Odświeżanie |
| --- | --- | --- |
| [minic.md](minic.md) | Specyfikacja języka mini-C (przykłady kompilowane w testach) | ręcznie |
| [compare.md](compare.md) | Rozmiar mini-C vs cc65 / SDCC (14 benchy) | `python3 tools/compare.py --write` |
| [target-sizes.md](target-sizes.md) | Rozmiar CODE na 8 celach (ścieżka Cell) | `UPDATE_TARGET_SIZES=1 dotnet test --filter TargetSizeTests` |
| [vreg-sizes.md](vreg-sizes.md) | Rozmiar CODE na 8 celach (ścieżka `--ir vreg`) | `UPDATE_VREG_SIZES=1 dotnet test --filter VRegSizeTests` |
| [v2-sizes.md](v2-sizes.md) | Rozmiar CODE pilota ABI v2 (`nes`) | `UPDATE_V2_SIZES=1 dotnet test --filter AbiV2SizeTests` |
| [disasm-6502.md](disasm-6502.md) / [disasm-z80.md](disasm-z80.md) | Listingi nasz vs cc65 / SDCC per funkcja | `python3 tools/disasm_compare.py --cpu <cpu> --all --md docs/disasm-<cpu>.md` |
| [hotspots.md](hotspots.md) | Najczęstsze pary/trójki instrukcji 6502 | `python3 tools/hotspots.py --write` |

## Podfoldery

| Folder | Zawartość |
| --- | --- |
| [language/](language/) | Frontendy: ranking języków, plany COBOL i Fortran |
| [codegen/](codegen/) | Cele, ABI v1, optymalizacje per CPU, analizy rozmiaru |
| [ir-vreg-abi/](ir-vreg-abi/) | IR (Cell/VReg), alokatory, projekt ABI v2 |
| [assembler/](assembler/) | Asembler wielocelowy, luka wobec referencji, linker |
| [archive/](archive/) | Historyczne analizy jednorazowe (liczby nieaktualne) |
| [technical/](technical/) | Opracowanie zbiorcze (indeks: [technical/README.md](technical/README.md)) |
| [plans/](plans/) | Plany prac: `NN-nazwa.json` (źródło) + `NN-nazwa.md` (render) |

## Znane nieaktualności (oznaczone w plikach)

- `codegen/minic-optimization.md`: opisuje VReg jako planowany — jest zaimplementowany (plan 37).
- `ir-vreg-abi/ir-vreg-plan.md`: propozycja pierwotna, zastąpiona przez `vreg-design.md`.
- `codegen/codegen-size-and-hl-plan.md`, `archive/`: liczby sprzed planów 32–40; aktualne w [compare.md](compare.md).
