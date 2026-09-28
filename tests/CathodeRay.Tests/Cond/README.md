# Test asemblacji warunkowej

Trwały test `.if`/`.elseif`/`.else`/`.endif` (ca65) i `IF`/`ELIF`/`ELSE`/`ENDIF`
(z80asm): `main.s` w każdym katalogu składany **naszym** asemblerem i porównywany
z `expected.bin` (test `CondTests.Golden_Matches_Reference`).

## Przypadki

| Katalog | Składnia | `expected.bin` z |
|---|---|---|
| `6502` | ca65 | ca65 `--cpu 6502` + ld65 |
| `6502x` | ca65 | ca65 `--cpu 6502X` + ld65 (LAX w aktywnej gałęzi) |
| `65c02` | ca65 | ca65 `--cpu 65C02` + ld65 (STZ/BRA w aktywnej gałęzi) |
| `8080` | intel | z80asm `-m8080 -b` (IF/ELSE/ENDIF bez ELIF) |
| `z80` | zilog | z80asm `-mz80_strict -b` |
| `z80u` | zilog | z80asm `-mz80 -b` (IXL w aktywnej gałęzi) |

Programy ćwiczą: wybór wariantu, łańcuch elseif/ELIF, zagnieżdżenia, symbole
definiowane tylko w aktywnej gałęzi oraz porównania w warunkach.

## Pułapki zgodności (udokumentowane, nie bugi)

- ca65 **nie zna** `==` ani `!=` (tylko `=` i `<>`) — nasz asembler akceptuje
  nadzbiór (`=`, `==`, `!=`, `<>`, `<`, `<=`, `>`, `>=`), więc źródła golden
  używają operatorów wspólnych.
- Etykieta w linii `.if`/`IF` wiąże bieżący PC (jak w referencjach).

## Regeneracja

```bash
python3 tools/make_cond_golden.py   # wymaga ca65, ld65, z88dk
dotnet test --filter "FullyQualifiedName~CondTests"
```
