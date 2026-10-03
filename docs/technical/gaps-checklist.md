# Czeklista gapów architektury

Źródło analizy: odpowiedź „co jest błędne” + [`mini-c-status.md`](mini-c-status.md) §5.
Odznaczaj po dowiezieniu (listing + `compare.py` + goldeny bez wzrostu).

Flagi statusu: `closed` (zrobione), `queued` (plan istnieje, czeka), `partial` (zaczęte, wstrzymane),
`propozycja` (task bez planu — kandydat do `docs/plans/`).

## Mapa gap → plan

| Gap | Plan / task |
| --- | --- |
| DJNZ + dług splitów | plan 43 (queued, 0/8) |
| VM bytecode | plan 34 (queued, 0/4) |
| Łańcuchy `jmp` | GO z P4 → plan 45 (propozycja) |
| Wynik w A/X (6502/6800) | P1: NO-GO (max 10,6% < 15%) |
| ABI v2 cd., bramka −15% | P2: NO-GO (najlepszy bench −5,2%) |
| CSE/LICM, SSA/GVN | P3: NO-GO (dostępne 2,7%, niezmienniki 1,8%) |
| Wyrocznia ramek | T1: zrobione (`FrameModelTests`, plan 44) |

## Zasada: najpierw pomiar, potem plan

Taski P1–P4 i T1 są w planie 44 ([../plans/44-gap-measurements.md](../plans/44-gap-measurements.md), queued). Wynik wpisz tu (liczba + komenda),
a dopiero na jego podstawie otwórz **jeden** plan (reguła repo: jeden plan w locie). Bez liczb nie ma planu.

- [x] **P1 — koszt `cc_ret` na 6502/6800**: NO-GO. Ruch `cc_ret(_h)` to 0,3–5,2% CODE na 6502
  i 0,3–10,6% na 6800 (max `05_calls` 6800: 26/246 B) — poniżej progu 15%. Samodzielny plan „wynik w A/X” skreślony.
- [x] **P2 — re-pomiar bramki ABI v2**: NO-GO. `--ir vreg --cpu nes`: sample jak w `v2-sizes.md`;
  benche z dopisanym `main`: fib −5,2%, fnptr −1,3%, sw −3,2%, max3 +3,4%, bubble +1,8%.
  Najlepszy wynik 3× poniżej bramki −15%. Planu 39 nie wznawiać.
- [x] **P3 — powtórzenia i niezmienniki w IR**: NO-GO. Na próbce VReg (335 Bin/Un/Cmp): duplikaty 26%,
  ale dostępne tylko 2,7%; niezmienniki pętli 1,8% — oba poniżej 3%. SSA/GVN zostaje odłożone.
- [x] **P4 — łańcuchy `jmp`**: GO z warunkami. 569 łańcuchów jednopoprzednikowych bez fall-through na stubie
  (1707 B, ~2–3% CODE na program; JMP=3 B) + 19 z ryzykiem spadku (wykluczyć). Historyczne +3 B z planu 32
  to artefakt (przekierowanie bez usuwania martwej etykiety). Plan: 45 (propozycja).

## Kolejność realizacji (najpierw pomiar, potem bajty za najmniejsze ryzyko)

1. **P1–P4 + T1 (plan 44)** — równolegle, rozmiar S, zero zmiany codegenu. T1 jak najwcześniej: każda zmiana ramek/alokatora bez niego to zgadywanie.
2. **Plan 43 (DJNZ)** — pętle to trzeci koszt po wywołaniach i ramkach; ma własną bramkę go/no-go (poz. 4). Przed grzebaniem w 6502, bo rusza współdzielony alokator/liveness.
3. **Przy GO z fazy 0, po stosunku zysk/koszt**: wynik W≤2 w A/X na 6502 (GO z P1) → ParamAlias rejestrowy + re-bramka v2 (GO z P2) → LICM prostych niezmienników (GO z P3) → łańcuchy `jmp` (GO z P4).
4. **Ogony i 6800** (małe, niezależne): ParamAlias BE + tail call 6800 (każde po micro-pomiarze) → tail call z ramką (rozpoznanie) → relaksacja form w asemblerze (największe ryzyko regresji, na końcu).
5. **Tor równoległy**: plan 34 (VM) — niezależny od codegen AOT, nie konkuruje o priorytet. **Koniec kolejki**: alokator grafowy (dopiero gdy bramka linear-scan vs greedy pokaże lukę).

Nie zaczynać od ABI v2 ani SSA „bo to właściwe rozwiązanie” — oba mają przegrane bramki (v2: +1,7 B; SSA: brak pomiaru).

## Bez pomiaru nie ruszać (decyzje)

- **Emiter VReg `--regalloc` i alokator grafowy** — keep-set jest doradczy; diagram: [architecture-diagram.md](architecture-diagram.md) §1.
  Zależy od wyniku P3 i bramki linear-scan vs greedy; teraz nie planować.
- **ParamAlias BE (6800), tail call 6800 / z ramką** — małe zyski, ryzyko ramek; po P1.
- **Forward-ref = najdłuższa forma** — relaksacja częściowa; zrobić tylko jeśli `compare.py` pokaże ≥1% na asemblerze.
- ~~Ślepa wyrocznia ramek~~ — zrobione w T1 (`FrameModelTests`, plan 44).

## Odłożone / kolejka (plany istnieją)

- [ ] `queued` Plan 43 (DJNZ, 0/8).
- [ ] `queued` Plan 34 (VM bytecode, 0/4).
- [ ] `propozycja` Plan 45 (łańcuchy `jmp`, GO z P4).

## Zamknięte (referencja, nie odznaczać)

- [x] `closed` Ramki vs skoki w przód (`646d204`, `RecursionFrameTests`) — plan 30/32.
- [x] `closed` VRegLift: kill-bez-pokrycia, `__end`, addr-taken → `Pinned` — plan 37.
- [x] `closed` Wynik w HL (Z80/8080) — plan 35. Tail call Z80/8080/6502 — plan 36/40.
- [x] `closed` Pilot ABI v2 `nes` + flaga `--abi` — plan 39 (bramka nie przeszła, default v1).
- [x] `closed` Pomiary P1–P3 (NO-GO) i P4 (GO → plan 45) + T1 (`FrameModelTests`, mutacja kontrolna) — plan 44.
