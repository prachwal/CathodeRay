# Test linkera wielomodułowego

Trwały test `cathode link`: `mod0.s` + `mod1.s` (+ `map.cfg`) w każdym katalogu,
składane **naszym** asemblerem do obiektów (`-f obj`) i łączone linkerem,
porównywane z `expected.bin` (test `ModulesTests`). Moduły wołają się
nawzajem (`GLOBAL`/`EXTERN`), więc test ćwiczy relokacje między modułami.

## Przypadki

| Katalog | CPU | `expected.bin` z |
|---|---|---|
| `6502` | 6502 | ca65 (`.import`/`.export`) + ld65 |
| `6502x` | 6502x | ca65 `--cpu 6502X` + ld65 |
| `65c02` | 65c02 | ca65 `--cpu 65C02` + ld65 |
| `z80` | z80 | z80asm `-mz80_strict` (link obiektów) |
| `z80u` | z80u | z80asm `-mz80` |
| `8080` | 8080 | z80asm `-m8080` |

`map.cfg` to `MEMORY`/`SEGMENTS` w naszym formacie (podzbiór ld65).

## Regeneracja

```
python3 tools/make_modules_golden.py   # wymaga ca65, ld65, z88dk
dotnet test --filter "FullyQualifiedName~ModulesTests"
```
