# Optymalizacja, VReg i pomiary

Źródła: [../minic-optimization.md](../codegen/minic-optimization.md), [../vreg-design.md](../ir-vreg-abi/vreg-design.md), [../ir-vreg-plan.md](../ir-vreg-abi/ir-vreg-plan.md),
[../compare.md](../compare.md), [../vreg-alloc.md](../ir-vreg-abi/vreg-alloc.md), [../vreg-sizes.md](../vreg-sizes.md), plany 32, 33, 35–38, 40.

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
| fib | 142 | 52 | 68 | 31 |
| copy | 42 | 74 | 22 | 30 |
| max3 | 77 | 70 | 55 | 53 |
| sum_bytes | 54 | 71 | 38 | 37 |
| str_len | 43 | 56 | 25 | 14 |

Średnia geometryczna bez wierszy mnożenia, dzielenia i przesunięć (oznaczonych `*`): mini-C / cc65 = 1.27, mini-C / SDCC = 1.15.
Pełna tabela jest w `compare.md`. Odświeża ją `python3 tools/compare.py --write` (wymaga zainstalowanych cc65 i SDCC).

Rozmiary na wszystkich celach i programach przykładowych: [../target-sizes.md](../target-sizes.md) (generowane testem `TargetSizeTests`).

## Kierunki i aktualne plany

Plany 40–43 są zamknięte: Z80/8080 mają alokację, tail-call i optymalizacje pętli; plan 43 dodał `djnz` na Z80 (8080 bez zmian). Plan 45 domknął bezpieczne łańcuchy `jmp` na stubie. RST dla helperów pozostaje świadomie odrzucone ze względu na niski zysk i stały koszt wektorów/runtime.

Plan 47 jest zamknięty: ABI v2 z argumentami W2 w X/Y/U, direct-page `TryStep` oraz kopie przez X/Y z U jako licznikiem. Wynik W≤2 w D i TFR do ustawienia drugiego wskaźnika odrzucono po pomiarze. Dane i status per CPU: [opt-6809.md](../codegen/opt-6809.md).

Plan 34 (VM bytecode) pozostaje w kolejce. ABI v2 nie staje się domyślne: pilot NES nie przeszedł bramki rozmiaru, a 6809 udostępnia v2 tylko opt-in. SSA/GVN, LICM i alokator grafowy pozostają odłożone do czasu pojawienia się pozytywnego pomiaru.

## Ograniczenia ekosystemu

- Informacje debugowe to tylko mapa (`--map`), bez DWARF i sanitizerów.
- Biblioteka standardowa to podzbiór, a nie pełna libc.
- Dla krytycznych pętli sensownym narzędziem pozostaje ręczny asembler lub dostrajanie źródła.
