# Test segmentów (wariant A linkera)

Trwały test `.segment`/`.code`/`.data`/`.bss` (`SEGMENT`/`CODE`/`DATA`/`BSS`):
`main.s` + `map.txt` w każdym katalogu, składane **naszym** asemblerem
i porównywane z `expected.bin` (test `SegmentsTests`).

## Przypadki

| Katalog | CPU | `expected.bin` z |
|---|---|---|
| `6502` | 6502 | ca65 + ld65 (adresy segmentów z mapy ld65 `-m`) |
| `6502x` | 6502x | ca65 `--cpu 6502X` + ld65 |
| `65c02` | 65c02 | ca65 `--cpu 65C02` + ld65 |
| `z80` | z80 | z80asm `-mz80_strict -b` (`SECTION` + sklejanie CODE/DATA) |
| `z80u` | z80u | z80asm `-mz80 -b` |
| `8080` | 8080 | z80asm `-m8080 -b` |

`map.txt` to `NAZWA@$ADRES` (nasz `--map`). Dla ca65 adresy wyznacza ld65
(sekwencyjnie od bazy) — generator przepisuje je z jego mapy.

## Pułapki zgodności (udokumentowane, nie bugi)

- ca65 nie ma `.bss` — referencja używa `.segment "BSS"` + `type=bss` w `.cfg`.
- z80asm nie pozwala na drugi `ORG` w tej samej sekcji — powrót do sekcji
  kontynuuje licznik (nasz `.segment "CODE"` po raz drugi też wraca).
- z80asm z sekcjami produkuje osobne pliki per sekcja — generator skleja
  CODE + zera + DATA (BSS pomijane, jak w naszym obrazie).
- ld65 w `.cfg` wymaga `$` przy adresach (bez niego czyta dziesiętnie).

## Regeneracja

```
python3 tools/make_segments_golden.py   # wymaga ca65, ld65, z88dk
dotnet test --filter "FullyQualifiedName~SegmentsTests"
```
