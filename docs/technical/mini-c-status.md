# Mini-C — co zaimplementowane, a co przed nami

Stan na main: plany 43 (DJNZ), 45 (łańcuchy `jmp`), 46 (bazowy 6809) i 47 (optymalizacje 6809) zamknięte; w kolejce plan 34 (VM bytecode). Plan 32 pozostaje queued z jedną historyczną pozycją partial, której cel zrealizował później plan 45. Specyfikacja języka: [../minic.md](../minic.md).
Szczegóły architektury: [architecture.md](architecture.md), cele i ABI: [targets-and-abi.md](targets-and-abi.md),
optymalizacje: [optimization.md](optimization.md).

## 1. Struktura kompilatora (skrót)

```text
.c → CPreprocessor → Lexer → Parser → TypeChecker → Lowering (Wide8Legalizer → IrPasses) → IrInliner
   → cel.Emit: CaseFold → Legalizer → WideLegalizer → Legalizer → [IndexFusion, ZP: 6502]
              → ByteSelector (ByteIsa) → Peephole / BranchRelaxer → asembler → linker (+ crt0, stdlib)
```

- **Frontend** nie zna CPU; **cel** to ~150 linii (`XxxIsa : ByteIsa`) + `XxxTarget : ByteTarget` + crt0.
- **Cell IR** (produkcyjny, default) — komórki absolutne W=1/2/4; wyrocznia `IrInterpreter`.
- **VReg IR** (plan 37, flaga `--ir vreg`) — opada do Cell IR, więc ABI/crt0 działają bez zmian.
- Cele: `stub 6502 65c02 nes 6510 z80 8080 6800 6809` (LE, oprócz 6800/6809 BE; 6809 domyślnie ABI v1, opt-in v2 dla argumentów W2 w X/Y/U, wynik przez `cc_ret`; DP=$00).

## 2. Funkcjonalności języka — co działa (wszystko closed, plany 17–31)

| Obszar | Zakres | Plan |
| --- | --- | --- |
| Typy 8/16-bit | `uchar/schar/int/uint`, arytmetyka, promocje | 17, 18, 26 |
| Wskaźniki i tablice | `*p`, `p[i]`, `p-q`, wielowymiarowe, `sizeof` | 24, 27, 30 |
| Struktury | pola, `typedef`, `sizeof`, kopiowanie, **przez wartość** (limit 64 B, `cc_retbuf`) | 28, 30 |
| Preprocesor: komentarze w dyrektywach, makra wariadyczne | `/* */` i `//` po `#include`/`#define`/`#if`; `...` i `__VA_ARGS__` | 48 |
| `typedef` typu tablicowego | `typedef int jmp_buf[3]`: zmienne, parametry (rozpad na wskaźnik), `sizeof` | 48 |
| `setjmp`/`longjmp` | z80, 8080, 6502, 6800, 6809; funkcja z `setjmp` trzyma komórki w pamięci | 48 |
| `stdint.h`, `FILE` i strumienie, `snprintf`, `strtod`/`atof` | konsola jako `stdout`/`stderr`, `fopen` zwraca NULL; `%g/%f/%e` tylko w `snprintf` | 48 |
| `long`/`ulong` | 32 bity, `WideLegalizer` (połówki 16-bit), `%ld/%lu/%lx` | 30 |
| `float`/`double` | IEEE-754 programowo (`rt_float.c`), bez `%f` | 31 |
| `long long` | 64 bity (`Wide8Legalizer` + `rt_ll64.c`), bez `%lld` | 31 |
| Pola bitowe | nazwane pola w jednostce typu, odczyt-modyfikacja-zapis | 31 |
| `void *`, `size_t`, `offsetof`, `enum` | konwersje z kontrolą `const`, `f(void)` | 27, 30 |
| Wskaźniki do funkcji | deklaracje, tablice, wołania, `...` w prototypach | 29 |
| Sterowanie | `if/while/do/for/switch/break/continue/goto` | 26, 27 |
| Preprocesor | `#include/#define/#if/#error/#pragma once`, `#`/`##`, `-D`, `--incdir` | 21, 29 |
| `static/extern/const/volatile/inline` | semantyka modułowa, `volatile` bez optymalizacji | 29, 31 |
| Biblioteka | `string/ctype/stdlib/stdio` (podzbiór), `printf` do 5 arg, linkowanie na żądanie | 22, 25, 29 |
| Ramki i rekurencja | callee-saves na stosie sprzętowym, kontrola głębokości | 20 |
| Funkcje | do 6 argumentów, rekurencja, inlining liści (`IrInliner`) | 16, 31 |

Świadome braki (sekcja „Nie działa” w `minic.md`): `%f`/`%lld` w `printf` (zamiast `ftoa`/`lltoa`),
`long double`, funkcje matematyczne, konwersja `float` ↔ `long long`, anonimowe pola bitowe,
funkcja zwracająca wskaźnik do funkcji, struktury > 64 B przez wartość.

## 3. Optymalizatory — co gdzie działa

| Przebieg | Wspólne | 6502 / 65C02 / nes / 6510 | Z80 / 8080 | 6800 | 6809 |
| --- | --- | --- | --- | --- | --- |
| Składanie stałych, siła dla 2^n | ✅ `IrPasses` | ✅ | ✅ | ✅ | ✅ |
| Martwe tempy, forward temps | ✅ | ✅ | ✅ | ✅ | ✅ |
| Inlining liści (≤10 instr. IR; `static` z 1 wołaniem: 40) | ✅ `IrInliner` | ✅ | ✅ | ✅ | ✅ |
| ParamAlias (parametr = `cc_argN`) | ✅ LE | ✅ | ✅ | ❌ (BE, czeka) | ❌ (BE, czeka) |
| Argumenty rejestrowe ABI v2 | — | ✅ pilot NES (A/X) | ❌ | ❌ | ✅ opt-in: W2 0–2 w X/Y/U; W1/dalsze przez pamięć |
| Alokator komórek w rejestry | — | ❌ (czeka na liveness, plan 40/42) | ✅ BC/DE (+HL wynik) | ❌ | ❌ |
| Wynik W≤2 w rejestrze | — | ❌ (`cc_ret`; v2: A/X w planie) | ✅ HL (plan 35) | ❌ (`cc_ret`) | ❌ (`cc_ret`) |
| Słowa przez pary (`add hl,de`) | — | ❌ | ✅ Z80 (`TryAddWord`); 8080 częściowo | ❌ | ❌ |
| Porównania ze znakiem (forma 0) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Porównania ze znakiem (stała) | — | ❌ (trampolina) | ✅ Z80; 8080 świadomie nie | ❌ (trampolina) | ❌ (trampolina) |
| Peephole `Tidy` | — | ❌ | ✅ | ❌ | ❌ |
| Relaksacja skoków | — | ✅ `BranchRelaxer` | ✅ (`jr`, `DropJumpToNext`) | ✅ `BranchRelaxer` | ✅ `BranchRelaxer` |
| Tail call | — | ✅ `jmp` | ✅ `jp`/`jp (hl)` | ❌ | ❌ |
| Strona zerowa / direct | — | ✅ ZP (`z:`, do 16 B/moduł) | — | częściowo (formy direct) | częściowo (DP=$00, ZP) |
| `IndexFusion` (`lda tab,x`) | — | ✅ | ❌ | ❌ | ❌ |
| Kopie bloków (LDIR) | matcher w selektorze | ❌ (brak rozkazu) | ✅ Z80 | ❌ (brak rozkazu) | ❌ |
| `switch uchar` → 1 B | ✅ `LowerSwitch` | ✅ | ✅ | ✅ | ✅ |
| Mnożenie przez stałą | ✅ `Legalizer` | ✅ | ✅ | ✅ | ✅ |
| DJNZ | — | — (brak rozkazu) | ✅ Z80 (plan 43); — 8080 | — (brak rozkazu) | — (brak rozkazu) |
| VReg: CSE/DCE/linear-scan | ✅ (`--ir vreg`) | ✅ | ✅ | ✅ | ✅ |

Szczegóły per CPU: [`../codegen/opt-z80.md`](../codegen/opt-z80.md), [`../codegen/opt-8080.md`](../codegen/opt-8080.md),
[`../codegen/opt-6502.md`](../codegen/opt-6502.md), [`../codegen/opt-6800.md`](../codegen/opt-6800.md),
[`../codegen/opt-6809.md`](../codegen/opt-6809.md).

## 4. Pomiary (skrót; pełne: [`../compare.md`](../compare.md))

Średnia geometryczna (bez mnożenia/dzielenia/przesunięć): **mini-C / cc65 = 1.19**, **mini-C / SDCC = 1.36** (`docs/compare.md`, 26 benchy).
Największe luki: wywołania i rekurencja (`fib`, `sw`, `fnptr`). ABI v2 pozostaje opt-in na NES i 6809; v1 jest nadal domyślne. Plan 47 mierzy dalsze optymalizacje 6809.

## 5. Co przed nami

| Temat | Status | Zakres |
| --- | --- | --- |
| Liczniki pętli + DJNZ (Z80) | ✅ plan 43 zamknięty | Matcher, pre-kolor licznika do B i emisja `djnz`; 8080 bez zmian |
| Maszyna wirtualna (bytecode) | plan 34 queued, 0/10 | format IR, interpreter, fuzz bytecode vs AOT i porty asemblerowe |
| Łańcuchy `jmp` | ✅ plan 45 zamknięty | 569 bezpiecznych łańcuchów; `07_control.c` stub 623→605 B; ryzykowne wejścia zachowane |
| ABI v2 jako default | odroczone (plan 39 closed) | Bramka dla pilota NES nie przeszła; v2 pozostaje opt-in na NES i 6809, v1 jest domyślne |
| SSA / GVN / LICM | odroczone decyzją pomiarową | tylko gdy pomiary wskażą globalne straty (plan 37) |
| Alokator grafowy | odroczony | jw. — linear-scan wystarcza na 8-bitowce |
| COBOL / Fortran | propozycje (`../language/`) | osobne semantic IR → VReg; nie zaczęte |
| Optymalizacje 6809 | plan 47 closed, 7/7 | ABI v2 argumentów, direct-page `TryStep` i pętla kopii X/Y; full suite, goldeny bez wzrostu, leaf i recursion fuzz 60/60 zielone |
| Nowe CPU | plan 46 zamknięty dla 6809 | Bazowy target, assembler, emulator, testy i goldeny; kolejne porty według [targets-and-abi.md](targets-and-abi.md) |

Historia prac: [`../plans/`](../plans/) (`NN-nazwa.json` jest źródłem planu, `.md` to render).
Czeklista gapów z mapowaniem na plany: [gaps-checklist.md](gaps-checklist.md).

Wyniki planu 44 (pomiary, closed): P1 NO-GO (ruch `cc_ret` max 10,6% < 15% — samodzielny wynik w A/X skreślony),
P2 NO-GO (v2 vs v1: najlepszy bench fib −5,2% przy bramce −15% — planu 39 nie wznawiać),
P3 NO-GO (dostępne duplikaty 2,7%, niezmienniki 1,8% < 3% — SSA/GVN odłożone),
P4 GO (569 łańcuchów `jmp` na stubie, ~2–3% CODE → plan 45),
T1 zrobione (`FrameModelTests` + mutacja kontrolna).
