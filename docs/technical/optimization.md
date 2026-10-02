# Optymalizacja, VReg i pomiary

Źródła: [../minic-optimization.md](../minic-optimization.md), [../vreg-design.md](../vreg-design.md), [../ir-vreg-plan.md](../ir-vreg-plan.md),
[../compare.md](../compare.md), [../vreg-alloc.md](../vreg-alloc.md), [../vreg-sizes.md](../vreg-sizes.md), plany 32, 33, 35–38, 40.

## Stan optymalizacji

Potok jest celowo płytki w porównaniu z GCC lub LLVM.

| Obszar | Stan |
| --- | --- |
| Peephole lokalny | jest, zależny od celu |
| Składanie stałych, redukcja siły dla potęg dwójki | jest w przebiegach IR |
| Usuwanie martwych tempów | jest |
| CSE między blokami | brak w ścieżce Cell, w VReg są CSE i CSE kopii lokalnie |
| Optymalizacje pętli (LICM, rozwijanie, indukcja) | zalążkowe |
| Wektoryzacja | brak (na 8-bitowych celach mało wartościowa) |
| Alokacja rejestrów | Cell: zachłanna na komórkach absolutnych. VReg: konserwatywna oraz linear-scan |
| PGO | brak, tylko skrypt `hotspots.py` |

Powody: cele mają bardzo mało rejestrów, więc alokator grafowy rzadko się opłaca. Komórki absolutne upraszczają mapowanie IR na asembler
i interpreter-wyrocznię, ale nie modelują żywotności. Priorytetem była poprawna semantyka wielu celów i czysta granica IR, a nie szczytowa wydajność.

## Projekt VReg (plan 37, zamknięty)

Główne decyzje z `vreg-design.md`:

- **D1. VReg opada do Cell IR.** Po alokacji moduł jest przepisywany na `Ir.Module` (vreg staje się syntetyczną komórką), więc całe ABI,
  crt0, wywołania ogonowe i `callSave` działają bez zmian. Drugi backend oznaczałby powielenie ABI.
- **D2. Spill to statyczna komórka absolutna**, a nie slot na stosie, bo 6502, stub i 6800 nie adresują względem wskaźnika stosu.
  Rekurencja działa przez istniejący mechanizm `Saved` (vregi żywe po wywołaniach, parametry, adresy wzięte).
- **D3. Ponowne użycie.** Żywotność adaptuje `IrLiveness` (punkt stały na grafie bloków), fakty o użyciach wzorują się na `IrFacts`,
  a konserwatywny alokator to dotychczasowy `RegisterAllocator`.
- **D4. Fabryka dopiero z drugą implementacją.** Na początku wystarczyła flaga `--ir cell|vreg`.
- **D5. Minimalny zestaw instrukcji:** `mov`, `bin`, `un`, `load`, `store`, `cmp`, `br`, `jmp`, `call`, `ret`. Phi pojawi się dopiero przy SSA.
- **D6. Warianty CPU przez rejestr celów**, np. `Mos6502Target(nes: true)`, bez nowych fabryk.

Zakres wykonany w planie 37: model rekordów i `VRegInterpreter` (wyrocznia przed alokacją), fakty, żywotność i przebiegi (CSE, coalesce, DCE),
`AccumulatorAllocator` (spill-all), `LinearScanAllocator`, `--ir vreg` end-to-end, warianty `nes` i `6510`, osobne goldeny rozmiarów dla VReg.
Matryca `VRegMatrixTests` (26 programów × 5 celów) daje wartość i wyjście konsoli równe wersji Cell.

Wynik pomiaru linear-scan względem alokatora konserwatywnego: 278 z 660 wartości trzymanych w rejestrach na próbkach.
Keep-set jest doradczy do czasu bezpośredniego emitera. SSA i GVN świadomie odroczono do decyzji opartej na pomiarach.

## Wyniki rozmiaru kodu

Porównanie z kompilatorami wzorcowymi (`docs/compare.md`, segment CODE w bajtach, 14 małych funkcji z `samples/bench`):

| funkcja | mini-C 6502 | cc65 -Os | mini-C Z80 | SDCC Z80 |
| --- | ---: | ---: | ---: | ---: |
| fib | 144 | 52 | 76 | 31 |
| copy | 42 | 74 | 48 | 30 |
| max3 | 83 | 70 | 55 | 53 |
| sum_bytes | 54 | 71 | 38 | 37 |
| str_len | 43 | 56 | 25 | 14 |

Średnia geometryczna bez wierszy mnożenia, dzielenia i przesunięć (oznaczonych `*`): mini-C / cc65 = 1,32, mini-C / SDCC = 1,31.
Pełna tabela jest w `compare.md`. Odświeża ją `python3 tools/compare.py --write` (wymaga zainstalowanych cc65 i SDCC).
Uwaga: `targets.md` zawiera starsze, mniej korzystne wartości (ok. 1,7× cc65 i ok. 3,6× SDCC), zapisane przed pracami z planów 32–40.

Rozmiary na wszystkich celach i programach przykładowych: [../target-sizes.md](../target-sizes.md) (generowane testem `TargetSizeTests`).

## Kierunki (plan 40, w toku: 5 z 11 zadań gotowych)

Plan 40 domyka lukę do SDCC na Z80 i 8080. Zrobione: `xor a` zamiast `ld a,0` i martwa stopka po skoku końcowym (1), częściowo `ex de,hl` (2),
skoki po stałej ze znakiem i redukcja siły dla `x+x` (3, 4), `LDIR` dla pętli kopiujących: copy 48 → 22 B (5), zawężenie `switch` na `uchar` do 1 bajta (11).
Zostały: DJNZ (6), RST (7, świadomie odroczone), tail-call przez join (8), ramka i indukcja w parach rejestrów (9, zadanie największe) i pomiary zamykające (10).
Szczegółowa analiza: [../plans/40-z80-codegen.md](../plans/40-z80-codegen.md).

Dalej: nowe ABI z argumentami w rejestrach (`ArgRegs` w `CpuModel`, pilotaż na NES/6510 jako osobny plan) oraz SSA/GVN i alokacja grafowa
wyłącznie wtedy, gdy pomiary je uzasadnią.

## Ograniczenia ekosystemu

- Informacje debugowe to tylko mapa (`--map`), bez DWARF i sanitizerów.
- Biblioteka standardowa to podzbiór, a nie pełna libc.
- Dla krytycznych pętli sensownym narzędziem pozostaje ręczny asembler lub dostrajanie źródła.
