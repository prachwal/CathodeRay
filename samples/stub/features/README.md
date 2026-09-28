# Showcase mechanik asemblera (CPU stub)

Dwa programy pokazujące prawie wszystkie mechaniki z `docs/assembler-capability-gaps.md`:

## main.asm (+ defs.inc, blob.bin)

Jeden moduł: `.include`, `.define` (tekstowe i funkcyjne), `.macro`
(parametry, domyślne, `.local`, `.paramcount`), `.if`/`.elseif`/`.else`,
`.ifblank`, tanie etykiety `@`, zakresy `.scope`/`.proc` z `::`,
`.out`/`.warning`/`.assert`, `.incbin`, `.align`.

```sh
cathode asm samples/stub/features/main.asm --cpu stub -o /tmp/feat.bin -l /tmp/feat.lst
cathode stub run /tmp/feat.bin --dump 0x40:8
```

Program sumuje `blob.bin` (1+2+3+4) do `total`, podwaja przez `MODE`
i odkłada znaczniki gałęzi. Oczekiwane: `total = 20`, `variant = 1`,
`doubled = 4`, `z1 = 0`, `z2 = 5`, `pcc = 2`, `X = 0`, `SP = FF`.

## mathlib.s (mul8, divmod)

Podprogramy arytmetyczne (`CALL`): `mul8` (A=a, X=b → A=a*b mod 256),
`divmod` (A=n, X=d → A=n/d floor, X=n%d; dzielnik 0 daje 0).
`divmod` łata operand `SUB` i wykrywa pożyczkę przez `BCC`.

## minic.s (demo konwencji mini-C)

Program łamiący konwencje z `docs/stub-calling-conv.md`: argument w `A`,
wynik w `A`, zmienne absolutne, 2 poziomy zagnieżdżenia (`main` → `square`
→ `mul8`), porównanie `<` przez `CPA`+`BCC`, pętla `for` przez `X`.
Oczekiwane: `s1 = 16`, `s2 = 25`, `flag = 0`, `flag2 = 7`, `X = 5`.

## link/ (main.s, lib.s, map.cfg)

Dwa moduły + konsolidacja: `.segment`, `GLOBAL`/`EXTERN`, `cathode link`.

```sh
cathode asm samples/stub/features/link/main.s --cpu stub -f obj -o /tmp/m0.o
cathode asm samples/stub/features/link/lib.s --cpu stub -f obj -o /tmp/m1.o
cathode link /tmp/m0.o /tmp/m1.o -m samples/stub/features/link/map.cfg -o /tmp/prog.bin
cathode stub run /tmp/prog.bin --dump 0x0C:2
```

Oczekiwane: `result = 15` (5 + 10 z drugiego modułu).
