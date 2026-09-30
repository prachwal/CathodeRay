# Mini-C: analiza rozmiaru kodu i plan wyniku w HL (Z80/8080)

Dokument zbiorczy z analizy codegen (wrzesień 2026), porównania z cc65/SDCC/sccz80
oraz szczegółowego planu diff dla ABI wyniku w rejestrze HL.

- Repo: https://github.com/prachwal/CathodeRay
- Branch odniesienia: `stub/c-performance` (ok. HEAD planu 33)
- Benchmarki: `samples/bench/fib.c`, `max3.c`

---

## 1. Kontekst projektu

CathodeRay to toolchain C# (.NET 10): asembler + linker + mini-C z IR i generatorami
na **stub**, **6502/65C02**, **Z80**, **8080**, **6800**.

Pipeline:

```text
C → preprocessor → lexer → parser → typecheck → lowering → IR
  → passes (const/copy, DCE, inliner)
  → Legalizer / WideLegalizer → [RegisterAllocator]
  → ByteSelector → ByteIsa (per-CPU) → peephole / branch relax
  → asembler → linker (+ crt0 + runtime)
```

Model wartości wyrósł ze stuba: **komórki w BSS**, argumenty `cc_argN`, wynik `cc_ret`,
ramki callee-saves na sprzętowym stosie przy rekurencji. Rejestry (B/C/D/E na Z80)
to opcjonalny cache (`RegisterAllocator`), nie domyślne miejsce wartości.

---

## 2. Porównanie z kompilatorami referencyjnymi

Metodyka: bajty **samej funkcji** (bez pełnego runtime ref), `cathode cc --cpu <cpu>`.

| Program | Cel | Nasz | Ref | Stosunek |
|---------|-----|-----:|----:|---------:|
| fib | 6502 | 144 B | cc65 74 B | **1.95×** |
| max3 | 6502 | 83 B | cc65 90 B | **0.92×** |
| fib | Z80 | 95 B | SDCC 31 B | **3.06×** |
| max3 | Z80 | 61 B | SDCC 53 B | **1.15×** |
| fib | 8080 | 116 B | sccz80 ~50 B (+ runtime) | **~2.3×** |
| max3 | 8080 | ~89 B | sccz80 ~90 B (+ helpers) | **~1.0×** |
| fib | 6800 | 140 B (było 193 B przed planem 33 kr. 19-20) | (brak ref C) | vs 6502 144 B: porównywalnie |
| max3 | 6800 | 95 B (było 148 B) | — | vs 6502 83 B |

**Wniosek przekrojowy:** stosunek zależy od kształtu kodu, nie od „złych instrukcji”.
Wołania/rekurencja 2–3×; prosty kod już ~0.9–1.2× ref.

Tabela szersza (`docs/compare.md`, geometryczna bez mul/div): mini-C/cc65 ≈ 1.73,
mini-C/SDCC ≈ 3.52.

---

## 3. Dlaczego wynik jest duży — elementy codegen

### 3.1. Model pamięci

`ByteSelector` traktuje skalar jako adres w pamięci:

- parametry → `cc_arg1`…`cc_arg6` (+ `_h`)
- wynik → `cc_ret` / `cc_ret_h`
- lokale/tempy → symbole BSS
- A (i HL na Z80) to scratch prymitywów, nie „domyślny rejestr zmiennej”

Referencje (cc65, SDCC) to maszyny rejestr/stos wokół kanału A/X lub HL/DE.

### 3.2. Ramki rekurencyjne (`Lowering.Frames`)

Ramka tylko gdy funkcja leży na cyklu grafu wołań. Wtedy:

1. Liveness (`IrLiveness`) zbiera komórki żywe po `Call`.
2. Trafiają do `Function.Saved` (+ parametry, `AddrOf`).
3. Prolog: push każdego bajtu (na Z80 często `ld a,(n); push af`).
4. Epilog: pop + wynik przez `cc_ret`, potem `ret`.

Na fib to dominujący koszt względem SDCC (prawie zerowy prolog, n w DE, wynik w HL).

### 3.3. Porównania ze znakiem

Wspólny idiom: bias `eor #128` / `xor 80h` + `sbc #80` + skok po overflow.
Na max3 (inline vs runtime `tosicmp`) bywa **lepszy** wynik niż cc65.
Rezerwa: operandy w rejestrach, specjalizacja `== 0` / stała.

### 3.4. RegisterAllocator (Z80/8080)

**Jest:** pula B,C,D,E i pary BC/DE; liveness na CFG; koszt push/pop wokół `Call`;
ParamAlias; usuwanie z Saved komórek w reg.

**Nie ma (jeszcze):**

- wyniku w HL zamiast `cc_ret`
- arg1 live-in w HL
- alokacji na 6502/6800
- push pary w prologu Saved zamiast `push af` po bajtach

### 3.5. Per-CPU

| Cel | Mocne | Słabe |
|-----|--------|--------|
| **6502** | ZP, peephole, TryStep | brak regalloc; wynik przez `cc_ret` |
| **Z80** | TryAddWord, jr, regalloc, P/V | wynik/arg w pamięci; `push af` w ramce |
| **8080** | lhld/shld | jak Z80 + brak jr, `push psw` |
| **6800** | BE poprawne | brak direct page, mało ldx/stx, pełne kopie param |

---

## 4. Priorytety optymalizacji (ROI)

| Priorytet | Zmiana | Cele | Efekt |
|-----------|--------|------|--------|
| **P0** | Wynik W≤2 w rejestrze (HL / A:X) | Z80, 8080, potem 6502 | −4…12 B na wołanie; fib mocno |
| ~~P0~~ | ~~6800 direct page + TryMoveWord~~ (zrobione: plan 33 kr. 19-20, max3 148→95 B, fib 193→140 B) | 6800 | — |
| **P1** | Prolog Saved: `push bc`/`de` nie `push af` | Z80, 8080 | −30% prologu ramki |
| **P1** | Arg1 live-in w HL | Z80, 8080 | mniej `ld hl,(cc_arg1)` |
| **P1** | Mniej kopiowania param → komórki funkcji | wszystkie | krótszy prolog |
| **P2** | Lekki cache X na 6502 | 6502 | fib bliżej 1.3–1.5× |
| **P2** | Dalsze Try* (dec hl, cmp0) | wszystkie | kilka % |

Kolejność bezpieczna (po weryfikacji z listingiem `fib` Z80: push par w Saved oszczędza ok. 8 B bez zmiany ABI, wynik w HL ok. 10-14 B, ale dotyka rutyn asemblerowych `rt_mul.s`/`rt_div.s`, które zwracają przez `cc_ret`): push par → wynik HL (z regułą dla asm rt) → arg w HL → 6502. Zob. plan 35.

Po każdym kroku: `dotnet test` + kolumna size **nie rośnie**.

---

## 5. Plan diff: wynik skalarny w HL (Z80 / 8080)

### 5.1. Kontrakt ABI

| Szerokość | Rejestr wyniku | Uwagi |
|-----------|----------------|--------|
| 1 B | **L** | po `ld l,a` |
| 2 B | **HL** | LE: L=lo, H=hi |
| 4 B+ / struct / float | bez zmian | `cc_ret`, `cc_rethi`, `cc_retbuf` |

- HL **nie** jest w `CellPairs` / `SavedAround` (tylko `bc`, `de`).
- Po `call main` crt0: `ld (cc_ret),hl` (8080: `shld cc_ret`), potem `halt` —
  testy nadal czytają `cc_ret` z pamięci.
- Stub / 6502 / 6800: bez zmian w tym PR.

### 5.2. Pliki

```
src/CathodeRay.C/ByteIsa.cs           + ReturnsInResultReg, TryMoveTo/FromResultReg
src/CathodeRay.C/Z80Isa.cs           + implementacja + crt0
src/CathodeRay.C/Intel8080Isa.cs     + to samo (mnemoniki Intel)
src/CathodeRay.C/ByteSelector.cs     ~ EmitRet, EmitCall
docs/stub-calling-conv.md            ~ sekcja Z80/8080
docs/targets.md                      ~ jedna linia
tests/.../target-sizes.txt           tylko po UPDATE gdy ↓
```

### 5.3. ByteIsa — API

```csharp
public virtual bool ReturnsInResultReg => false;

public virtual bool TryMoveToResultReg(Ir.Op value, int width) => false;

public virtual bool TryMoveFromResultReg(Ir.Cell dst, int width) => false;
```

### 5.4. Z80Isa / Intel8080Isa

- `ReturnsInResultReg => true`
- `TryMoveToResultReg`: imm → `ld hl,nn`; para reg → `ld l,c; ld h,b`; komórka → `ld hl,(sym)`; W=1 → A potem `ld l,a`
- `TryMoveFromResultReg`: `ld (dst),hl` / para reg / `ld a,l` + store
- crt0 po `call main`: `ld (cc_ret),hl` / `shld cc_ret`

### 5.5. ByteSelector.EmitRet

Gdy `ret.Value != null` i `ret.W <= 2` i `_isa.ReturnsInResultReg`:

- `TryMoveToResultReg(ret.Value, ret.W)` (z fallbackiem bajtowym do L/H)
- **nie** store do `cc_ret`

Inaczej: dotychczasowa ścieżka `cc_ret`.

Ustawienie HL **przed** skokiem do etykiety `ret` jest poprawne: epilog to `pop af` /
`pop bc|de` — HL nietknięte.

### 5.6. ByteSelector.EmitCall

Po `call` i `PopPair(saved)`:

- jeśli wynik W≤2 i `ReturnsInResultReg` → `TryMoveFromResultReg(call.Result, W)`
- inaczej → odczyt z `cc_ret` jak dziś

Invariant: `SavedAround` nigdy nie zawiera `hl`.

### 5.7. Kolejność commitów

1. API ByteIsa + implementacja Z80/8080 (jeszcze nieużywane) + crt0 most
2. EmitRet + EmitCall
3. docs + assert na pary Saved

Message: `perf(c): wynik W<=2 w HL na Z80/8080`

### 5.8. Testy

```bash
dotnet test --nologo --filter "IrConformance|TargetMatrix|RandomIr"
UPDATE_TARGET_SIZES=1 dotnet test --nologo --filter TargetSizeTests
# git diff tests/CathodeRay.Tests/target-sizes.txt  — z80/8080 ≤ baseline
```

Ręcznie: `cathode cc samples/bench/fib.c --cpu z80 -l` — brak zbędnego
`ld hl,(cc_ret)` po własnym `call`; wynik w HL przed `ret`.

### 5.9. Oczekiwany efekt

| Benchmark | Dziś (orient.) | Po HL-ret |
|-----------|---------------|-----------|
| fib Z80 | ~95 B (~3.0× SDCC) | ~75–85 B |
| max3 Z80 | ~61 B (~1.15×) | ≤55–58 B |

Dalsze cięcie: arg w HL, `dec hl` na ścieżce argów, push par w Saved.

### 5.10. Pułapki

1. Nie dodawać HL jako scratch w epilogu Saved między ustawieniem wyniku a `ret`.
2. W=1: ABI to **L**, nie A.
3. `long` / struct: stara ścieżka pamięciowa.
4. Size: stub/6502/6800 nie mogą urosnąć (wspólny `ByteSelector` — branchuj na `ReturnsInResultReg`).
5. Nie łączyć z „arg1 w HL” w tym samym PR.

---

## 6. Stan planów (orientacyjny)

| Plan | Temat | Status |
|------|--------|--------|
| 30 | Cele 6502/Z80/8080/6800, long, void* | zamknięty |
| 32 | Wydajność 6502 / peephole | wstrzymany (kroki 5 i 9 zastąpione planem 33) |
| 33 | Z80 regalloc, Try*, 8080, 6800 DP | zamknięty (20/20; HL-ret = plan 35) |
| 34 | VM bytecode | kolejka — nie zmniejsza AOT |

---

## 7. Jak odtworzyć pomiary

```bash
dotnet build src/CathodeRay.Cli
python3 tools/disasm_compare.py --cpu 6502 --show fib max3
python3 tools/disasm_compare.py --cpu z80 --show fib max3
cathode cc samples/bench/fib.c -o f.bin --cpu 8080 -l f.lst
cathode cc samples/bench/fib.c -o f.bin --cpu 6800 -l f.lst
python3 tools/compare.py --write   # odświeża docs/compare.md
```

---

## 8. Podsumowanie

Rozmiar boli głównie przez **model „wartość = komórka BSS + pełna ramka”**,
nie przez jakość pojedynczych instrukcji. Prosty kod jest już blisko ref;
rekurencja płaci ABI. Największy sensowny krok na Z80/8080 to **wynik W≤2 w HL**
(ten dokument, §5), potem arg live-in i tańszy prolog Saved; na 6800 — direct page.
