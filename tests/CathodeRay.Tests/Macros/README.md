# Test makr

Trwały test makr: `main.s` w każdym katalogu składany **naszym** asemblerem
(pre-pass po includach) i porównywany z `expected.bin`
(test `MacroTests.Golden_Matches_Reference`). Programy ćwiczą definicje
z parametrami, wywołania, `.local`/`LOCAL` oraz makro wołające makro.

## Przypadki

| Katalog | Składnia | `expected.bin` z |
|---|---|---|
| `6502` | ca65 `.macro`/`.endmacro`/`.local` | ca65 + ld65 |
| `6502x` | ca65 | ca65 `--cpu 6502X` (LAX w makrze) |
| `65c02` | ca65 | ca65 `--cpu 65C02` (STZ/BRA w makrze) |
| `8080` | intel `MACRO`/`ENDM`/`LOCAL` | z80asm `-m8080 -b` |
| `z80` | zilog `MACRO`/`ENDM`/`LOCAL` | z80asm `-mz80_strict -b` |
| `z80u` | zilog | z80asm `-mz80 -b` (IXH w makrze) |

## Pułapki zgodności (udokumentowane, nie bugi)

- Ten build ca65 (V2.18 Ubuntu) przyjmuje w `.macro` tylko **jeden parametr**
  (wieloparametrowe definicje z dokumentacji odrzuca) — golden ca65 używa
  makr jednoparametrowych; wieloparametrowe pokryte testami jednostkowymi
  (nasza semantyka: split po przecinkach, jak w dokumentacji ca65).
- `MACRO` w formie z etykietą (`NAZWA: MACRO args`, z80asm) i bez
  (`.macro nazwa args`, ca65); etykieta przy `.macro` to błąd.
- Definicje makr są bezwarunkowe: otaczający `.if` ich nie wyłącza
  (odstępstwo od ca65, rzadki edge).
- Klamer `{}` nie ma: przecinek w argumencie tylko przez nawiasy/cudzysłowy.
- Lokalne per rozwinięcie tylko jawne (`.LOCAL`/`LOCAL` → `__Mn_nazwa`);
  bez tego duplikat, jak w referencjach.
- Błąd w rozwinięciu wskazuje wywołanie i definicję:
  `line 4: ... (in expansion of 'bad' defined at main.s:1)`.
- Rozszerzenie własne (brak w referencjach): wartości domyślne
  `.macro nazwa a, b=5` — brakujący lub pusty argument podstawia tekst
  domyślny (może być wyrażeniem).

## Regeneracja

```
python3 tools/make_macros_golden.py   # wymaga ca65, ld65, z88dk
dotnet test --filter "FullyQualifiedName~MacroTests"
```
