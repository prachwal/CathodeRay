# Czeklista gapów architektury

Źródło analizy: odpowiedź „co jest błędne” + [`mini-c-status.md`](mini-c-status.md) §5.
Odznaczaj po dowiezieniu (listing + `compare.py` + goldeny bez wzrostu).

Flagi statusu: `closed` (zrobione), `queued` (plan istnieje, czeka), `partial` (zaczęte, wstrzymane),
`propozycja` (task bez planu — kandydat do `docs/plans/`).

## Mapa gap → plan

| Gap | Plan / task |
| --- | --- |
| DJNZ + dług splitów | plan 43 (closed): DJNZ na Z80; bez zmian na 8080 |
| VM bytecode | plan 34 (queued, 0/10) |
| Łańcuchy `jmp` | plan 45 (closed): bezpieczne łańcuchy wdrożone na stubie |
| Optymalizacje 6809 | plan 47 (closed): ABI v2 argumentów, `TryStep` direct-page, kopie X/Y z licznikiem U |
| Wynik w A/X (6502/6800) | P1: NO-GO (max 10,6% < 15%) |
| ABI v2 cd., bramka −15% | P2: NO-GO (najlepszy bench −5,2%) |
| CSE/LICM, SSA/GVN | P3: NO-GO (dostępne 2,7%, niezmienniki 1,8%) |
| Wyrocznia ramek | T1: zrobione (`FrameModelTests`, plan 44) |

## Zasada: najpierw pomiar, potem plan

Taski P1–P4 i T1 są zamknięte w planie 44 ([../plans/44-gap-measurements.md](../plans/44-gap-measurements.md)). Wynik wpisz tu (liczba + komenda),
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
  wynikało z braku usuwania martwej etykiety; poprawka i testy są w zamkniętym planie 45.

## Kolejność realizacji (najpierw pomiar, potem bajty za najmniejsze ryzyko)

1. **Plan 47 (6809, closed)** — wdrożono tylko warianty zmniejszające CODE bez wzrostu w żadnym programie; wynik D i TFR odrzucone.
2. **Plan 34 (VM bytecode, queued)** — niezależny od codegen AOT i po planie 47.
3. **Ogony i 6800** — ParamAlias BE, tail-call 6800 / z ramką i relaksacja form wymagają osobnego pomiaru oraz planu.
4. **SSA/GVN/LICM i alokator grafowy** — pozostają odłożone do czasu, gdy porównania wskażą konkretny zysk.

Nie ustawiać ABI v2 jako domyślnego bez ponownej bramki rozmiaru (pilot NES z planu 39: +1,7 B); 6809 ma osobny opt-in wyłącznie dla argumentów. SSA/GVN również czeka na pomiar.

## Bez pomiaru nie ruszać (decyzje)

- **Emiter VReg `--regalloc` i alokator grafowy** — keep-set jest doradczy; diagram: [architecture-diagram.md](architecture-diagram.md) §1.
  Zależy od wyniku P3 i bramki linear-scan vs greedy; teraz nie planować.
- **ParamAlias BE (6800), tail call 6800 / z ramką** — małe zyski, ryzyko ramek; po P1.
- **Forward-ref = najdłuższa forma** — relaksacja częściowa; zrobić tylko jeśli `compare.py` pokaże ≥1% na asemblerze.
- ~~Ślepa wyrocznia ramek~~ — zrobione w T1 (`FrameModelTests`, plan 44).

## Plany (status i kolejka)

- [x] `closed` Plan 43 (DJNZ Z80; port 8080 nie zmieniony).
- [ ] `queued` Plan 34 (VM bytecode, 0/10).
- [x] `closed` Plan 47 (6809; argumenty W2 w X/Y/U przez opt-in v2, direct-page i X/Y copy-loop z licznikiem U; wynik D/TFR odrzucone, goldeny bez wzrostu).
- [x] `closed` Plan 45 (łańcuchy `jmp`, GO z P4).

## Zamknięte (referencja, nie odznaczać)

- [x] `closed` Ramki vs skoki w przód (`646d204`, `RecursionFrameTests`) — plan 30/32.
- [x] `closed` VRegLift: kill-bez-pokrycia, `__end`, addr-taken → `Pinned` — plan 37.
- [x] `closed` Wynik w HL (Z80/8080) — plan 35. Tail call Z80/8080/6502 — plan 36/40.
- [x] `closed` Pilot ABI v2 `nes` + flaga `--abi` — plan 39 (bramka nie przeszła, default v1).
- [x] `closed` Pomiary P1–P3 (NO-GO) i P4 (GO → plan 45) + T1 (`FrameModelTests`, mutacja kontrolna) — plan 44.
- [x] `closed` Bazowy target 6809, assembler, emulator, macierz i ABI v2 argumentów opt-in — plan 46 / 47.
