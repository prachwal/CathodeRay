# ABI v2 — argumenty i wyniki w rejestrach (projekt migracji)

Status: **projekt** (nie zaimplementowany). Branch: `design/abi-v2`.
Punkt wyjścia: [analiza luki](https://github.com/prachwal/CathodeRay/blob/opt/abi-tax/docs/vreg-design.md#11-ryzyka-konkretne-z-historią)
mówi wprost: ~70% luki do cc65/SDCC to trzy decyzje architektoniczne (pamięć-ABI, statyczne ramki,
brak rejestrów dla wartości żywych). Ten dokument projektuje naprawę pierwszej z nich.
Powiązane: [targets.md](targets.md#kontrakt-rejestrowy-cpumodel-plan-38),
[stub-calling-conv.md](stub-calling-conv.md#plan-38-cpumodel-a-zamrożone-abi),
[CpuModel](../src/CathodeRay.C/CpuModels.cs).

## 0. Mapa obecnego ABI v1 (kotwice)

```text
wołający: Value(args) → tymczasowe → Ir.Call(Args) → ByteSelector.EmitCallArgs → cc_argN (pamięć)
wołany:   prolog: cc_argN → komórki parametrów (+ ParamAlias: parametr = cc_argN, gdy liść/martwy)
wynik:    cc_ret (+cc_ret_h) albo HL na Z80/8080 (plan 35); struct → cc_retbuf; long long → cc_retbuf
```

| Element | Kotwica |
| --- | --- |
| Obniżanie wołań (abstrakcyjne `Ir.Call`, bez `cc_arg`) | [Lowering.Calls.cs](../src/CathodeRay.C/Lowering.Calls.cs#L18-L58) |
| Limity: `MaxArgs = 6`, `ArgSlots` (long = 2 sloty) | [TypeChecker.cs](../src/CathodeRay.C/TypeChecker.cs#L452-L452) |
| `long long`/struct przez wskaźnik/bufor | [Lowering.Calls.cs](../src/CathodeRay.C/Lowering.Calls.cs#L70-L135) |
| Emisja args / prolog (`_isa.ArgCell` czyta model) | [ByteSelector.cs](../src/CathodeRay.C/ByteSelector.cs#L293-L300), [ByteIsa.cs](../src/CathodeRay.C/ByteIsa.cs) |
| Alias parametr = `cc_argN` (liście + plan 35 krok 3) | [ParamAlias.cs](../src/CathodeRay.C/ParamAlias.cs#L10-L20) |
| Ramki: `Saved` na cyklach, kontrola stosu | [Lowering.Frames.cs](../src/CathodeRay.C/Lowering.Frames.cs), [Lowering.Calls.cs](../src/CathodeRay.C/Lowering.Calls.cs#L153-L192) |
| Komórki umówione w crt0 (`cc_argN`, `cc_ret`, `cc_retbuf`…) | [Mos6502Isa.cs](../src/CathodeRay.C/Mos6502Isa.cs#L393-L398) |
| Ręczny asembler czytający `cc_arg` | [rt_mul.s / rt_div.s](../src/CathodeRay.C/stdlib/target/z80/rt_mul.s#L1-L9) (×2 CPU) |
| Wariadyki czytają `cc_argN` wprost | [printf.c](../src/CathodeRay.C/stdlib/lib/printf.c) (`cc_arg2..cc_arg6`) |
| Harness czyta wynik z `cc_ret` | [TargetHarness.cs](../tests/CathodeRay.Tests/TargetHarness.cs#L17-L17), [CcRun.cs](../tests/CathodeRay.Tests/CcRun.cs#L94-L94) |
| Model: `ArgRegs = []` = ABI v1 | [CpuModels.cs](../src/CathodeRay.C/CpuModels.cs) |
| Interpreter wiąże parametry wprost (bez `cc_arg`) | [IrInterpreter.cs](../src/CathodeRay.C/IrInterpreter.cs#L209-L211) |

## 1. Cele i nie-cele

**Cele:** wołania tańsze o ~40% na Z80 (`05_calls`: 296→~180 B), `fib`/`sw`/`fnptr` w dół na wszystkich CPU;
jeden mechanizm dla wszystkich celów (dane w `CpuModel`, nie gałęzie `if (cpu)` w selektorze);
`printf`/wariadyki działają bez zmian; default `v1` aż v2 dogoni macierz.

**Nie-cele:** ramki na stosie i induction variables w rejestrach (osobny temat, BEZ łamania ABI —
da się zrobić na v1); zmiana `MaxArgs`/slotów; dotykanie `IrInterpreter` (abstrakcyjny, przeżyje);
nowe cele CPU.

## 2. Decyzje

### D1. Hybryda z pozycyjną numeracją pamięci (klucz migracji)

Pierwsze 3 argumenty (W≤2) idą w rejestry; **reszta — zawsze w `cc_argN` numerowane pozycyjnie
(tak jak dziś), łącznie z nadmiarem wariadycznym**. Konsekwencje:

- `printf.c` czyta `cc_arg2..cc_arg6` — DZIAŁA BEZ ZMIAN (format w rejestrze, nadmiar w pamięci pozycyjnie).
- W4/agregaty/`long long`/wskaźniki zwrotne — kanałami jak dziś (bez zmian).
- Puste sloty pamięci (argument poszedł rejestrem) są nieszkodliwe.
- Limit `MaxArgs = 6` i `ArgSlots` bez zmian.

### D2. Mapowanie na rejestry (docelowe, per CPU)

| CPU | arg1 | arg2 | arg3 | reszta | wynik W1 | wynik W2 |
| --- | --- | --- | --- | --- | --- | --- |
| Z80/8080 | HL | DE | BC | `cc_argN` pozycyjnie | L (plan 35) | HL (plan 35) |
| 6502/65c02/nes/6510 | A (W1) albo A/X lo/hi (W2) | — (pamięć) | — | `cc_argN` | A (NOWE w v2) | A/X lo/hi (NOWE w v2) |
| 6800 | A (W1) albo X (W2) | B (tylko W1) | — | `cc_argN` | A (NOWE w v2) | D, A=hi B=lo (NOWE w v2) |
| stub | — | — | — | `cc_argN` | — | — |

Uwagi:

- W1 w parze/rejestrze 16-bitowym: **młodszy bajt, starszy nieokreślony** (wołany czyta tylko swój W).
  Na 6502 W2 w A/X to dokładnie konwencja cc65 — kopiujemy sprawdzone.
- 6502 celowo tylko PIERWSZY argument w rejestrach (3 rejestry to za mało na więcej bez rozlewania
  gorzej niż dziś; `fib`/`add`/`neg` i tak na tym stoją).
- stub ZAWSZE na v1 (cel referencyjny/oracle — §7).
- `long long`/struct/W4: jak dziś (wskaźnik/bufor), niezależnie od pozycji.

### D3. Wynik zawsze wraca też do `cc_ret` (crt0 dopisuje)

Po `call main` crt0 zapisuje wynik do `cc_ret`/`cc_ret_h` (jak plan 35 dla HL — rozszerzone na wszystkie
CPU i obie ABI). Efekt: **harness (`CcRun`, `TargetHarness`), mapy i narzędzia bez zmian**; koszt 3–5 B
w crt0 raz na program. Decyzja świadomie droższa w bajtach, tańsza w ekosystemie.

### D4. Wołania pośrednie i rekurencja bez zmian kształtu

Wskaźnik funkcji dotychczasowym kanałem (`cc_fp`/`__icall`/komórka); ABI dotyczy tylko argumentów/wyniku.
Ramki: parametr w rejestrze żywy przez wołanie podlega tym samym regułom co dziś
(`Saved`/`SavedAround`, reguły żywotności z planu 35 kroku 3) — push par wokół wołań już to umie.

### D5. Flaga `--abi v1|v2`, default `v1`, wdrożenie per CPU

Jak `--ir cell|vreg` (plan 37): flaga w `CcCommand`, default v1, `--abi v2` na CPU bez implementacji →
czytelny błąd (wzór: `CTargets.Planned`). Kolejność: `nes` → pomiar → `6502/6510/65c02` →
`6800` → `Z80/8080`. Osobne goldeny (`*.abi-v2.txt`, wzór `vreg-sizes.txt`); goldeny v1 ani drgną.
`ParamAlias` dostaje bliźniaka rejestrowego (alias parametru na rejestr argumentu przy tych samych
regułach żywotności) albo gaśnie na v2 — decyzja implementacyjna, kryterium: brak regresji v1.

## 3. Co się zmienia plik po pliku

| Plik | Zmiana |
| --- | --- |
| `CpuModels.cs` | wypełnić `ArgRegs` per CPU (D2); nic więcej |
| `ByteIsa.ArgCell` | gałąź rejestrowa już jest — zaczyna działać (dziś martwa) |
| `ByteSelector` (prolog + `EmitCallArgs`) | pobór args z rejestrów; wynik W1/W2 na 6502/6800 do A/X/D |
| `Lowering.Calls.cs` | BEZ ZMIAN (abstrakcyjne `Ir.Call` — dowód słuszności warstw) |
| `RegisterAllocator` + `SavedAround` | rejestry argumentowe jako pre-kolorowane (żywe na wejściu); reszta bez zmian |
| `ParamAlias` | wariant rejestrowy albo wyłączenie na v2 (D5) |
| `CheckStack` | ramki z rejestrami argumentowymi wliczonymi (te same `_frames`) |
| `crt0` (4 ISA) | dopisanie wyniku do `cc_ret` (D3) |
| `rt_mul.s` / `rt_div.s` (2 CPU) | przepisanie na v2 (args w HL/DE) — JEDYNE ręczne `.s` do ruszenia |
| `printf.c`, reszta stdlib `.c` | bez zmian (D1) |
| `CcCommand` | `--abi v1\|v2` (wzór `--ir`) |
| `CcRun` / `TargetHarness` / mapy | bez zmian (D3) |
| `IrInterpreter` | bez zmian (wiąże wprost) |
| `compare.py` / goldeny | kolumna/tabela v2 obok v1 (nie zamiast) |

## 4. Migracja i bramki

1. `nes`: implementacja + pełna macierz v2 vs v1 (wartość+konsola na każdym samplu) + goldeny v2.
2. Pomiar: `05_calls`-podobne na `nes` v1 vs v2 (oczekiwane −30–40% w wołanimach); jeśli poniżej −15%, STOP i rewizja D2.
3. Kolejne CPU w kolejności z D5, każde z macierzą i goldenami.
4. Default zostaje `v1` do pełnej macierzy v2 na wszystkich CPU (osobna decyzja + plan, jak zmiana defaultu `--ir`).

Bramki każdego kroku: `build -warnaserror`, full suite zielony, **goldeny v1 bajt-w-bajt**,
`--abi v2` na CPU spoza listy = czytelny błąd, `target-sizes.txt` (v1) nie rośnie.

## 5. Pomiary docelowe (prognoza z raportu ABI)

| Program | dziś (v1) | cel v2 | skąd |
| --- | --- | --- | --- |
| `05_calls.c` Z80 | 296 B | ~180 B | args/wyniki w HL/DE/BC zamiast 16+16 B transferów |
| `fib` 6502 | 144 B | ~110 B | arg `n` w A/X, wynik w A (ramek to nie rusza) |
| `sw` 6502 | 120→69 B (po fixach z raportu) | ~60 B | + wynik w A (−12 B) |
| `fnptr` Z80 | 76 B | ~45 B | tail-call przez join + args w rejestrach |

## 6. Ryzyka i mitigacje

| Ryzyko | Mitigacja |
| --- | --- |
| Wariadyki (`printf`) | D1: nadmiar zawsze w `cc_arg` pozycyjnie — `printf.c` nietknięty, testy `CLibTests` pilnują |
| Ręczne `.s` (4 pliki) | tylko `rt_mul`/`rt_div` ×2 CPU; reszta stdlib to `.c` (idzie potokiem) |
| Rekurencja + arg w rejestrze | D4: te same reguły `Saved`/`SavedAround` (testy `RecursionFrameTests` × `--abi v2`) |
| Wołania pośrednie | kanał wskaźnika bez zmian; `TailCallTests` × v2 |
| Dryf v1 przy refaktorze | goldeny v1 w bramce każdego kroku (jak plan 38 zad. 3/5) |
| Dwa ABI w głowie | flaga + osobne goldeny + default v1; pilotaż na `nes` (zero ekosystemu) |
| Harness/tools | D3 (writeback w crt0); `IrInterpreter` abstrakcyjny |

## 7. Fazy (rozwinięcie w planie 39)

1. Flaga `--abi` + szkielet (v2 czytelnie niedostępne) + `ArgRegs` w modelu.
2. Pilot `nes`: prolog/`EmitCallArgs`/wynik w A + `rt` (brak `.s` na nes? — weryfikacja) + crt0 writeback.
3. Macierz `nes` v1 vs v2 + goldeny v2 + bramka −15%.
4. `6502/6510/65c02`, potem `6800`, potem `Z80/8080` (każdy: implementacja + macierz + goldeny).
5. `ParamAlias` rejestrowy albo wygaszenie na v2.
6. Decyzja o defaulcie (osobny plan).

## Powiązane

- [vreg-design.md §D1](vreg-design.md#d1-vreg-opada-do-cell-ir-adapter-nie-drugi-backend) — adapterowa filozofia (tu: hybryda zamiast drugiego ABI).
- [minic-optimization.md](minic-optimization.md#ecosystem-limitations) — zamrożone ABI jako organizm.
- Plan pracy: `docs/plans/39-abi-v2.json` (queued).
