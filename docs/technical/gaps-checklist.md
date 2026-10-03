# Czeklista gapów architektury

Źródło analizy: odpowiedź „co jest błędne” + [`mini-c-status.md`](mini-c-status.md) §5.
Odznaczaj po dowiezieniu (listing + `compare.py` + goldeny bez wzrostu).

Flagi statusu: `closed` (zrobione), `queued` (plan istnieje, czeka), `partial` (zaczęte, wstrzymane),
`propozycja` (task bez planu — kandydat do `docs/plans/`).

## Mapa gap → plan (skrót)

| Gap | Plan? |
| --- | --- |
| DJNZ + dług splitów | ✅ plan 43 (queued, 0/8) |
| VM bytecode | ✅ plan 34 (queued, 0/4) |
| Łańcuchy `jmp` | ⚠️ plan 32 poz. 9 partial (wstrzymany) + task re-pomiaru w [Cele i codegen](#cele-i-codegen) |
| Wynik w A/X na 6502, ABI v2 cd., bramka −15% | ❌ brak planu → taski `propozycja` w [Luka ABI](#luka-abi-największa) |
| CSE/LICM, emiter VReg, alokator grafowy | ❌ brak planu → taski `propozycja` w [Model wartości i IR](#model-wartości-i-ir) |
| ParamAlias BE, tail call 6800/z ramką, forward-ref | ❌ brak planu → taski `propozycja` w [Cele i codegen](#cele-i-codegen) |
| Ślepa wyrocznia ramek | ❌ brak planu → task `propozycja` w [Metodologia testów](#metodologia-testów) |

## Luka ABI (największa)

- [ ] **Wynik w pamięci (`cc_ret`) na 6502/6800** — odpowiednik planu 35 dla 6502.
  - [ ] `propozycja` [S] Pomiar: ile B na wołanie zjada `cc_ret` w `samples/minic` (listing 6502).
  - [ ] `propozycja` [M] Wynik W≤2 w A/X na 6502 + writeback w crt0 (analogia planu 35) + matryca.
- [ ] **Argumenty w pamięci (`cc_argN`)** — kontynuacja ABI v2 (`abi-v2-design.md` item 5).
  - [ ] `propozycja` [M] ParamAlias rejestrowy albo wygaszenie na v2 (kryterium: brak regresji v1).
  - [ ] `propozycja` [S] Re-pomiar bramki −15% na wołaniowych po ParamAlias (`v2-sizes.md` dziś +1,7 B).

## Model wartości i IR

- [ ] **CSE między blokami / LICM** — Cell IR nie widzi żywotności.
  - [ ] `propozycja` [S] Pomiar: ile powtarzanych podwyrażeń w bloku i niezmienników pętli (skrypt na IR).
  - [ ] `propozycja` [M] LICM prostych niezmienników pętli (po pomiarze).
- [ ] **Bezpośredni emiter VReg (`--regalloc`)** — keep-set dziś doradczy.
  - [ ] `propozycja` [M] Emiter VReg→asm na 1 CPU (pilot Z80) + flaga `--regalloc` + matryca vs Cell.
- [ ] **Alokator grafowy** — odroczony świadomie.
  - [ ] `propozycja` [S] Bramka pomiarowa: linear-scan vs greedy na benchach (go/no-go dla grafowego).

## Cele i codegen

- [ ] **DJNZ / liczniki w B (Z80)** — `queued` plan 43 (0/8), w tym dług splitów z planu 42 (poz. 7).
- [ ] **ParamAlias dla big-endian (6800)** — przebieg tylko LE.
  - [ ] `propozycja` [M] Ścieżka aliasów dla BE albo rejestr X dla parametrów (po liveness z planu 42/43).
- [ ] **Tail call na 6800** — brak `SupportsTailCall`.
  - [ ] `propozycja` [S] Pomiar zysk vs ryzyko przy BE i ramkach (go/no-go).
- [ ] **Tail call z ramką (rekurencja wzajemna)** — ograniczenie `Saved`.
  - [ ] `propozycja` [S] Rozpoznanie: ile benchy tracą na `call+ret` z ramką (listing).
- [ ] **Forward-ref = najdłuższa forma** — relaksacja tylko częściowa.
  - [ ] `propozycja` [M] Iteracyjna relaksacja form w `TwoPassAssembler` (wzór: `BranchRelaxer`).
- [ ] **Łańcuchy `jmp`** — `partial` plan 32 poz. 9 (wstrzymane: +3 B na stubie).
  - [ ] `partial` [S] Re-pomiar po planach 40–42 (skoki krótsze po optymalizacjach?).

## Metodologia testów

- [ ] **Wyrocznia ślepa na błędy ramek** — `IrInterpreter` powtarza semantykę `Saved`.
  - [ ] `propozycja` [M] Niezależny model ramek w teście albo fuzz mutujący `Saved` (wykryłby klasę `646d204`).

## Odłożone / kolejka (plany istnieją)

- [ ] `queued` Plan 43 (DJNZ, 0/8).
- [ ] `queued` Plan 34 (VM bytecode, 0/4).
- [ ] SSA / GVN — `propozycja` dopiero po pomiarze z sekcji IR.

## Zamknięte (referencja, nie odznaczać)

- [x] `closed` Ramki vs skoki w przód (`646d204`, `RecursionFrameTests`) — plan 30/32.
- [x] `closed` VRegLift: kill-bez-pokrycia, `__end`, addr-taken → `Pinned` — plan 37.
- [x] `closed` Wynik w HL (Z80/8080) — plan 35. Tail call Z80/8080/6502 — plan 36/40.
- [x] `closed` Pilot ABI v2 `nes` + flaga `--abi` — plan 39 (bramka nie przeszła, default v1).
