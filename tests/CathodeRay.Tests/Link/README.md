# Test linkowania (.include)

Trwały test wieloplikowości asemblera: każdy katalog to jeden przypadek —
`main.s` + `partN.inc` (plus `sub/` w `6502-nested`), składany **naszym**
asemblerem z kontekstem pliku i porównywany z `expected.bin`.
Test: `tests/CathodeRay.Tests/LinkTests.cs` (odkrywa katalogi automatycznie).

## Przypadki

| Katalog | CPU / składnia | Źródło | `expected.bin` z |
|---|---|---|---|
| `6502` | 6502 / ca65 | `Asm/6502/program.s` | ca65 + ld65 |
| `6502-mos` | 6502 / mos | `Asm/6502/program_mos.s` | złoty `Asm/6502/program.bin` (ca65 nie zna składni MOS) |
| `6502x` | 6502x / ca65 | `Asm/6502/program.s` | ca65 `--cpu 6502X` + ld65 |
| `65c02` | 65c02 / ca65 | `Asm/65c02/program.s` | ca65 `--cpu 65C02` + ld65 |
| `8080` | 8080 / intel | `Asm/8080/opcodes.s` (pierwsze 60 linii) | z80asm `-m8080 -b` |
| `z80` | z80 / zilog | `Asm/z80/program.s` | z80asm `-mz80_strict -b` |
| `z80u` | z80u / zilog | `Asm/z80/program.s` | z80asm `-mz80 -b` |
| `6502-nested` | 6502 / ca65 | jak `6502`, ale `part0.inc` includuje `sub/part1.inc` | ca65 + ld65 |
| `stub` | stub | `main.s` + `defs.inc` | `flat.s` (ten sam program w jednym pliku, brak referencji dla stuba) |

Cięcia dobrano tak, żeby etykiety i odwołania w przód/tył (`loop`/`done`,
`later`, `table`, `message`) przekraczały granice plików. Referencje dostają
ten sam podzielony `main.s` i same rozwijają include'y (ca65 `.include`,
z80asm `INCLUDE`), więc test sprawdza zgodność całej semantyki podziału.

## Regeneracja

```
python3 tools/make_link_golden.py   # wymaga ca65, ld65, z88dk (jak make_asm_golden.py)
dotnet test --filter "FullyQualifiedName~LinkTests"
```

Skrypt kasuje i odtwarza zawartość katalogów (poza tym README), więc ręczne
poprawki w `.s`/`.inc` należy najpierw przenieść do programu golden w `Asm/`
albo do logiki cięć w skrypcie.
