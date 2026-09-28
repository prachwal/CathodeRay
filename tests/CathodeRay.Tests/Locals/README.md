# Test tanich etykiet (@) i .incbin

Trwały test wieloplikowości cz. 2: `main.s` + `data.bin` w każdym katalogu,
składane **naszym** asemblerem i porównywane z `expected.bin`
(test `LocalsTests`). Programy ćwiczą zakresy lokalnych (dwa `@x`, referencja
z drugiego zakresu, forward-ref `@fwd`) oraz wstawianie binarki.

## Przypadki

| Katalog | CPU | `expected.bin` z |
|---|---|---|
| `6502` | 6502 | ca65 + ld65 |
| `6502x` | 6502x | ca65 `--cpu 6502X` (LAX w zakresie) |
| `65c02` | 65c02 | ca65 `--cpu 65C02` (STZ w zakresie) |
| `z80` | z80 | z80asm `-mz80_strict -b` |
| `z80u` | z80u | z80asm `-mz80 -b` (IXH/IXL w zakresie) |
| `8080` | 8080 | z80asm `-m8080 -b` |

Semantyka `@` (zgodna z obiema referencjami, sprawdzona empirycznie):
zakres między etykietami globalnymi, forward-ref w zakresie działa, duplikat
w zakresie i `@` bez poprzedzającego globala to błąd.

## Regeneracja

```
python3 tools/make_locals_golden.py   # wymaga ca65, ld65, z88dk
dotnet test --filter "FullyQualifiedName~LocalsTests"
```
